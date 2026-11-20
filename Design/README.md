# Design Documentation

All long-form documentation lives here. `README.md` in the repository root stays short and only links to these files.

| Document | Contents |
| --- | --- |
| [GameDesign.md](GameDesign.md) | Vision, core loop, economy, units, buildings, combat, controls, HUD, MVP scope |
| [TechnicalArchitecture.md](TechnicalArchitecture.md) | Solution layout, simulation model, rendering, input, UI, assets, testing, risks |

## Rules

1. **Docs are part of the deliverable.** A task that changes behavior, numbers, controls, structure or dependencies updates the matching document in the same change.
2. **Design and implementation must not drift.** If code and a table disagree, fix the doc (or the code) before the task is confirmed finished.
3. **Mark intent vs. reality.** Items not yet implemented are described as planned, or listed as out of scope. Do not document features that do not exist as if they do.
4. **Keep it readable.** Short sections, tables for data, no duplicated prose between documents. Link instead of copying.
5. **One topic per file.** New topics get a new `Design/*.md` file, added to the table above.
