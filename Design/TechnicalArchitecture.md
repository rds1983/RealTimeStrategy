# Vector Front — Technical Architecture

How the prototype is built. Update this file whenever structure, dependencies or a subsystem changes.

---

## 1. Constraints

| Constraint | Consequence |
| --- | --- |
| MonoGame provides no scene graph | We own the render loop, camera, draw ordering and culling. |
| DigitalRiseModel has **no renderer** | It is used only to build `DrMesh` data; rendering uses MonoGame `BasicEffect`. |
| All 3D art is procedural | Models are assembled in code from `MeshPrimitives`; no model files, no importer. |
| Prototype, not a framework | Prefer plain C# classes over generic engines/ECS frameworks. |
| Testability | Simulation logic must run headless in xUnit, without `GraphicsDevice`. |
| Small dependency surface | Adding a NuGet package requires developer approval and a docs update. |

---

## 2. Solution layout

```
RealTimeStrategy.slnx
RealTimeStrategy/
  RealTimeStrategy.csproj        net8.0 app, MonoGame output
  Program.cs                     entry point
  VectorFrontGame.cs             MonoGame Game subclass, frame loop, simulation clock wiring
  Assets/                        runtime assets: fonts, styles, generated textures, audio, maps
  Core/                          pure simulation — no MonoGame types except Vector2/3 math
    GameInfo.cs                  title/version metadata, safe for headless tests
    Simulation/                  SimulationClock (fixed-step accumulator)
    Definitions/                 UnitDef, BuildingDef, WeaponDef, DefinitionRegistry
    World/                       World, Entity, PlayerState, EntityId, spawn/despawn
    Orders/                      Order types, OrderQueue, order execution
    Navigation/                  NavGrid, AStar, Path, flow helpers
    Movement/                    locomotion, separation, path following
    Combat/                      weapons, targeting, projectiles, damage
    Economy/                     resources, harvesting, supply, construction
    Production/                  queues, rally points, requirements
    Ai/                          scripted opponent state machine
    Math/                        fixed math helpers, geometry queries
  Presentation/                  rendering, input, UI and platform glue
    Content/AssetPaths.cs        resolves runtime Myra asset root
    Rendering/                   mesh cache, model factory, renderer, effects, debug overlay host
    Ui/                          Myra markup, generated panels, HUD binding, screens
      DebugOverlay.xmmp          design-time markup (not shipped, not loaded at runtime)
      DebugOverlayPanel.cs       hand-written runtime behavior (kept by the exporter)
      DebugOverlayPanel.Generated.cs  myra-export output (never edit by hand)
    Input/                       input state, picking, selection controller, command controller
    Camera/CameraView.cs         view/projection matrices + frustum culling (unit tested)
    Camera/                      RtsCamera (T-006)
    Audio/                       placeholder SFX hooks
  Data/                          JSON map/balance overrides (optional, later)
RealTimeStrategy.Tests/          xUnit, ProjectReference to the game project
  GameInfoTests.cs               headless smoke test of the Core assembly
  Simulation/                    SimulationClock tests
dotnet-tools.json                local tool manifest (myra-export)
Design/                          documentation
```

`Core` may reference `Microsoft.Xna.Framework` math structs (`Vector2`, `Vector3`) and nothing else from MonoGame. It must never touch `GraphicsDevice`, `ContentManager`, `Game`, `GameTime`, or input devices. Tests exercise `Core` directly and simulate ticks with a fixed `float dt`.

`Presentation` is named that way (not `Game`) because a `RealTimeStrategy.Game` namespace would shadow MonoGame's `Game` type in every file under the root namespace.

Rationale for two projects instead of three: the user's layout specifies one game project plus a test project. A future `RealTimeStrategy.Core` extraction is possible without changing the namespaces.

---

## 3. Dependencies

| Package | Version | Use |
| --- | --- | --- |
| `MonoGame.Framework.DesktopGL` | 3.8.6.1 | Window, input, rendering, audio |
| `DigitalRiseModel.MonoGame` | 0.7.6 | `DrMesh`, `MeshPrimitives` (box, sphere, cylinder, cone, capsule, torus, disc, plane, geosphere) |
| `Myra` | 1.6.8 | HUD, panels, buttons, dialogs; brings FontStashSharp 1.6.2 transitively for text |
| `myra-export` (local dotnet tool) | 1.6.8 | Exports `.xmmp` Myra markup to C# (`dotnet tool restore` once per clone) |
| `xunit` | 2.9.2 | Unit tests |
| `xunit.runner.visualstudio` | 2.8.2 | Test discovery/execution |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | Test host |

Target framework: **net8.0** for both projects (LTS, supported by MonoGame 3.8 and the installed SDKs). `RollForward=Major` lets the app run on a newer installed runtime.

