# TASKS — Vector Front Roadmap

Single source of truth for progress. Read `AGENTS.md` before starting a task.

**Status markers**

| Mark | Meaning |
| --- | --- |
| `[ ]` | Not started |
| `[~]` | In progress (at most one at a time) |
| `[x]` | Finished **and explicitly confirmed by the developer** |

**Rules**

- Work top-down. Take the first `[ ]` task, or resume the single `[~]` task. The developer may reorder or override at any time.
- A task is never marked `[x]` without explicit confirmation (see `AGENTS.md`, step 5–6).
- Task IDs are stable. Do not renumber; append new tasks with the next free ID.
- If a task turns out to be too large, split it into new tasks in place and note the split in the original.
- Every task updates the relevant `Design/` docs when behavior, numbers, controls, structure or dependencies change.
- Acceptance criteria are the minimum bar. "Builds and runs" alone is not acceptance.

---

## M0 — Foundation

Goal: a window with a lit 3D scene, a working camera and a testable build.

- [x] **T-001 — Solution scaffold**
  - Create `RealTimeStrategy.slnx`, `RealTimeStrategy/RealTimeStrategy.csproj` (net8.0, MonoGame DesktopGL 3.8.6.1, DigitalRiseModel.MonoGame 0.7.6, Myra 1.6.8) and `RealTimeStrategy.Tests` (xUnit, project reference).
  - Add `.gitignore`, `Assets/` subfolders, and a smoke test proving the test project runs.
  - Acceptance: `dotnet build RealTimeStrategy.slnx` and `dotnet test RealTimeStrategy.Tests` succeed from a clean clone; window opens and exits cleanly.

- [x] **T-002 — Application shell and fixed-step loop**
  - `VectorFrontGame` with `GraphicsDeviceManager`, 1600×900 window, VSync, Esc quit.
  - Fixed 20 Hz simulation accumulator + render interpolation hooks; `GameTime` never leaks into `Core`.
  - Myra bootstrap (`MyraEnvironment`, asset manager mirroring `Presentation/Ui`), debug panel authored in `Presentation/Ui/DebugOverlay.xmmp` and exported with `myra-export`; `F1` toggles it. Runtime behavior lives in the hand-written `DebugOverlayPanel.cs` partial.
  - Overlay shows FPS, frame ms, simulation rate, tick count and interpolation alpha; camera position arrives with T-006, entity count with T-009.
  - `--overlay` shows the overlay at startup; `--screenshot <file>` saves the final smoke-test frame for visual verification.
  - Acceptance: fixed-step tests (20 ticks per simulated second at 60 and 20 FPS, catch-up clamp); overlay toggles; label text refreshed at 4 Hz, not per frame.

- [x] **T-003 — 3D rendering foundation**
  - Depth buffer, `BasicEffect` lighting (ambient + directional), camera matrices, frustum culling, draw-list API.
  - Render a ground plane and a reference box; verify winding, normals and depth.
  - Acceptance: no z-fighting at default camera, stable 60 FPS, unit test for frustum/bounds math.

- [x] **T-004 — Procedural mesh library**
  - `MeshLibrary` wrapping `MeshPrimitives` with a parameter-based cache (box, cylinder, sphere, cone, capsule, torus, disc, plane).
  - Dispose GPU resources on shutdown; replace the direct `MeshPrimitives` calls in the demo scene with cached lookups.
  - Acceptance: repeated requests share one buffer; unit tests for cache keys and disposal; no leak after restart.

- [x] **T-005 — Model factory and palette**
  - `ModelFactory` assembles named models from primitive parts with local transforms and materials (Harvester, Scout, Striker, Lancer, Nexus, Reactor, Fabricator, Foundry, Turret, alloy node, projectile, debris); parts reference cached `MeshLibrary` meshes and never own them; dimensions start as local constants and are bound to definitions in T-008.
  - Faction palettes (Concord cyan / Null magenta) with emissive accents.
  - Acceptance: every model is visible, distinguishable at default zoom, and documented in `Design/GameDesign.md` if silhouettes change.

