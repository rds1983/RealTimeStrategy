# AGENTS.md — Agent Workflow

You are the implementation agent for **Vector Front**, a futuristic RTS prototype. The developer reviews and confirms your work; you do not decide on your own that a task is done.

---

## 0. Read before working

1. This file.
2. `TASKS.md` — find the current task.
3. `Design/GameDesign.md` and `Design/TechnicalArchitecture.md` — the relevant sections, not just the summary.
4. `git status` — never overwrite or revert work you did not create. If the tree is dirty, understand why first.

---

## 1. Mandatory workflow

### Step 1 — Get the next task

- If a task is marked `[~]`, resume it.
- Otherwise take the **first** `[ ]` task in `TASKS.md`, in file order.
- The developer can override the order or hand you a different task; their instruction wins.
- Mark the task `[~]` in `TASKS.md` before you start editing code, unless you are only investigating.
- If the task is too large for one cycle, split it into new tasks (next free IDs) and implement the first slice. Note the split.

### Step 2 — Do it

- Read the acceptance criteria and the design sections they reference. If they conflict, ask before guessing.
- Plan briefly: files to add/change, data model impact, test plan. Then implement.
- Keep the change focused. No drive-by refactors, reformatting or dependency additions.
- Follow the architecture: `Core/` is headless simulation, `Game/` is MonoGame presentation. `Core` never references input, graphics devices or wall-clock time.
- All 3D models are procedural from `MeshPrimitives`. Do not add model files or a model importer.
- Add or update unit tests in the same change as the logic.
- If you discover a required design decision, make the smallest reasonable choice, document it, and mention it in the completion report.

### Step 3 — Test thoroughly

Run, from the repository root:

```bash
dotnet build RealTimeStrategy.slnx
dotnet test RealTimeStrategy.Tests --logger "console;verbosity=normal"
dotnet run --project RealTimeStrategy -- --smoke-test 30
dotnet run --project RealTimeStrategy
```

The smoke test creates the window, renders 30 frames and exits with code 0; use it whenever graphics code changes.

Then verify by category:

| Change type | Required verification |
| --- | --- |
| Simulation logic | Unit tests for happy path, edge cases, invalid input; determinism where relevant |
| Economy/production/combat | Arithmetic tests + a multi-tick integration test that asserts end state |
| Rendering | Launch the game: correct visuals, depth, colors, no exceptions, 60 FPS at default view |
| Input/selection | Manual click/drag/hotkey checklist; tests for pure math (picking, bounds) |
| UI | Launch, verify layout at 1600×900, clicks do not leak into the world, cancel/close paths |
| AI | Headless multi-minute simulation test: no stalls, no illegal commands, deterministic same-seed result |
| Performance | Measure before/after; record numbers in `Design/TechnicalArchitecture.md` |

Do not report success from a build alone. If a manual check is impossible (no display), say so explicitly. Never leave failing or flaky tests behind. Do not "fix" unrelated failures silently — report them.

### Step 4 — Update documentation

In the same change:

- `Design/GameDesign.md` — mechanics, numbers, controls, UI, scope changes.
- `Design/TechnicalArchitecture.md` — structure, packages, rendering/sim architecture, performance data, risks.
- `Design/README.md` — when adding a new design document.
- `TASKS.md` — task notes, splits, discovered follow-ups (new tasks).
- `README.md` — only build/run/layout changes; keep it brief.

If nothing in the docs changed, that is fine — but verify it is true.

### Step 5 — Ask the developer for confirmation

Stop and ask. Present a short report:

```
Task: T-0xx — <title>
What changed: <files/systems, 2–4 bullets>
Tests: build OK; N tests passed (new: ...); manual: <what you checked and saw>
Docs: <files updated / no changes needed>
Notes/decisions/risks: <anything the developer should know>
Is this task finished?
```

Wait for an explicit answer. Silence, a new unrelated request, or "looks interesting" is **not** confirmation. If the developer requests changes, make them and ask again.

