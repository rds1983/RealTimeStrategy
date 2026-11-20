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
    World/MapGrid.cs             map size, 2-unit cell conversion (unit tested)
    World/                       World, Entity, PlayerState, EntityId, command records,
                                 snapshots + deterministic checksum (unit tested)
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
    Rendering/                   renderer, models, shared Myra desktop host
      SceneRenderer.cs           draw list, culling, BasicEffect
      MeshLibrary.cs             device-backed procedural primitive cache
      MeshKey.cs                 value key for cached shapes
      MeshCache.cs               keyed cache + disposal (unit tested)
      ModelFactory.cs            builds cached DrModel hierarchies per (id, palette)
      ModelId.cs                 unit/structure/prop identifiers
      FactionPalettes.cs         Concord / Null / Neutral colors
      GridTextureFactory.cs      generated ground + build-grid textures
      ModelShadow.cs             per-model shadow footprint
      GameUi.cs                  one Desktop, HUD + debug panels, pointer-over-HUD
    Ui/                          Myra markup, generated panels, HUD binding, screens
      GameHud.xmmp               controls legend markup
      GameHudPanel.cs / .Generated.cs
      DebugOverlay.xmmp          debug stats markup (not shipped, not loaded at runtime)
      DebugOverlayPanel.cs       hand-written runtime behavior (kept by the exporter)
      DebugOverlayPanel.Generated.cs  myra-export output (never edit by hand)
    Input/                       input state, picking, selection controller, command controller
    Camera/CameraView.cs         view/projection matrices + frustum culling (unit tested)
    Camera/RtsCamera.cs          focus/yaw/zoom controller, map clamping, focus helpers
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
- `F1` toggles the Myra debug panel (FPS, frame ms, simulation rate/tick/alpha, camera focus + grid cell, distance); stats refresh at 4 Hz.

### Determinism

- One seeded `Random` owned by the world for spread/AI variation; never `DateTime.Now` or `Guid` in simulation.
- No wall-clock or frame-rate dependent logic in `Core`.
- Iteration order is stable (lists + monotonically increasing ids), so tests can assert exact outcomes.
- Determinism is required for tests, not (yet) for lockstep multiplayer.

### Definitions

- Balance data lives in `Core/Definitions` as immutable `record` types: `UnitDef`, `BuildingDef`, `WeaponDef`, `FactionDef`, plus value types `ResourceCost` and `Footprint` (in 2 × 2 unit build cells).
- `DefaultDefinitions` mirrors `Design/GameDesign.md` (4 units, 5 structures, 4 weapons, 2 factions). Update the design tables and this file together; code is authoritative at runtime.
- `DefinitionRegistry` validates before exposing data: unique ids, non-empty names/roles, positive health/build time/speed/radius/weapon stats, non-negative costs/supply/armor, resolves weapon/prerequisite/produced-unit references, and rejects self-requirements. All errors are aggregated into one `ArgumentException` listing each problem.
- Gameplay systems receive the registry and never hard-code stats. The prototype build radius is `DefinitionRegistry.BuildRadiusUnits = 12`.

### Entity model

- `Entity` is a plain class: `EntityId`, `PlayerId`, `EntityKind`, the matching `UnitDef`/`BuildingDef` reference, `DefinitionId`, current/previous position (render interpolation), facing, health/max health, armor, radius, speed and a deferred-removal flag. Stats are copied at spawn.
- `PlayerState` holds `FactionId`, alloy, energy and supply used/cap. Supply is maintained incrementally when units/structures spawn or are removed.
- `World` owns the `DefinitionRegistry`, a seeded `Random`, both players, an ordered `List<Entity>` + id lookup, and a `WorldCommand` queue. `Step(dt)` copies previous positions, applies commands, removes dead/despawned entities at tick end, then increments the tick.
- Commands are data records (`SpawnUnit`, `SpawnBuilding`, `Damage`, `Despawn`) applied only at tick start. Unknown targets are ignored; invalid commands never throw mid-tick.
- `ComputeChecksum()` is a deterministic FNV-1a hash of tick, players and entities; `CreateSnapshot()` returns immutable entity/player snapshots for tests and replay tooling.
- A spatial index for range queries is added in T-010.

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
- **Lens:** 45° vertical FOV, near 0.5, far 500. `RtsCamera` drives the `CameraView`; the demo world starts at focus `(0, 0)` and distance 80 (max zoom) so both bases fit.
- **Lighting:** `BasicEffect` with per-pixel lighting, ambient `0.42`, one directional light (`0.78`, direction `(-0.45, -1, -0.35)`), specular `0.08` / power 24. No textures yet.
- **Draw list:** `SceneRenderer` keeps a preallocated `List<MeshDrawCommand>` of `(DrMeshPart, world, diffuse, emissive, texture, alpha, transparent)`; no per-frame allocation once the list capacity is reached. `SubmittedCount` / `CulledCount` / `DrawnCount` are exposed for debugging.
- **World rendering:** `VectorFrontGame.CreateWorldScene` instantiates the registry/world and spawns 18 demo entities (both factions, all MVP model types) through `WorldCommand`s. Each frame maps `Entity` + faction to a cached `DrModel` (`EntityModels`) and submits it at `Vector3.Lerp(PreviousPosition, Position, SimulationClock.Alpha)`. Current smoke state: 80 cached meshes, 18 models, 18 entities.
- **Ground and shadows:** the ground is a 120 × 120 plane with UV scale 60, textured by a generated 128 px tile whose edge lines map to 2-unit `MapGrid` cells. Four thin boxes mark the map border. Every entity submits a scaled disc at its interpolated ground position (alpha 0.4, transparent pass); `ModelShadow` provides the per-`ModelId` radius.
- **Build-grid overlay:** a second copy of the ground mesh at y = 0.03 uses a transparent cyan tile texture (alpha 120). Toggle with `G`; it defaults off and the `--build-grid` smoke flag enables it for deterministic screenshots.
- **Transparent pass:** `SceneRenderer` collects textured/alpha commands (`SubmitTransparent`) and draws them after opaque geometry with `AlphaBlend` + `DepthRead` (no depth writes). Opaque commands use `Opaque` + `Default`.
- **Materials:** `ModelFactory` creates three `DrMaterial`s per model (chassis, panel, accent). The accent uses a dark diffuse base (35 % of the accent) plus the full accent as emissive, so energy details stay colored instead of clipping to white.