The game project is an `Exe` (console attached) during development so logs and crash output are visible; switch to `WinExe` for the release build if the console becomes distracting.

`DigitalRiseModel` returns CPU/GPU buffers tagged with vertex type `VertexPositionNormalTexture`; we render those buffers with a `BasicEffect` configured per material. It pulls in `Newtonsoft.Json` and `XNAssets.MonoGame` transitively.

---

## 4. Simulation model

### Tick loop

- Simulation runs at a fixed **20 Hz** (`dt = 0.05 s`); rendering is uncapped with VSync. MonoGame's own fixed timestep is disabled (`IsFixedTimeStep = false`) so there is exactly one accumulator.
- `SimulationClock` (`Core/Simulation`) converts real elapsed seconds into 0–5 steps per frame (0.25 s budget). Excess time — debugger pause, window drag, breakpoint — is dropped instead of banked, preventing a catch-up spiral.
- `VectorFrontGame.Update` passes `gameTime.ElapsedGameTime` as `double` into the clock and calls `StepSimulation(0.05f)` per tick. `Core` never sees `GameTime`.
- Presentation interpolates entity transforms between the previous and current simulation state using `SimulationClock.Alpha`.
- All gameplay changes happen inside `World.Step(dt)`: movement, combat, economy, production, AI.
- Input never mutates the world directly. It enqueues `Command` values; commands are validated and applied at the start of a tick.
- `F1` toggles a debug overlay with FPS, frame ms, simulation rate, tick count and interpolation alpha. It is drawn with FontStashSharp/Rajdhani and rebuilds its text at 4 Hz into a reused `StringBuilder`.

### Determinism

- One seeded `Random` owned by the world for spread/AI variation; never `DateTime.Now` or `Guid` in simulation.
- No wall-clock or frame-rate dependent logic in `Core`.
- Iteration order is stable (lists + monotonically increasing ids), so tests can assert exact outcomes.
- Determinism is required for tests, not (yet) for lockstep multiplayer.

### Entity model

- `Entity` is a plain class with `EntityId Id`, `PlayerId Owner`, `EntityKind Kind`, position, radius, HP/armor, optional `MoveState`, `WeaponState`, `ProductionState`, `HarvestState`.
- Definitions (`UnitDef`, `BuildingDef`) are immutable data records; instances hold runtime state and a reference to their definition.
- `World` keeps entities in a `List<Entity>` plus a `Dictionary<EntityId, Entity>` and a spatial hash for range queries. Dead entities are deferred-removed after the tick.

### Navigation and movement

- `NavGrid` over the 120 × 120 map with 2 × 2 unit cells (60 × 60 cells); structures and blockers mark cells impassable.
- A* on the grid, 8-directional with corner cutting disallowed; paths are simplified with line-of-sight string pulling.
- Units follow waypoints with steering: desired velocity, max acceleration, separation from neighbors, and a stuck timer that repaths or stops.
- Unit-to-unit collision is soft (separation forces); buildings block movement via the nav grid.
- Flow-field or crowd pathfinding is out of scope; the expected unit count (≤ 300) does not need it.

### Orders

- `Order` is a small value type/enum + parameters: `Move`, `AttackMove`, `Attack(target)`, `Hold`, `Stop`, `Harvest(node)`, `ReturnCargo`, `Build(placement)`, `Produce(defId)`, `SetRally(point)`.
- Each unit has a queue (max 4 for player orders, unlimited internal for AI); structures only accept production/rally orders.
- `OrderExecutor` is a per-tick system that inspects the current order and writes movement/weapon intents. Combat units auto-acquire targets only between explicit orders.

---

## 5. Rendering

### Frame pipeline (foundation, T-003)

1. `GraphicsDevice.Clear` clears color and depth; the camera supplies view/projection and a frustum.
2. `VectorFrontGame.Draw` fills a per-frame draw list (`SceneRenderer.BeginFrame` → `Submit` → `Render`).
3. `Render` culls each command by transforming the mesh's local `BoundingBox` to world space and testing it against the frustum.
4. Surviving meshes are drawn from their existing `DrMeshPart` vertex/index buffers with one shared `BasicEffect`.
5. Myra's desktop renders last, after the 3D pass.

Later passes add the textured/grid ground (T-007), blob shadows, units/structures/projectiles (T-005+), and selection markers (T-017).

### Renderer details

