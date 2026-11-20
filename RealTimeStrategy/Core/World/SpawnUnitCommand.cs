using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;

namespace RealTimeStrategy.Core.World;

public sealed record SpawnUnitCommand(PlayerId Owner, UnitId Unit, Vector3 Position) : WorldCommand;