- [x] **T-006 — RTS camera**
  - Focus point + yaw/pitch/distance; arrow keys/edge pan, MMB drag, wheel zoom (20–80), Q/E 45° yaw steps, map clamping.
  - Drive the existing `CameraView` (replace the fixed reference camera in `VectorFrontGame`).
  - Expose `FocusOn(point)` for entity-bound keys, wired in T-015.
  - Add camera focus coordinates to the debug overlay panel (T-002).
  - Acceptance: camera math unit tests (clamping, yaw wrap, pan scaling); no gimbal flip; smooth zoom.

- [x] **T-007 — Ground, build grid and blob shadows**
  - Generated grid texture, map border, build-grid overlay toggle, fake shadows under entities; replaced the plain reference plane from T-003.
  - Acceptance: grid aligns exactly with the 2 × 2 unit grid used by placement; shadows sit under every demo entity and interpolate with entity transforms; F1 shows the camera's grid cell.

---

## M1 — Simulation core

Goal: deterministic headless world with movement and orders.

- [x] **T-008 — Definitions and balance data**
  - Immutable `UnitDef`, `BuildingDef`, `WeaponDef`, `FactionDef` records + registry; values from `Design/GameDesign.md`.
  - Validation tests (positive costs, ranges, footprints, required defs exist).
  - Acceptance: all MVP units/buildings defined; invalid definitions fail fast with clear messages.

- [x] **T-009 — World, entities and players**
  - `World` with seeded RNG, `DefinitionRegistry`, `PlayerState` (alloy, energy, supply, ids), `Entity` list + lookup, spawn/despawn, deferred removal, `Step(dt)`; use `MapGrid.MapSize`/`MapGrid.CellSize` for bounds. Every entity references its `UnitDef`/`BuildingDef` for stats.
  - Commands queue applied at tick start; world snapshot/checksum helper for tests.
  - Entity rendering: each entity holds its cached `DrModel` and palette; the renderer submits `DrModel` at the interpolated entity transform.
  - Add the entity count to the debug overlay panel (T-002).
  - Acceptance: spawn/despawn/checksum tests; same seed + commands = identical checksum.

- [x] **T-010 — Spatial grid and queries**
  - Uniform grid for entity range queries, rebuilt or updated incrementally per tick.
  - APIs for radius query, nearest enemy, neighbors for separation.
  - Acceptance: results match brute force on randomized cases; 300-unit query benchmark stays under budget.

- [x] **T-011 — Navigation grid and A***
  - `NavGrid` (60 × 60 cells, same 2-unit `MapGrid` cells but separate from `SpatialGrid`), blockers from structures/nodes/map edges, 8-way A* without corner cutting, line-of-sight path smoothing.
  - Acceptance: path found/blocked cases, no diagonal wall clipping, path length ≤ 1.3× optimal on test maps.

- [x] **T-012 — Movement and steering**
  - Waypoint following with world paths from `PathFinder`, acceleration/deceleration, separation via `World.Spatial` neighbor queries, stuck detection and repath, facing/rotation toward velocity.
  - Acceptance: units reach destinations within tolerance, do not pile on one point, recover from blocked paths in tests.

- [x] **T-013 — Orders and command queue**
  - `Order` value types (`Move`, `AttackMove`, `Attack`, `Hold`, `Stop`, `Harvest`, `ReturnCargo`, `Build`, `Produce`, `SetRally`), queue limits, validation, `OrderExecutor`; issue them as `WorldCommand`s applied by the existing tick-start queue rather than adding a second command path. Move/Stop delegate to `World.MoveUnit`/`World.StopUnit`; attack/hold will consume the same `MoveState`.
  - Acceptance: queue transitions, replacement rules and invalid orders covered by tests; units hold last position on `Stop`.

---

## M2 — Selection and command issuing

Goal: the player can select and command units with the mouse.

- [x] **T-014 — Picking and hover**
  - Screen ray → ground plane, entity hit test with bounding radius, units prioritized over structures, hover highlight.
  - Acceptance: picking tests with known camera setups; hover works at all zoom levels; input routing leaves room for HUD exclusion in T-034.

- [ ] **T-015 — Selection and control groups**
  - Click select via `Picking.PickEntity`, drag box (shift adds), structures vs. units rules, `Ctrl+1…9` assignment and recall, `Backspace` focuses the Nexus and `Space` the selection via `RtsCamera.FocusOn`.
  - Acceptance: selection tests (empty box, mixed selection, dead entities pruned); control groups survive deaths.

