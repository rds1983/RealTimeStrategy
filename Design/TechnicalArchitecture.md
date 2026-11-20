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
  RealTimeStrategy.csproj        net8.0 console app, MonoGame output
  Program.cs                     entry point, GraphicsDeviceManager setup
  VectorFrontGame.cs             MonoGame Game subclass, hosts the frame loop
  Assets/                        fonts, Myra styles, generated textures, audio, maps
  Core/                          pure simulation — no MonoGame types except Vector2/3 math
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
  Game/                          presentation and platform glue
    Rendering/                   mesh cache, model factory, renderer, effects, debug draw
    Input/                       input state, picking, selection controller, command controller
    Camera/                      RtsCamera
    Ui/                          Myra widgets, HUD binding, screens
    Audio/                       placeholder SFX hooks
  Data/                          JSON map/balance overrides (optional, later)
RealTimeStrategy.Tests/          xUnit, ProjectReference to the game project
Design/                          documentation
```

`Core` may reference `Microsoft.Xna.Framework` math structs (`Vector2`, `Vector3`) and nothing else from MonoGame. It must never touch `GraphicsDevice`, `ContentManager`, `Game`, or input devices. Tests exercise `Core` directly and simulate ticks with a fixed `float dt`.

Rationale for two projects instead of three: the user's layout specifies one game project plus a test project. A future `RealTimeStrategy.Core` extraction is possible without changing the namespaces.

---

## 3. Dependencies

| Package | Version | Use |
| --- | --- | --- |
| `MonoGame.Framework.DesktopGL` | 3.8.6.1 | Window, input, rendering, audio |
| `DigitalRiseModel.MonoGame` | 0.7.6 | `DrMesh`, `MeshPrimitives` (box, sphere, cylinder, cone, capsule, torus, disc, plane, geosphere) |
| `Myra` | 1.6.8 | HUD, panels, buttons, dialogs |
| `xunit` | 2.9.2 | Unit tests |
| `xunit.runner.visualstudio` | 2.8.2 | Test discovery/execution |
| `Microsoft.NET.Test.Sdk` | 17.11.1 | Test host |

Target framework: **net8.0** for both projects (LTS, supported by MonoGame 3.8 and the installed SDKs). `RollForward=Major` lets the app run on a newer installed runtime.

The game project is an `Exe` (console attached) during development so logs and crash output are visible; switch to `WinExe` for the release build if the console becomes distracting.

`DigitalRiseModel` returns CPU/GPU buffers tagged with vertex type `VertexPositionNormalTexture`; we render those buffers with a `BasicEffect` configured per material. It pulls in `Newtonsoft.Json` and `XNAssets.MonoGame` transitively.

---

## 4. Simulation model

### Tick loop

- Simulation runs at a fixed **20 Hz** (`dt = 0.05 s`); rendering is uncapped/VSync.
- `VectorFrontGame.Update` accumulates real time and runs zero or more simulation steps. Presentation interpolates entity transforms between the previous and current simulation state.
- All gameplay changes happen inside `World.Step(dt)`: movement, combat, economy, production, AI.
- Input never mutates the world directly. It enqueues `Command` values; commands are validated and applied at the start of a tick.

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

### Frame pipeline

1. Clear, update camera matrices, build the visible draw list (frustum cull by bounding sphere).
2. Draw ground plane with a generated grid texture.
3. Draw blob shadows (transparent discs) for units and structures.
4. Draw structures, then units, then projectiles/effects (sorted by mesh to reduce state changes).
5. Draw selection rings, move markers and debug overlays (depth-tested lines).
6. Draw Myra desktop last, in a separate 2D pass.

### Mesh and model pipeline

- `MeshLibrary` creates and caches `DrMesh` instances once from `MeshPrimitives` (unit box, cylinder, sphere, cone, capsule, torus, disc, plane variants). Cache key = primitive + parameters, so repeated models share vertex/index buffers.
- `ModelFactory` assembles named models from parts, e.g. `Striker = torso box + 2 leg cylinders + cockpit sphere + gun barrel cylinder`. Each part has a mesh key, local transform and material id. Models are built once at startup.
- `DrModel`/`DrMeshPart` are drawn manually: set `BasicEffect.World/View/Projection`, `DiffuseColor`, `EmissiveColor`, then `GraphicsDevice.DrawIndexedPrimitives`.
- Materials are simple: chassis diffuse color, emissive accent color, ambient/directional light, no textures except the ground grid and UI. Emissive accents plus additive glow discs provide the sci-fi look without custom shaders.
- Shadows are fake (blob discs). Real shadow mapping and bloom are post-prototype.

### Camera

`RtsCamera` keeps focus point, yaw, pitch (fixed 55°), distance (20–80), and computes view/projection. Pan uses arrow keys, edge scroll or middle-mouse drag, is focus-plane based and scaled by zoom; rotation snaps in 45° steps. Camera bounds are clamped to the map plus a margin.

### Picking

Screen ray → ground plane for world position; entity picking uses distance-to-ray vs. bounding radius, with units preferred over structures. Box selection projects unit positions to screen space and tests against the rectangle.

---

## 6. UI (Myra)

- `MyraEnvironment.Game = game` once at startup; a `Desktop` is the root and receives input after the game world when a modal exists.
- Styles: a Myra stylesheet XML under `Assets/Ui/`, loaded from disk (see assets below), plus `FontSystem`/`FontSystemSharp` for the UI font.
- HUD composition: `Panel` roots anchored to screen edges, `HorizontalStackPanel` for the top bar, `Grid` for the command card, custom `Widget` subclasses for the minimap and health bars if needed.
- HUD reads a read-only snapshot of the world each frame; it never mutates simulation state directly — clicks enqueue commands.
- Keyboard shortcuts remain in the input controller, not in Myra, so gameplay input works regardless of UI focus. When a text field or modal is open, gameplay input is suppressed.
- All screens (HUD, pause, victory/defeat) are Myra widgets toggled by game state.

**Risk to validate early:** Myra's MonoGame backend expects a stylesheet and fonts; verify the exact initialization and asset layout against the Myra samples in T-034 before building the HUD on top of it.

---

## 7. Assets and content pipeline

- **No MGCB content project.** Assets are read at runtime from `RealTimeStrategy/Assets` (copied to output via csproj `Content` items with `CopyToOutputDirectory=PreserveNewest`).
- Textures needed for the prototype are generated in code (ground grid, white 1×1 pixel, selection/glow discs). Fonts and Myra styles are real files under `Assets/`.
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

Debug overlay (`F1`) shows FPS, frame ms, simulation ms, entity count, draw calls (via a counter around draws) and camera position.

---

## 9. Testing strategy

- **Unit tests** (`RealTimeStrategy.Tests`): definitions/validation, economy math, harvesting, supply, production queues, construction/refund, damage formula, targeting/leash, order transitions, pathfinding, win/lose conditions, AI deterministic smoke runs.
- **Headless world tests:** `new World(seed, map)`, enqueue commands, `Step(0.05f)` N times, assert state. No graphics, no clock.
- **Determinism test:** run the same seed + command script twice and compare a world checksum/snapshot.
- **Manual smoke checklist** for rendering/UI tasks: window opens, camera pans/zooms, models visible with correct colors, no exceptions in log, 60 FPS.
- **Smoke test:** `dotnet run --project RealTimeStrategy -- --smoke-test 30` creates the window/graphics device, renders 30 frames and exits with code 0. Use it to verify the graphics stack without interacting with the game.
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
