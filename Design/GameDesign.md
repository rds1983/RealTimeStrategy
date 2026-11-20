# Vector Front — Game Design

A futuristic real-time strategy prototype for one player against a scripted AI.

**Status:** pre-implementation design. Once a system exists, the code is authoritative for numbers; if balance or behavior changes intentionally, update this document in the same task.

---

## 1. Vision

Wireframe-era clarity with modern sci-fi presentation. The player commands a small expeditionary force on a hostile plain: expand the alloy economy, place a handful of modular structures, field a compact army of geometric war machines and break the enemy Nexus.

**Pillars**

| Pillar | Meaning for decisions |
| --- | --- |
| Readable at a glance | Bold silhouettes, faction-colored emissive accents, clear health bars, no visual noise. |
| Short, decisive match | A skirmish resolves in 10–15 minutes; every unit and structure has a purpose. |
| Economy enables aggression | Harvesting is simple to learn, but node placement forces map control. |
| Prototype over content | Few systems, deeply tested; no campaign, no tech-tree sprawl. |

**Non-goals for the prototype:** campaign, multiplayer, save/load, fog of war, voice acting, imported art assets.

### Model silhouettes

Every model is assembled at runtime from boxes, cylinders, cones, spheres and a torus. The goal is a readable silhouette at RTS zoom, not detail.

| Model | Silhouette cues |
| --- | --- |
| Harvester | Low cargo chassis, two side hover pods, front scoop, glowing engine dots. |
| Scout | Slim dart fuselage, swept wings, twin rear engines, tail fin. |
| Striker | Biped: box torso, shoulder pads, two forward guns, bright visor. |
| Lancer | Heavy biped: oversized shoulder cannon, thick legs, back antenna. |
| Nexus | Hex platform, ringed glowing core, tall spire, four pylons with glowing tips. |
| Reactor | Cylindrical core, glowing torus, three cooling fins, cap cone. |
| Fabricator | Wide plant, gantry + crane hook, side tanks, glowing front vent. |
| Foundry | Blocky furnace, twin chimneys with glowing rings, bright furnace door. |
| Turret | Round base, rotating head, twin barrels with glowing muzzles, sensor orb. |
| Alloy node | Cluster of glowing crystals on grey rocks. |
| Projectile | Small glowing sphere with a short tail. |
| Debris | A few small tumbling panels. |

Accent parts use an emissive material: a dark base color plus the faction accent, so they read as lit energy rather than white highlights.

---

## 2. Setting

Two machine collectives contest a derelict world for its alloy crust.

- **Player faction — Concord:** white/blue-grey chassis, cyan energy accents, angular "architect" silhouettes.
- **AI faction — Null Swarm:** charcoal chassis, magenta/red accents, spikier silhouettes.

Both factions are mechanically mirrored in the prototype; only palette and naming differ. Asymmetry is post-prototype work.

---

## 3. Core loop

1. **Harvest** — Harvesters carry alloy from nodes back to the Nexus.
2. **Power** — Reactors generate energy and raise the supply cap.
3. **Build** — Spend alloy on structures that unlock production and defense.
4. **Produce** — Fabricators (and a Foundry for heavy units) build the army.
5. **Fight** — Take map control, destroy the enemy Nexus before it destroys yours.

Pressure comes from contested alloy nodes in the middle of the map and from AI attack waves that grow over time.

---

## 4. Match rules

| Rule | Value |
| --- | --- |
| Map | 120 × 120 units, flat terrain, two bases on opposite corners |
| Start | Nexus, 3 Harvesters, 2 Strikers, 500 alloy, 100 energy |
| Victory | Enemy has no Nexus left |
| Defeat | Player has no Nexus left |
| Pause | `P` freezes simulation; UI stays interactive |
| Restart | From the victory/defeat screen |

---

## 5. Economy

### Resources

| Resource | Gathered by | Used for | Cap |
| --- | --- | --- | --- |
| **Alloy** | Harvesters from alloy nodes, deposited at a Nexus | Every structure and unit | 2 000 |
| **Energy** | Reactors, passive trickle | Advanced units (Lancer) | 300 |
| **Supply** | Nexus (+10), Reactor (+5) | Unit population | 100 |

Energy is a slow, capped trickle rather than a harvestable resource: it rewards keeping reactors alive, but can never be banked indefinitely.

### Harvesting

| Parameter | Value |
| --- | --- |
| Node reserve | 1 800 alloy |
| Harvester capacity | 22 alloy per trip |
| Harvest time | 2.2 s |
| Deposit time | 0.6 s |
| Harvester speed | 5.0 u/s |
| Typical income | 22 alloy per (2.2 s + 0.6 s + round-trip travel) |

Harvesters are unarmed and fragile; raiding the enemy harvest line is a valid strategy.