- [ ] **T-016 — Order issuing**
  - Right-click context orders issued through `World.TryIssueOrder` (move, attack, harvest, deposit, rally), with world points from `Picking.TryGetGroundPoint`; `A` + click attack-move, `S` stop, `H` hold, contextual cursor feedback.
  - Acceptance: each order reaches the correct entity type (rally is stored for T-024); invalid targets ignored without deselecting; integration test drives a unit across the map.

- [ ] **T-017 — Selection and order visuals**
  - Selection rings, group indicators, move/attack markers, invalid-placement flash.
  - Acceptance: markers fade and clean up; rings interpolate with units; no visual leftovers after death.

---

## M3 — Economy

Goal: harvesting loop that the player can see and interrupt.

- [ ] **T-018 — Alloy nodes and harvesting**
  - Alloy node entities with reserves (add a `ResourceNode` kind to the world entity model); nodes block their `NavGrid` cells; extend `OrderExecutor` to accept `Harvest`/`ReturnCargo`; harvester state machine (seek → harvest → return → deposit), carry capacity, timings from the design doc.
  - Acceptance: one Harvester produces the documented rate ±5 % in simulation; tests for full/empty cargo, dead node, blocked path.

- [ ] **T-019 — Resources, deposits and supply**
  - Nexus drop-off, player resources, supply used/cap, spend/refund helpers, read-only HUD snapshot.
  - Acceptance: arithmetic and cap tests; spending is atomic; refused purchase leaves state unchanged.

- [ ] **T-020 — Reactors, energy and supply**
  - Passive energy generation per Reactor, energy cap, energy costs for advanced units.
  - Acceptance: generation matches design rates; destroying a Reactor stops income and frees supply; tests for cap clamping.

- [ ] **T-021 — Node depletion and reassignment**
  - Depleted nodes disappear; idle Harvesters pick the nearest available node (player-owned order preferred).
  - Acceptance: multi-harvester distribution tests; no harvester stuck on a dead node; empty map does not throw.

---

## M4 — Building and production

Goal: place structures, build an army, expand production.

- [ ] **T-022 — Placement mode and ghost preview**
  - Footprint snapping to `MapGrid` 2-unit cells using `BuildingDef.Footprint`, build-radius check, overlap/blocker check, ghost tint, rotation-free placement, cancel; temporary debug hotkey enters placement mode.
  - Acceptance: validity uses `World.Navigation` cells and footprint rules exactly; test matrix for overlap, radius, nodes, map edge; ghost never blocks input after cancel.

- [ ] **T-023 — Construction progress, cancel and refund**
  - Cost charged on confirm, progress over build time, 50 % refund on cancel, completion swaps to the active model, destruction removes footprint.
  - Acceptance: refund/cost tests, nav-grid updates on completion/destruction, no negative resources.

- [ ] **T-024 — Production queues and rally points**
  - Per-structure queues, cost/supply checks at enqueue, cancel with refund, spawn at exit point, rally move order; temporary debug hotkey queues each producible unit. Costs, build times and allowed units come from `BuildingDef` / `UnitDef` / `DefinitionRegistry`.
  - Acceptance: queue order, blocking on supply, refund on cancel, rally point applied to newly spawned units; tests.

- [ ] **T-025 — Temporary command bridge**
  - One documented hotkey map that reaches every build/produce action, replacing the ad-hoc debug keys; shown in the debug overlay. Removed in T-037.
  - Acceptance: full build/produce loop playable before the HUD exists; hotkey map matches the command card layout.

- [ ] **T-026 — Requirements and unlocks**
  - Reactor → Fabricator → Foundry/Turret gating, disabled reasons exposed to the UI layer.
  - Acceptance: requirement matrix tests; destroying a prerequisite does not delete existing structures but blocks new ones.

---

## M5 — Combat

Goal: readable, testable combat that decides the match.

- [ ] **T-027 — Weapons and damage**
  - Weapon state (range, cadence, projectile speed, spread), damage formula with armor classes and multipliers, overkill protection; all values read from `WeaponDef`.
  - Acceptance: table-driven damage tests from `GameDesign.md`; minimum damage 1; no damage from dead attackers.

