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
    World/                       World, Entity, PlayerState, EntityId, SpatialGrid,
                                 command records, snapshots + checksum (unit tested)
    Orders/                     Order/OrderType/OrderQueue/OrderExecutor (unit tested)
    Navigation/                 NavGrid, PathFinder (8-way A*, LOS smoothing, unit tested)
    Movement/                    MoveState + MovementSystem (steering, separation, repath)
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
    Input/                       input state, picking, SelectionModel, OrderController
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
- `DefaultDefinitions` mirrors `Design/GameDesign.md` (4 units, 5 structures, 4 weapons, 3 factions — Concord, Null and the environment-only Neutral faction). Update the design tables and this file together; code is authoritative at runtime.
- `DefinitionRegistry` validates before exposing data: unique ids, non-empty names/roles, positive health/build time/speed/radius/weapon stats, non-negative costs/supply/armor, resolves weapon/prerequisite/produced-unit references, and rejects self-requirements. All errors are aggregated into one `ArgumentException` listing each problem.
- Gameplay systems receive the registry and never hard-code stats. The prototype build radius is `DefinitionRegistry.BuildRadiusUnits = 12`.
- Harvest balance (node reserve, capacity, harvest/deposit seconds, node radius, interaction padding) lives in `EconomyDef` as constants until a second node type exists.

### Entity model

- `Entity` is a plain class: `EntityId`, `PlayerId`, `EntityKind`, the matching `UnitDef`/`BuildingDef` reference, `DefinitionId`, current/previous position (render interpolation), facing, health/max health, armor, radius, speed and a deferred-removal flag. Stats are copied at spawn. Resource nodes use `EntityKind.ResourceNode`, carry a `Reserve`, and have no definition; Harvester units own a `HarvesterState`.
- `PlayerState` holds `FactionId`, alloy, energy and supply used/cap. Supply is maintained incrementally when units/structures spawn or are removed. Nodes belong to `PlayerId.Neutral`, which exists for palettes/ownership checks only and is excluded from snapshots and checksums.
- `World` owns the `DefinitionRegistry`, a seeded `Random`, both players, an ordered `List<Entity>` + id lookup, and a `WorldCommand` queue. `Step(dt)` copies previous positions, applies commands, removes dead/despawned entities at tick end, then increments the tick.
- Commands are data records (`SpawnUnit`, `SpawnBuilding`, `SpawnResourceNode`, `Damage`, `Despawn`) applied only at tick start. Unknown targets are ignored; invalid commands never throw mid-tick.
- `ComputeChecksum()` is a deterministic FNV-1a hash of tick, players and entities, including node reserves and harvester phase/cargo/timer; `CreateSnapshot()` returns immutable entity/player snapshots for tests and replay tooling.
- `SpatialGrid` (T-010) hashes living entities into the same 2-unit `MapGrid` cells and is rebuilt at the end of every `Step`. It exposes radius queries (span or reused `List`, no per-query allocation), `FindNearestEnemy`, and `Count`; `World.Spatial` reflects the last completed tick.
- A separate navigation grid for pathfinding is added in T-011; the spatial hash only answers proximity queries.

### Navigation and movement

- `NavGrid` (T-011) is a passability grid over the same 120 × 120 map/2-unit cells (60 × 60), separate from the spatial hash. `World.Navigation` blocks/unblocks building footprints as structures spawn and are removed; resource nodes block the single cell under their center, reference-counted so two nodes in one cell do not unblock each other (T-018). Map edges are implicit blockers.
- `PathFinder` runs 8-way A* with an octile heuristic, reusable score/state arrays and a `PriorityQueue`. Diagonal steps require both adjacent orthogonal cells to be passable, so units cannot clip wall corners.
- Paths are simplified with `NavGrid.HasLineOfSight` (Bresenham + orthogonal corner checks); every simplified segment is collision-free. The world-space overload returns cell centers and includes the start cell.
- Randomized tests compare path cost against a reference Dijkstra and require ≤ 1.3× optimal; walls, blocked goals, corner traps and smoothing are covered.
- `MovementSystem` (T-012) is invoked by `World.Step` after commands and before final cleanup. Units with a `MoveState` follow `PathFinder` world paths toward the exact destination: accelerate toward waypoints (speed × 5, minimum 3 u/s²), advance within 0.5 units, arrive within 0.3 units, and turn toward velocity at 10 rad/s (`Facing = atan2(-vz, vx)` so models authored facing +X align).
- Separation uses `World.Spatial` neighbors (units only, inside `radius + 1.8`): a weighted push-away vector is added to desired velocity (0.9 moving, 0.35 idle), capped at unit speed/50 %, so units spread instead of stacking.
- Stuck detection checks displacement every 0.5 s (< 0.1 units) and whenever the next waypoint becomes blocked; it re-paths from the current position, stopping only if no route exists. Movement clamps positions to the map and never enters blocked `NavGrid` cells.
- `World.MoveUnit`/`World.StopUnit` are the movement API for now; T-013 orders will call them. Checksums include facing, velocity and destination so movement replay is deterministic.
- Unit-to-unit collision is soft (separation forces); buildings block movement via the nav grid. Flow-field/crowd pathfinding is out of scope for ≤ 300 units.