Behaviour:
- Right-clicking a node assigns every selected Harvester to it; they walk to the nearest free cell beside the node, mine a full load, carry it to the nearest owned drop-off (the Nexus), unload, and walk back. The loop repeats until the node is empty.
- Right-clicking the Nexus sends selected Harvesters to unload immediately and then resume their node.
- A node occupies one 2 × 2 build cell (`NavGrid`) and blocks movement; it is removed when its reserve reaches zero, and any harvester already loaded still finishes its delivery.
- Nodes are context targets only — left-clicking one never selects it.

Income is distance-bound: at 5.0 u/s a node roughly 30 units from the Nexus yields about 90 alloy/min, while a 75-unit haul is needed to reach the earlier ~40 alloy/min figure. The per-trip numbers above are authoritative; if the economy proves too hot once spending exists, retune capacity or harvest time rather than travel speed.

### Supply

- Each unit costs supply; production is blocked (not cancelled) when the cap is reached.
- The cap is raised only by structures, so army size is tied to base investment.
- Supply is freed when a unit dies; structures keep their supply contribution while alive.

---

## 6. Units

All units are built from the matching producer. Costs are alloy unless stated.

| Unit | Role | Cost | Build | Supply | HP | Armor class | Armor | Speed | Weapon | Range | Cadence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Harvester** | Economy | 50 | 12 s | 1 | 90 | Light | 0 | 5.0 | — | — | — |
| **Scout** | Harass, vision, chase | 40 | 10 s | 1 | 70 | Light | 0 | 9.0 | Kinetic 6 | 9 | 0.8 s |
| **Striker** | Line infantry mech | 80 | 16 s | 2 | 130 | Armored | 2 | 5.5 | Kinetic 11 | 8 | 0.9 s |
| **Lancer** | Anti-armor walker | 110 + 40 energy | 22 s | 3 | 170 | Armored | 4 | 4.2 | Plasma 34 | 11 | 2.4 s |

Notes:

- **Harvester** — automatically returns to harvesting after depositing. Right-clicking a node assigns it; right-clicking the Nexus returns it.
- **Scout** — cheap, fast, low damage; the only unit that outruns Strikers. Good for scouting and picking off Harvesters.
- **Striker** — backbone. Reasonable against everything, best against light targets.
- **Lancer** — slow, expensive, melts structures and armored units; needs escorts.

---

## 7. Structures

| Structure | Role | Cost | Build | HP | Armor | Supply | Requires | Produces / effect |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Nexus** | Headquarters, drop-off | 400 | 45 s | 1 600 | 5 | +10 | — | Harvester; alloy drop-off |
| **Reactor** | Power and population | 100 | 20 s | 600 | 3 | +5 | — | +0.7 energy/s |
| **Fabricator** | Unit production | 150 | 25 s | 900 | 3 | — | Reactor | Scout, Striker |
| **Foundry** | Heavy production | 250 | 35 s | 1 000 | 3 | — | Fabricator | Lancer |
| **Turret** | Static defense | 120 | 20 s | 550 | 4 | — | Fabricator | Plasma 16, range 13, 1.2 s |

Placement rules:

- Structures are placed on the build grid (cell = 2 × 2 units); footprints are 1×1 (Turret), 2×2 (Reactor) or 3×3 cells (Nexus, Fabricator, Foundry).
- A structure must not overlap terrain blockers, another structure or a resource node, and must be within the player's build radius (12 units of an owned structure).
- Cost is paid when placement is confirmed; construction then progresses automatically (no builder unit in the prototype).
- Cancelling an unfinished structure refunds 50 % of its cost. Destroyed structures refund nothing.
- A second Nexus can be built (expansion) but is not required for the MVP AI.

---

## 8. Combat model

### Damage

```
damage = max(1, weaponDamage × classMultiplier − targetArmor)
```

| Weapon type | vs Light | vs Armored | vs Structure |
| --- | --- | --- | --- |
| Kinetic | ×1.0 | ×0.75 | ×0.5 |
| Plasma | ×0.8 | ×1.25 | ×1.0 |

- Every weapon has range, cadence, projectile speed and a small spread.
- Projectiles travel and can miss a target that died or moved out of the impact area (visual only; damage is resolved on impact against the current target position).
- Units flash on hit; structures show a damage state.

### Behavior

- **Auto-acquire:** idle or moving units engage enemies inside `range + 2`; they return to their previous order when the target dies or leaves leash range (`range + 8`).
- **Attack-move:** move toward a point, stopping to engage anything encountered.
- **Attack:** chase a specific target until it dies or leaves a generous leash.
- **Hold:** no chasing; fire only inside weapon range.
- **Structures** do not chase and cannot be given orders.
- Line of sight is not modeled in the prototype; only weapon range and target validity matter.

### Presentation

- Health bars appear above damaged or selected entities.
- Impacts spawn a short-lived emissive flash and a fading ring.
- Deaths spawn debris primitives that fade out; no persistent corpses.

---

## 9. Camera and controls

Camera: perspective, 45° field of view, pitch 55°, default yaw 45°, distance 20–80 units (default 45) with smooth wheel zoom. Pan with arrow keys, edge scroll or middle-mouse drag; `A`/`S`/`H` are reserved for combat commands, so WASD is not used for panning.