- [ ] **T-028 — Targeting and auto-acquire**
  - Acquisition range, target scoring (nearest, in range) via `World.Spatial.FindNearestEnemy`, leash return to previous order, no target through structures for units (unless attacking the structure).
  - Acceptance: acquire/leash/reacquire tests; idle units defend themselves; ordered attack persists until target dies.

- [ ] **T-029 — Projectiles and impacts**
  - Travel-time projectiles, impact resolution, homing vs. ballistic behavior per weapon, cleanup.
  - Acceptance: projectile pool does not leak; damage lands at impact; visual impact event emitted for the renderer.

- [ ] **T-030 — Death, removal and effects**
  - Death state, deferred removal, supply/resource bookkeeping, debris/explosion events.
  - Acceptance: no references to dead entities survive a tick; selection/production references cleaned up; tests for simultaneous kills.

- [ ] **T-031 — Turrets and combat orders**
  - Turret auto-fire, `AttackMove` (engage then continue), `Hold` (no chase), `Stop`, chase leash; `AttackMove` and `Attack` orders complete when combat resolves.
  - Acceptance: behavior tests for each stance; turrets never move; attack-move reaches destination after clearing enemies.

- [ ] **T-032 — Damage feedback and health bars**
  - Health bars above damaged/selected entities, hit flash, structure damage states, hover tooltip data.
  - Acceptance: bars billboard correctly at all camera yaws/zooms; hidden at full health unless selected; no UI overlap at default view.

- [ ] **T-033 — Match state and win/lose**
  - Match phases (playing/paused/finished), Nexus-loss detection from `World.Entities` for both sides, result payload, restart.
  - Acceptance: tests for simultaneous Nexus destruction, no Nexus on start (fail fast), restart returns clean state.

---

## M6 — HUD and UX

Goal: a complete, understandable interface.

- [ ] **T-034 — HUD layout, theme and input routing**
  - Keep the shared `GameUi` desktop; add the full-screen HUD structure (top bar, bottom panels), a Myra stylesheet/theme, and relocate the controls legend into a help panel or keep it unobtrusive.
  - Implement full UI input routing: replace the HUD-rectangle check with `Desktop` hit-testing; clicks over any HUD widget must not reach world picking/orders; modal/focus rules; edge-pan and drag suppressed over UI where appropriate.
  - Acceptance: HUD + debug overlay coexist; a test label renders over the 3D view; clicks on HUD never select or issue orders; keyboard shortcuts remain game-side; empty HUD consumes no input; no MGCB dependency.

- [ ] **T-035 — Top resource bar**
  - Alloy, energy current/cap, supply used/cap, match timer with icons/glyphs.
  - Acceptance: values update from world snapshot only; supply-blocked and low-energy states are visually distinct.

- [ ] **T-036 — Selection and production panel**
  - Primary selection name, HP/armor, current order, production queue with progress and cancel buttons.
  - Acceptance: multi-selection shows a sensible summary; panel clears on empty selection; queue cancel refunds correctly.

- [ ] **T-037 — Command card**
  - 3 × 3 contextual grid for build/produce/stop/hold/attack-move, tooltips with cost and disabled reasons, keyboard shortcut labels.
  - Acceptance: every action reachable with mouse and hotkey; disabled reasons match T-026 rules; replaces T-025 bridge.

- [ ] **T-038 — Minimap**
  - Terrain, structures, units, camera viewport rectangle using `MapGrid`; left-click jump, right-click move order; click-drag pan.
  - Acceptance: minimap coordinates match world picking; updates at 60 FPS without allocation spikes; respects map bounds.

- [ ] **T-039 — Notifications and tooltips**
  - Transient messages (insufficient funds, supply blocked, construction complete, under attack, Nexus lost) with cooldown/dedup.
  - Acceptance: no message spam from repeated clicks; messages readable at 1080p; under-attack triggers once per engagement window.

- [ ] **T-040 — Pause and end screens**
  - Pause overlay, victory/defeat screen with match time and unit summary, restart button, keyboard shortcuts.
  - Acceptance: simulation frozen while paused; restart fully resets world, camera and UI; Esc does not quit mid-match without confirmation; restart safely recreates/reuses GPU resources (mesh library + cached DrModels share buffers — no double-dispose or dangling buffers).

---

## M7 — AI opponent