### Mesh and model pipeline

- `MeshLibrary` (T-004) wraps `MeshPrimitives` and caches meshes by `MeshKey` (kind, dimensions, tessellation, UV scales, handedness, plane normal/back-face, cylinder cap). `MeshCache` owns the dictionary and disposal and is unit tested through an injected factory; `MeshLibrary` maps typed calls to keys and invokes the primitives.
- The public API mirrors DigitalRiseModel's actual parameter names: cylinder `height`/`radius`/`capped`, cone `radius`/`height`, capsule `length`/`radius`, disc `radius`/`sectorAngle`; planes default to `NormalDirection.UpY` because RTS ground is horizontal. Look at the package source, not the README, when adding a shape.
- `MeshLibrary.Count` is logged by the smoke test (currently 80 unique primitive meshes for the demo world) and confirms that identical requests share one mesh.
- `ModelFactory` (T-005) builds a `DrModel` per `(ModelId, FactionPaletteId)`: one `DrModelBone` per part, each carrying a `DrMesh` with a single `DrMeshPart` cloned from the cached primitive and a `DefaultPose` (`SrtTransform`) for its local transform. `SceneRenderer.Submit(DrModel, world)` calls `CopyAbsoluteBoneTransformsTo` and draws each mesh at `absoluteBone * world`, reading diffuse/emissive from the part's `DrMaterial`. No parallel model/material/part types are used.
- **Ownership:** cloned parts share vertex/index buffers with the `MeshLibrary` templates. The library owns and disposes the GPU buffers; model objects are cached for the app lifetime and must not be disposed individually while the library is alive.
- Trap: `BasicEffect.EmissiveColor` adds directly to the lit result. Bright diffuse + bright emissive clips to white; emissive materials therefore use a dark diffuse base (see the accent `DrMaterial` in `ModelFactory`).
- `DrMesh`/`DrMeshPart` are drawn manually: set `BasicEffect.World/View/Projection`, `DiffuseColor`, then `GraphicsDevice.DrawIndexedPrimitives`.
- Materials are simple: chassis diffuse color, ambient/directional light, no textures except the ground grid and UI. Emissive accents plus additive glow discs provide the sci-fi look without custom shaders.
- Shadows are fake (blob discs, T-007). Real shadow mapping and bloom are post-prototype.

### Camera

`CameraView` (T-003) owns the view/projection matrices and a `BoundingFrustum`, with `SetViewport`, `LookAt` and two `IsVisible` overloads (sphere, or local bounds + world matrix). It is pure math and unit tested.

`RtsCamera` (T-006) drives it:

