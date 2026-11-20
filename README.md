# Vector Front

A playable prototype of a futuristic real-time strategy game: harvest alloy, build a base, produce an army and destroy the enemy Nexus.

Every 3D model is generated at runtime from primitives — there are no imported meshes.

## Stack

| Area | Choice |
| --- | --- |
| Language | C# (.NET 8) |
| Engine | MonoGame (DesktopGL) |
| 3D models | DigitalRiseModel (`MeshPrimitives`) |
| UI | Myra |
| Tests | xUnit |

## Layout

```
Design/                  design and technical documentation
RealTimeStrategy/        game source and csproj
RealTimeStrategy/Assets/ fonts, UI styles, shaders, audio, maps
RealTimeStrategy.Tests/  xUnit tests for simulation logic
RealTimeStrategy.slnx    solution
TASKS.md                 roadmap and current progress
AGENTS.md                workflow for AI agents
```

## Build & run

Requires the .NET 8 SDK or newer (projects target `net8.0`).

```bash
dotnet build RealTimeStrategy.slnx
dotnet test RealTimeStrategy.Tests
dotnet run --project RealTimeStrategy
```

Window/graphics smoke check: `dotnet run --project RealTimeStrategy -- --smoke-test 30`

## Documentation

- [Design/GameDesign.md](Design/GameDesign.md) — mechanics, units, buildings, controls, MVP scope
- [Design/TechnicalArchitecture.md](Design/TechnicalArchitecture.md) — projects, simulation, rendering, UI
- [TASKS.md](TASKS.md) — roadmap and current progress
- [AGENTS.md](AGENTS.md) — development workflow