### Step 6 — Only after explicit confirmation

1. Mark the task `[x]` in `TASKS.md` and add a row to the **Done log** (task, title, date, commit hash).
2. Review the remaining `TASKS.md` roadmap. You may reorganize it freely:
   - add tasks for gaps or follow-ups discovered while working,
   - remove tasks that are no longer relevant,
   - split, merge or reorder tasks,
   - update acceptance criteria that proved wrong.
   Keep IDs stable for existing tasks; preserve the milestone structure unless there is a reason to change it.
3. Commit and push:
   ```bash
   git add -A
   git commit -m "T-0xx: <imperative summary>"
   git push
   ```
   - Subject format: `T-0xx: <summary>` (max ~72 chars). Body (optional): why, notable decisions, tests run.
   - Commit code, tests, docs and `TASKS.md` together. Never commit `bin/`, `obj/`, build output or secrets.
   - Push to the current branch (`origin/master` unless the developer says otherwise). Never force-push. If the push fails, report it; do not rewrite history.
   - Do not add AI/co-author attribution unless asked.
4. Report the commit hash and stop. Start the next task only when the developer asks.

---

## 2. Blocking and escalation

Ask the developer before proceeding when:

- Acceptance criteria are ambiguous or contradictory.
- A change would require a new NuGet package, a different framework version, or a public API break.
- The task requires deleting or rewriting work you did not create.
- You must choose between two materially different designs (e.g. pathfinding approach, UI layout).
- Tests fail for reasons unrelated to the task, or the environment cannot run the game.
- The task cannot meet its acceptance criteria within a reasonable change size.

When blocked: mark the task `[~]`, add a short note under it in `TASKS.md`, and ask a concrete question with options. Never mark a blocked task `[x]`.

---

## 3. Definition of done

A task is done only when **all** are true and confirmed:

- [ ] Acceptance criteria met (demonstrated, not assumed).
- [ ] `dotnet build` and `dotnet test` pass; new logic has tests.
- [ ] The game launches and the affected feature was exercised manually (or the limitation is stated).
- [ ] No unrelated changes, dead code, TODOs without a task, or debug prints left behind.
- [ ] No new per-frame allocations in `Core` hot paths; performance budgets respected.
- [ ] Relevant `Design/` docs and `TASKS.md` updated.
- [ ] Developer explicitly confirmed the task.

---

## 4. Code conventions

- C# 12 / net8.0, nullable enabled, file-scoped namespaces, 4-space indentation, `_camelCase` private fields.
- One public type per file; folders mirror namespaces (`RealTimeStrategy.Core.Combat`, `RealTimeStrategy.Game.Rendering`).
- Prefer plain classes, readonly structs and explicit systems over frameworks, reflection or a custom ECS.
- Simulation: pass `float dt` explicitly; no `GameTime`, `DateTime`, `Task`, or `Random` without a seed in `Core`.
- Keep update paths allocation-free: no LINQ, closures, string formatting or `foreach` over interface-typed collections in per-tick loops.
- Numbers come from definitions/balance data, not magic literals scattered in systems.
- Comments explain *why*, not *what*. Public types get a one-line XML summary when the name is not self-explanatory.
- Dispose GPU resources; no static `GraphicsDevice` references in `Core`.
- Never commit generated files, IDE state, or assets that duplicate procedural primitives.

---

## 5. Repository rules

- `README.md` stays short: what the project is, stack, layout, build/run, links. Details go to `Design/`.
- `TASKS.md` is the only progress tracker; do not create parallel todo lists.
- `Design/` holds all design and technical documentation in Markdown.
- Game assets belong under `RealTimeStrategy/Assets/`. Generated meshes/textures live in code and are not assets.
- One active task at a time (`[~]`); do not stack up unconfirmed tasks.
- Commit only after confirmation (except when the developer explicitly asks for a WIP commit).