### Orders

- `Order` is a readonly record (`OrderType` + point/target/building/unit) with factories for `Move`, `AttackMove`, `Attack`, `Hold`, `Stop`, `Harvest`, `ReturnCargo`, `Build`, `Produce` and `SetRally`. Only fields relevant to the type are read.
- Each unit owns an `OrderQueue` (capacity 4, FIFO, head order executes). `replace` clears the queue and stops current movement; non-replace appends. Orders are issued through `World.TryIssueOrder` → `IssueOrderCommand`, so validation and application happen at tick start; invalid orders return false immediately.
- `OrderExecutor` validates ownership/targets/points, starts the head order before movement, and pops completed Move/AttackMove orders after movement. Move-like orders delegate to `World.MoveUnit`; Stop/Hold clear movement and set `HoldPosition` (movement skips held units).
- Combat/build/produce/rally order types are defined but rejected until their systems land (T-021/T-024/T-028). Attack validates the enemy and chases the target position; range/weapon behavior comes with targeting.
- Harvest/ReturnCargo are owned by `HarvestingSystem` but still flow through the queue: `Harvest` starts mining when it reaches the head (so shift-queued harvests do not jump ahead of earlier moves), `ReturnCargo` requires a node assignment. Both pop when the harvester state returns to idle.
- Replacing or stopping an order resets the harvester state, but cargo is kept on the unit; `ReturnCargo` deliberately preserves the node so the trip resumes after unloading.
- Queues, hold flags and current order data are included in the world checksum.

### Harvesting

- `HarvestingSystem` (`Core/Economy`) runs after movement and before order completion. `HarvestPhase` walks ToNode → Harvesting (2.2 s) → ToDropOff → Depositing (0.6 s) → back to ToNode, looping until the node is depleted or invalid.
- Cargo is `min(22, node.Reserve)` per load; the reserve drops at the end of the harvest phase, and a node at zero reserve is removed at tick end. A loaded harvester still completes its delivery after the node disappears — the node is only validated while walking to or mining it.
- Standing positions come from `TryFindStandPoint`: the point at `targetRadius + moverRadius + 1` on the approach side, otherwise the nearest passable cell in rings 1–5 around the target. Arrival tolerates 0.5 units extra because path following stops within its own tolerance. If no stand point exists the order aborts instead of oscillating.
- Drop-offs are the nearest living building owned by the player with `BuildingDef.IsDropOff` (the Nexus). `World.TryFindNearestDropOff` is re-evaluated at each return, so a destroyed Nexus does not leave a harvester stuck.
- `OrderController` context: right-clicking a node issues `Harvest` to the selected Harvesters only (other unit types ignore it and keep their selection); right-clicking an owned drop-off with a Harvester primary issues `ReturnCargo`. Nodes are never selectable.

### Order input