| Input | Action |
| --- | --- |
| `↑ ↓ ← →` / edge scroll | Pan |
| Middle mouse drag | Pan |
| Mouse wheel | Zoom |
| `Q` / `E` | Rotate camera 45° (smooth) |
| `G` | Toggle build-grid overlay |
| Left click | Select entity / confirm placement |
| Left drag | Box select |
| `Shift` + click / drag | Add to selection |
| `Shift` + right click | Queue order instead of replacing |
| Right click | Context order (move, attack, harvest, deposit, set rally) |
| `A` then left click | Attack-move |
| `S` | Stop |
| `H` | Hold position |
| `Backspace` | Center camera on Nexus |
| `Space` | Center camera on selection |
| `Ctrl` + `1…9` | Assign control group |
| `1…9` | Recall control group |
| `Esc` | Cancel placement / clear selection / close panel |
| `P` | Pause |
| `F1` | Toggle debug overlay (FPS, sim tick, entity count) |

Selection rules:

- Clicking a unit selects only units; clicking a structure selects that structure. Resource nodes are never selected — right-clicking one orders Harvesters to mine it.
- A drag box selects the player's units inside it (units take priority over structures).
- The selection panel and command card show the "primary" selection (first unit, or the structure).

---

## 10. HUD

```
+--------------------------------------------------------------+
| Alloy 620   Energy 140/300   Supply 8/15        [notifications] |
+--------------------------------------------------------------+
|                                                              |
|                    3D viewport                               |
|                                                              |
| +-------------+  +--------------------------+  +-----------+ |
| |  Minimap    |  | Selection / production   |  | Command   | |
| |             |  | panel                    |  | card 3x3  | |
| +-------------+  +--------------------------+  +-----------+ |
+--------------------------------------------------------------+
```

- **Top bar** — alloy, energy (current/cap), supply (used/cap), match timer.
- **Minimap** — terrain, structures, units (faction colors), camera viewport rectangle; left click jumps, right click issues a move order.
- **Selection panel** — name, portrait glyph, HP/armor, current order, and production queue with progress for structures.
- **Command card** — contextual 3×3 grid of build/produce/stop/attack-move buttons; disabled buttons show why (cost, supply, requirement, queue full).
- **Notifications** — "Not enough alloy", "Supply blocked", "Construction complete", "Under attack", "Nexus lost".
- **End screen** — VICTORY / DEFEAT with match time, units built/lost summary and Restart.

**Current prototype:** the HUD shows an always-visible controls legend in the top-right corner (pan/zoom/rotate/grid/debug/quit) plus the optional `F1` debug panel. The ground uses a subtle grid aligned to the 2 × 2 unit build cells; `G` overlays the brighter build grid. Resource bar, minimap, selection panel and command card are added with the systems they represent; they must not be faked earlier.

Selected units in a control group wear their group number on a small dark chip above the model. Accepted orders drop a short-lived ground ring (cyan = move/rally, red = attack, green = attack-move) that expands and fades in under a second, so repeated clicks never accumulate clutter.

---

## 11. AI opponent

The AI plays the same rules as the player (no cheating resources) and is intentionally readable.

| Phase | Time | Behavior |
| --- | --- | --- |
| Open | 0:00–3:00 | 4 Harvesters, Reactor, Fabricator, continuous Harvester production |
| Expand | 3:00–6:00 | Second Reactor, Turret, Foundry; saturate the second node cluster near its base |
| Pressure | 6:00+ | Army of Strikers/Lancers, attack waves every 60–90 s at the player's forward structures |
| Reaction | always | Pulls units home when the Nexus or a Reactor is attacked |

- First wave is small (3–4 units), later waves scale with AI income.
- Difficulty (Easy/Normal/Hard) changes wave size, delay and build speed multiplier only.
- AI state is a small scripted state machine, not a planner.

---

## 12. MVP scope

**In**

- 1 map, 1 faction per side (mirror), 1 AI opponent
- Alloy harvesting, energy trickle, supply
- 5 structures, 4 units
- Selection, box select, control groups, move/attack/attack-move/hold/stop
- Construction placement, production queues, rally points
- Combat with two damage types, health bars, projectiles, death FX
- Myra HUD, minimap, notifications, pause, victory/defeat
- xUnit coverage of simulation rules

**Out (post-prototype)**

- Fog of war, line of sight, terrain height
- Tech tree beyond the Foundry, unit abilities/upgrades
- Asymmetric factions, campaign, multiplayer, save/load
- Audio beyond minimal placeholder SFX
- Custom shaders/bloom, skeletal animation

---

## 13. Open questions

1. Should Harvesters also construct? (Current answer: no — self-constructing structures keep the prototype lean.)
2. Is a second Nexus needed for a satisfying expansion, or is a "Refinery" drop-off enough? Revisit after the first playtest.
3. Does the AI need a defensive structure before the first wave for readability?
4. Are 4 unit types enough to make counters legible? Revisit with the balance pass.