- **Coordinates:** right-handed (`Matrix.CreateLookAt` + `CreatePerspectiveFieldOfView`), default `toLeftHanded: false` DigitalRiseModel primitives, `CullMode.CullCounterClockwiseFace`.
- **Lens:** 45° vertical FOV, near 0.5, far 500. The current reference camera is fixed at `(38, 32, 38)` looking at `(0, 1, 0)`; `RtsCamera` (T-006) will drive the same `CameraView`.
- **Lighting:** `BasicEffect` with per-pixel lighting, ambient `0.42`, one directional light (`0.78`, direction `(-0.45, -1, -0.35)`), specular `0.08` / power 24. No textures yet.
- **Draw list:** `SceneRenderer` keeps a preallocated `List<MeshDrawCommand>` of `(DrMesh, world matrix, diffuse color)`; no per-frame allocation once the list capacity is reached. `SubmittedCount` / `CulledCount` / `DrawnCount` are exposed for debugging.
- **Reference scene:** `VectorFrontGame.CreateReferenceScene` builds a 120 × 120 ground plane (`NormalDirection.UpY`, y = 0) and three boxes (2 × 2 × 2, two scaled) to validate winding, normals, depth and lighting. It is temporary and is replaced once the world supplies entities.

### Mesh and model pipeline

- `MeshLibrary` (T-004) creates and caches `DrMesh` instances once from `MeshPrimitives` (unit box, cylinder, sphere, cone, capsule, torus, disc, plane variants). Cache key = primitive + parameters, so repeated models share vertex/index buffers. T-003 currently calls `MeshPrimitives` directly for the reference scene.
- `ModelFactory` (T-005) assembles named models from parts, e.g. `Striker = torso box + 2 leg cylinders + cockpit sphere + gun barrel cylinder`. Each part has a mesh key, local transform and material id. Models are built once at startup.
- `DrMesh`/`DrMeshPart` are drawn manually: set `BasicEffect.World/View/Projection`, `DiffuseColor`, then `GraphicsDevice.DrawIndexedPrimitives`.
- Materials are simple: chassis diffuse color, ambient/directional light, no textures except the ground grid and UI. Emissive accents plus additive glow discs provide the sci-fi look without custom shaders.
- Shadows are fake (blob discs, T-007). Real shadow mapping and bloom are post-prototype.

### Camera

`CameraView` (T-003) owns the view/projection matrices and a `BoundingFrustum`, with `SetViewport`, `LookAt` and two `IsVisible` overloads (sphere, or local bounds + world matrix). It is pure math and unit tested. `RtsCamera` (T-006) keeps focus point, yaw, pitch (fixed 55°), distance (20–80) and feeds a `CameraView`; pan uses arrow keys, edge scroll or middle-mouse drag, rotation snaps in 45° steps, bounds clamp to the map plus a margin.

### Picking

Screen ray → ground plane for world position; entity picking uses distance-to-ray vs. bounding radius, with units preferred over structures. Box selection projects unit positions to screen space and tests against the rectangle.

---

## 6. UI (Myra)

- `VectorFrontGame.LoadContent` sets `MyraEnvironment.Game` and roots `MyraAssets.DefaultAssetManager` at `AssetPaths.UiRoot` (`<exe>/Presentation/Ui`, created on startup) before creating widgets.
- Widget trees are authored as `.xmmp` (MML) files and exported to C# with the pinned local tool:
  ```bash
  powershell -ExecutionPolicy Bypass -File tools/export-ui.ps1
  ```
  The script restores `myra-export` (pinned in `dotnet-tools.json`) and exports every `Presentation/Ui/**/*.xmmp`. The exporter writes `<Class>.cs` (hand-editable, never overwritten) and `<Class>.Generated.cs` (always regenerated, never edit by hand). Both are committed.
- `.xmmp` files live next to their generated C# in `Presentation/Ui/` and are **not** runtime assets: they are not copied to the output and the game never parses them.
- Export options are stored in the markup: `Namespace`, `Class` and `FieldsVisibility="Private"`. Generated widget fields are private; the hand-written partial accesses them because it is the same class. Add `FieldsVisibility` to any new `.xmmp`.
- Myra's asset manager is rooted at `<exe>/Presentation/Ui` (created at startup), mirroring the source folder. That keeps the relative paths stored by the exporter valid at runtime: `../../Assets/Fonts/Rajdhani-Regular.ttf:16` resolves to `RealTimeStrategy/Assets/...` during export and to `<exe>/Assets/...` at runtime. No path rewriting is needed.
- Font syntax in MML is `<path>:<size>` for TTF/OTF. Color strings are `#RRGGBBAA` — alpha is **last**, unlike XNA's packed color format.
- `DebugOverlay` hosts the generated `DebugOverlayPanel` (a `VerticalStackPanel` of labels) inside a transparent full-screen `Panel`; the panel itself is anchored top-left with a translucent `#040810C8` backdrop and content-sized. The panel's runtime behavior (stats, refresh, toggle) lives in the hand-written `DebugOverlayPanel.cs` partial, not in the generated file.
- HUD shell, theme stylesheet, and input routing (`Desktop.IsMouseOverGUI`) arrive in T-034. Until then the debug overlay is the only Myra consumer.