- `OrderController` maps the current selection + cursor context to orders: right-click an enemy → `Attack`; right-click ground → `Move`; a selected structure + right-click ground → `SetRally` (stored on `Entity.RallyPoint` for T-024); right-click a node or owned drop-off → `Harvest`/`ReturnCargo` for Harvesters. Invalid targets/points are ignored without clearing the selection.
- `A` arms attack-move (crosshair); the next left-click issues `AttackMove` instead of selecting. `S` stops, `H` holds; both cancel attack-move mode. Shift queues instead of replacing. `Esc` cancels attack-move, then clears selection, then exits.
- Cursor feedback: crosshair when attack-move is armed or an enemy is hovered with units selected, arrow elsewhere/over the HUD.
- Gameplay only reaches `World.TryIssueOrder`; the controller never mutates the world directly.

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
- **World rendering:** `VectorFrontGame.CreateWorldScene` instantiates the registry/world and spawns the demo scene (both factions, all MVP model types, four alloy nodes) through `WorldCommand`s. Each frame maps `Entity` + owner faction to a cached `DrModel` (`EntityModels`) and submits it at `Vector3.Lerp(PreviousPosition, Position, SimulationClock.Alpha)`. Current smoke state: 89 cached meshes, 19 models, 22 entities.
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

- `Picking.GetRay` unprojects the cursor through `inverse(View * Projection)` (NDC with top-left screen coordinates) and returns a normalized world ray. **Trap:** use `Vector4.Transform` and divide by W; `Vector3.Transform` skips the perspective divide and collapses off-center rays toward the focus.
- `TryGetGroundPoint` intersects that ray with `y = 0`, rejecting rays that are parallel or point away from the ground.
- `PickEntity` tests each living entity's vertical proxy sphere (center at `position + radius·up`, radius `2×entity radius`) and returns the closest unit; only if no unit is hit does it return the closest structure. Units take priority even when a structure is nearer.
- Hover is refreshed every frame only when the window is active, the cursor is inside the client area and not over the HUD. `GameUi.IsPointerOverHud` converts the point with `Widget.ToLocal` because Myra's `Bounds` is local to the widget (using `Bounds.Contains(screenPoint)` checks the wrong rectangle).
- Selected/hovered entities draw cyan ground discs in the transparent pass. That pass disables lighting and back-face culling: primitive discs are horizontal and lighting a downward/zero normal renders them black.
- Picking is pure presentation math over `World`/`CameraView` and covered by known-camera tests at 20/45/80 zoom plus off-center round-trip tests; box selection and click-to-select integration live in `SelectionModel`.

### Selection

