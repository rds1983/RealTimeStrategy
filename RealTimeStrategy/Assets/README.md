# Assets

Runtime assets, loaded from disk by the game (no MGCB content pipeline).

| Folder | Contents |
| --- | --- |
| `Fonts/` | Rajdhani TTF (SIL OFL, see `OFL.txt`), loaded by Myra from `Presentation/Ui` markup |
| `Ui/` | Myra stylesheets and UI textures |
| `Shaders/` | Precompiled effects, if any are added after the MVP |
| `Audio/` | Sound effects and music |
| `Maps/` | Map / balance JSON files |

Myra markup (`*.xmmp`) is design-time source and lives in `RealTimeStrategy/Presentation/Ui`, not here. Export after editing:

```bash
powershell -ExecutionPolicy Bypass -File tools/export-ui.ps1
```

3D models and most textures are generated in code from primitives; they do not belong here.