---

## 7. Assets and content pipeline

- **No MGCB content project.** Assets are read at runtime from `RealTimeStrategy/Assets` (copied to output via csproj `Content` items with `CopyToOutputDirectory=PreserveNewest`).
- Textures needed for the prototype are generated in code (ground grid, white 1×1 pixel, selection/glow discs).
- `Assets/Fonts/Rajdhani-{Regular,SemiBold}.ttf` (SIL Open Font License 1.1, `OFL.txt` alongside) is loaded by Myra via `../../Assets/Fonts/<name>.ttf:<size>` paths from `Presentation/Ui` markup.
- Myra markup (`Presentation/Ui/*.xmmp`) is design-time source exported to committed C#; the game does not parse XMMP at runtime and the markup is not copied to the output. Regenerating UI requires `dotnet tool restore`.
- Maps and balance data are C# definitions initially; JSON map files can be added under `Assets/Maps` later without changing the architecture.
- Custom `.fx` shaders are out of scope for the MVP: runtime effect compilation is not guaranteed across MonoGame backends. If shaders are added later, precompile them to `.mgfx` and document the build step.

---

## 8. Performance budget

| Metric | Target |
| --- | --- |
| Simulation | ≤ 2 ms per tick at 20 Hz with 300 units |
| Render | 60 FPS at 1080p with 300 units and projectiles |
| Allocations | No per-frame allocations in `Core` steady state; avoid LINQ in per-tick systems |
| Draw calls | ≤ ~400 per frame; shared mesh buffers, no per-instance mesh creation |
| Startup | < 3 s to a playable match |

Debug overlay (`F1`) currently shows FPS, frame ms, simulation rate, tick count and interpolation alpha. Simulation ms, entity count, draw calls and camera position are added together with their systems.

---

## 9. Testing strategy

- **Unit tests** (`RealTimeStrategy.Tests`): definitions/validation, economy math, harvesting, supply, production queues, construction/refund, damage formula, targeting/leash, order transitions, pathfinding, win/lose conditions, AI deterministic smoke runs.
- **Headless world tests:** `new World(seed, map)`, enqueue commands, `Step(0.05f)` N times, assert state. No graphics, no clock.
- **Determinism test:** run the same seed + command script twice and compare a world checksum/snapshot.
- **Timing tests:** `SimulationClock` covers 20 Hz accumulation across 60/20 FPS frame patterns, partial ticks and alpha, invalid elapsed input, and the 5-step catch-up clamp (excess time dropped, not banked).
- **Rendering math:** `CameraView` tests cover frustum visibility (origin, behind camera, outside FOV, beyond far plane, near-plane intersection), world-matrix bounds culling, `LookAt` changes, aspect/projection and invalid input.
- **Manual smoke checklist** for rendering/UI tasks: window opens, camera pans/zooms, models visible with correct colors, no exceptions in log, 60 FPS.
- **Smoke test:** `dotnet run --project RealTimeStrategy -- --smoke-test 30` creates the window/graphics device, renders 30 frames and exits with code 0. Add `--overlay` to force the debug overlay visible and exercise the Myra/font path. Add `--screenshot <path.png>` to save the backbuffer of the final frame — use this for visual checks instead of OS window capture.
- Tests must be fast (< 10 s total) and must not require a display.

---

## 10. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| DigitalRiseModel is model-only | Own `BasicEffect` renderer from day one (T-003); validate with one box before building models. |
| Myra initialization/style assets are fiddly | Dedicated task (T-034) before HUD work; fall back to code-built widgets if XML styles block progress. |
| MonoGame runtime shader compilation | Use `BasicEffect` only for the MVP; precompile shaders if added later. |
| Floating-point drift across platforms | Determinism only asserted on the same machine/architecture; no cross-platform lockstep requirement. |
| Scope creep (tech tree, fog, campaign) | MVP scope in `GameDesign.md`; new features go to `TASKS.md` only with developer approval. |
| Per-unit draw calls at 300 units | Shared mesh cache, batching by mesh/material; instancing is an optional optimization task if profiling demands it. |

---

## 11. Future extensions (not in MVP)

- Fog of war and line of sight (needs per-tile visibility + shroud rendering).
- Asymmetric factions and a real tech tree (definitions already data-driven).
- Lockstep multiplayer (deterministic sim is the prerequisite).
- Map format + editor-independent JSON maps.
- Instanced rendering and a custom lit/emissive shader for a stronger visual identity.