- `SelectionModel` stores entity ids (not object references), so pruning and control groups never hold dead entities. It prunes dead/missing ids every frame; `Primary` prefers the first selected unit, otherwise the first living entity.
- Click selection uses `Picking.PickEntity`; clicking a structure replaces the selection with that structure. Shift-click adds (mixed selections are allowed, with units primary).
- Drag selection projects unit positions with `Viewport.Project` and selects only units whose screen centers fall in the rectangle; empty boxes clear unless Shift is held. A 5 px movement threshold distinguishes click from drag.
- Drag feedback is a SpriteBatch rectangle overlay (fill + 1 px border). Hover uses a soft filled cyan disc (alpha 0.22); selection uses a bright thin cyan torus ring (alpha 0.95) at the entity's base, so the two states are visually distinct.
- Control-group badges: every selected unit that belongs to a group draws its lowest group number above the model, interpolated with the entity like the rings. The digit sits on an opaque dark chip with a 1 px cyan border - a bare glyph is unreadable over the bright unit meshes. Badge work is collected first, then the chips render through the shared SpriteBatch pixel and the text through `ScreenTextRenderer`.
- Order markers: `OrderMarkerSystem` keeps short-lived ground rings for accepted orders (0.65 s). Radius grows 0.7 → 2.0 and alpha fades 0.8 → 0; expired markers are removed during update. Move/rally markers are cyan, attack targets red, attack-move destinations green. Markers are only created when `OrderController` accepted at least one order, so invalid clicks leave no visual noise.
- `Ctrl+1…9` snapshots the current selection into a control group; `1…9` recalls surviving members (empty groups clear the selection). `Backspace` focuses the player Nexus, `Space` the primary selection via `RtsCamera.FocusOn`.
- Selection input is ignored over the HUD and outside the client area.

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
- **Spatial grid:** randomized radius queries match brute force, nearest-enemy ignores allies and range, rebuild drops despawned entities, span queries report total matches even with a small buffer, map-edge queries stay in bounds, and 1,000 radius queries over 300 units complete in ~4 ms (budget 200 ms in the test).
- **Navigation:** nav-grid blocking/count/footprints and line-of-sight (including diagonal corner rejection); A* straights, diagonals, walls, blocked endpoints, corner traps, smoothing, world-space output, and building footprints blocking/unblocking in `World.Navigation`; 25 random obstacle maps compare path cost against reference Dijkstra within 1.3×.
- **Movement:** arrival within tolerance and stop, facing toward velocity, wall detours, mid-route blocking with successful repath, rejection of blocked destinations/buildings, stop semantics, previous-position interpolation, five-unit anti-stacking (no pair closer than 1 unit) and deterministic movement checksums.
- **Orders:** tick-start application, queue completion, sequential queued moves, replace semantics, capacity 4, stop/hold position retention, invalid targets/points/types/buildings, dead-target rejection and mid-queue replacement.
- **Harvesting:** one full cycle (cargo 22, reserve decrement, deposit), phase timings (2.2 s / 0.6 s), steady-state cadence within 5 % over 8 cycles, reserve-limited loads, depletion removal + order completion, empty-cargo return resuming mining, unreachable node aborting cleanly, node nav blocking/freeing, rejection for non-Harvesters, and deterministic checksums across runs.
- **Order controller:** context move/attack/rally/harvest/deposit, attack-move arming, stop/hold, shift queueing, no-selection no-ops, structure rally persistence, node context sending only Harvesters and a full cross-map move integration test.
- **Picking:** ground point from a known camera, center-ray unit hit, misses, closest-unit selection, unit-over-structure priority, working at 20/45/80 zoom, off-center projection→ground round-trips, off-center entity hits, dead entities ignored, empty world, and invalid viewport rejection.
- **Selection:** click replace, shift-add, structure-only click, unit-preferred primary, units-only box selection, empty-box clearing, dead pruning, control-group assign/recall, groups surviving deaths, empty-group clearing, invalid group rejection and lowest-group lookup for badges.
- **Order markers:** lifetime expiry, individually ageing markers, zero-delta retention and bulk expiry leaving no residue.
- **Determinism test:** run the same seed + command script twice and compare a world checksum/snapshot.
- **Timing tests:** `SimulationClock` covers 20 Hz accumulation across 60/20 FPS frame patterns, partial ticks and alpha, invalid elapsed input, and the 5-step catch-up clamp (excess time dropped, not banked).
- **Rendering math:** `CameraView` tests cover frustum visibility (origin, behind camera, outside FOV, beyond far plane, near-plane intersection), world-matrix bounds culling, `LookAt` changes, aspect/projection and invalid input.
- **RTS camera:** 15 tests cover defaults, distance/pitch, camera-relative panning, distance-scaled pan speed, map clamping, focus flattening, 45° yaw requests with smooth easing/wrapping, zoom clamping/smoothing, world-units-per-pixel, invalid bounds and viewport.
- **Mesh cache:** equal keys reuse one mesh, different keys create separate meshes, disposal releases every cached mesh exactly once, access after dispose throws, and the key includes every shape parameter with neutral defaults.
- **Definitions:** default content counts and key balance values match the design doc; duplicate ids, missing weapon/prerequisite/produced-unit references, self-requirements, negative costs, zero health/range/cooldown/footprint, blank names and unknown id lookups all fail with messages naming the offending field.
- **Map grid:** `MapGrid` tests cover map/cell constants, corner/center/edge conversion, cell-center round-trips and inside-map checks; the ground UV scale uses the same 2-unit cell constant, so the texture grid and build placement cannot drift.
- **Model demo (manual):** every entity model appears with a distinct silhouette and correct palette; accents stay colored (not white); shadows sit under models; debug stats report entity count (currently 18).
- **Manual smoke checklist** for rendering/UI tasks: window opens, camera pans/zooms, models visible with correct colors, no exceptions in log, 60 FPS.
- **Smoke test:** `dotnet run --project RealTimeStrategy -- --smoke-test 30` creates the window/graphics device, renders 30 frames and exits with code 0. Add `--overlay` to force the debug overlay visible, `--build-grid` to show the build grid, `--select-all` to preselect the player's units and put them in group 1 (useful for selection/badge screenshots), and `--screenshot <path.png>` to save the backbuffer of the final frame - use this for visual checks instead of OS window capture.
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