Goal: a credible opponent that uses the same rules.

- [ ] **T-041 — AI skeleton and opening build order**
  - AI player state machine, deterministic decision cadence, scripted opening (Harvesters, Reactor, Fabricator, Turret).
  - Acceptance: AI never issues illegal commands; opening completes by the documented time in a headless test.

- [ ] **T-042 — AI economy management**
  - Harvester count targets per node, replacement of losses, sustained harvesting of the second cluster near the base.
  - Acceptance: AI income stays within 10 % of an equivalent scripted human build; no idle Harvesters after 2 minutes.

- [ ] **T-043 — AI production and composition**
  - Army production by phase, composition mix (Striker/Lancer), supply-aware queuing, Foundry timing.
  - Acceptance: AI reaches documented army size by phase; no queued units stuck forever; deterministic across runs.

- [ ] **T-044 — AI attacks and defense**
  - Wave scheduling, rally/attack-move to player structures, retreat threshold, home defense reaction.
  - Acceptance: first wave arrives in the documented window; AI defends Nexus under attack; no unit ping-pong.

- [ ] **T-045 — Difficulty and AI regression tests**
  - Easy/Normal/Hard modifiers (wave size, cadence, build speed), headless full-match simulation test.
  - Acceptance: correct win/lose across seeds; AI does not stall; difficulty differences measurable.

---

## M8 — Polish, balance and release

Goal: prototype that can be handed to a player.

- [ ] **T-046 — Visual polish pass**
  - Faction accent glow, terrain detail, impact/death effects, camera shake on heavy impacts, consistent silhouette scale.
  - Acceptance: visual checklist in `Design/GameDesign.md`; 60 FPS retained at 300 entities.

- [ ] **T-047 — Audio placeholders**
  - UI clicks, weapon fire, impacts, build complete, under attack; volume settings.
  - Acceptance: no audio-related frame hitches; sounds can be muted; missing files never crash.

- [ ] **T-048 — Performance pass**
  - Profile tick and render, remove allocations in hot paths, batch draws, cache lookups.
  - Acceptance: budget table in `Design/TechnicalArchitecture.md` met; before/after numbers recorded.

- [ ] **T-049 — Playtest and balance**
  - Play 5 matches (or observe AI vs AI), tune economy/combat numbers, record changes in `Design/GameDesign.md`.
  - Acceptance: matches last 10–15 minutes; no dominant unit; no resource dead-end.

- [ ] **T-050 — Documentation and release build**
  - Verify README, `Design/`, TASKS and AGENTS match reality; publish a self-contained win-x64 build.
  - Acceptance: clean clone builds and runs; release archive contains assets; docs have no stale claims.

- [ ] **T-051 — Retrospective and next steps**
  - List what worked, what to cut, and candidate post-prototype features; rewrite the follow-up roadmap.
  - Acceptance: developer-confirmed retrospective added to `Design/`; TASKS.md reorganized for the next phase.

---

## Done log

Filled in by the agent **after** developer confirmation (see `AGENTS.md`).

| Task | Title | Date | Commit |
| --- | --- | --- | --- |
| T-001 | Solution scaffold | 2026-11-20 | 7f86ca2 |
| T-002 | Application shell and fixed-step loop | 2026-11-20 | 3801db7 |
| T-003 | 3D rendering foundation | 2026-11-20 | 673c8ad |
| T-004 | Procedural mesh library | 2026-11-20 | c6a4323 |
| T-005 | Model factory and palette | 2026-11-20 | 5dd25b3 |
| T-006 | RTS camera, smooth rotation and controls HUD | 2026-11-20 | 5e75f29 |
| T-007 | Ground, build grid and blob shadows | 2026-11-20 | 38ce3cf |
| T-008 | Definitions and balance data | 2026-11-20 | b4cdaae |
| T-009 | World, entities and players | 2026-11-20 | 6130721 |
| T-010 | Spatial grid and queries | 2026-11-20 | 0ce671d |
| T-011 | Navigation grid and A* | 2026-11-20 | f07ee62 |
| T-012 | Movement and steering | 2026-11-20 | 0541a35 |
| T-013 | Orders and command queue | 2026-11-20 | 1d3f056 |
| T-014 | Picking and hover | 2026-11-20 | _pending_ |