- **Pose:** ground focus point, fixed 55° pitch, yaw requested in 45° steps (`Q`/`E`) and eased toward the target at 14/s (snaps within 0.01 rad), distance 20–80 with 45 as the gameplay default. Camera position is `focus + (distance·cos(pitch)·sin(yaw), distance·sin(pitch), distance·cos(pitch)·cos(yaw))`.
- **Pan:** arrow keys and an 8 px edge zone (plus middle-mouse drag) move focus in camera-relative axes. Speed is `45 u/s × distance / 45`, so panning feels consistent at any zoom. Focus clamps to the map (`±60` on X/Z).
- **Zoom:** mouse wheel notches adjust a target distance by 6 units; `Update(dt)` smooths toward it with a 12/s exponential and snaps under 0.01 units.
- **Drag:** middle-mouse drag converts pixels to world units at the focus plane via `WorldUnitsPerPixel(viewportHeight)` and grabs the map (inverse X, forward Y).
- **API:** `FocusOn(point)` (flattens Y to the ground) is ready for `H`/`Space`/minimap bindings in T-015; `Rotate`, `Pan`, `PanBy`, `Zoom`, `Update`.
- **Demo world:** the temporary demo starts at distance 80 (max zoom) so both bases fit. Smoke runs ignore camera input so screenshots are deterministic.
- **Debug overlay:** `Focus x, z | dist` is refreshed with the other stats at 4 Hz.

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
- `GameUi` owns the single Myra `Desktop` and a transparent full-screen host `Panel`. It hosts:
  - `GameHudPanel` — always-visible top-right controls legend (pan/zoom/rotate/debug/quit);
  - `DebugOverlayPanel` — top-left stats, toggled with `F1`; its runtime behavior lives in the hand-written partial.
- Panel backgrounds use `#040810B4` / `#040810C8` (dark blue with alpha). Remember Myra's `#RRGGBBAA` order; a red panel means the color was written as `#AARRGGBB`.
- `GameUi.IsPointerOverHud(Point)` reports whether the cursor is over the HUD so camera edge-pan and middle-drag are suppressed there. Full UI/world input routing (clicks, selection, modals) arrives in T-034.
- HUD shell, resource bar, minimap and command card arrive with their systems; the current HUD intentionally shows only controls that exist.

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
- **Headless world tests:** `new World(registry, seed)`, enqueue commands, `Step(0.05f)` N times, assert state. No graphics, no clock.
- **World:** spawn commands apply at tick start, id lookup and map clamping, supply used/cap updates and release on removal, despawn/lethal damage deferred to tick end, unknown targets ignored, resource/tick-sensitive FNV checksums, immutable snapshots, and seeded RNG reproducibility.
- **Determinism test:** run the same seed + command script twice and compare a world checksum/snapshot.
- **Timing tests:** `SimulationClock` covers 20 Hz accumulation across 60/20 FPS frame patterns, partial ticks and alpha, invalid elapsed input, and the 5-step catch-up clamp (excess time dropped, not banked).
- **Rendering math:** `CameraView` tests cover frustum visibility (origin, behind camera, outside FOV, beyond far plane, near-plane intersection), world-matrix bounds culling, `LookAt` changes, aspect/projection and invalid input.
- **RTS camera:** 15 tests cover defaults, distance/pitch, camera-relative panning, distance-scaled pan speed, map clamping, focus flattening, 45° yaw requests with smooth easing/wrapping, zoom clamping/smoothing, world-units-per-pixel, invalid bounds and viewport.
- **Mesh cache:** equal keys reuse one mesh, different keys create separate meshes, disposal releases every cached mesh exactly once, access after dispose throws, and the key includes every shape parameter with neutral defaults.
- **Definitions:** default content counts and key balance values match the design doc; duplicate ids, missing weapon/prerequisite/produced-unit references, self-requirements, negative costs, zero health/range/cooldown/footprint, blank names and unknown id lookups all fail with messages naming the offending field.
- **Map grid:** `MapGrid` tests cover map/cell constants, corner/center/edge conversion, cell-center round-trips and inside-map checks; the ground UV scale uses the same 2-unit cell constant, so the texture grid and build placement cannot drift.
- **Model demo (manual):** every entity model appears with a distinct silhouette and correct palette; accents stay colored (not white); shadows sit under models; debug stats report entity count (currently 18).
- **Manual smoke checklist** for rendering/UI tasks: window opens, camera pans/zooms, models visible with correct colors, no exceptions in log, 60 FPS.
- **Smoke test:** `dotnet run --project RealTimeStrategy -- --smoke-test 30` creates the window/graphics device, renders 30 frames and exits with code 0. Add `--overlay` to force the debug overlay visible, `--build-grid` to show the build grid, and `--screenshot <path.png>` to save the backbuffer of the final frame — use this for visual checks instead of OS window capture.
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
| Per-unit draw calls at 300 units | Shared mesh cache; models currently use 8–20 parts each, so 300 units would far exceed the 400 draw-call budget. T-048 must batch by mesh/material or merge static parts into combined meshes before the perf target is enforced. |

---

## 11. Future extensions (not in MVP)

- Fog of war and line of sight (needs per-tile visibility + shroud rendering).
- Asymmetric factions and a real tech tree (definitions already data-driven).
- Lockstep multiplayer (deterministic sim is the prerequisite).
- Map format + editor-independent JSON maps.
- Instanced rendering and a custom lit/emissive shader for a stronger visual identity.
