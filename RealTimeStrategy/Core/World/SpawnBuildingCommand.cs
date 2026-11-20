using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;

namespace RealTimeStrategy.Core.World;

public sealed record SpawnBuildingCommand(PlayerId Owner, BuildingId Building, Vector3 Position) : WorldCommand;
