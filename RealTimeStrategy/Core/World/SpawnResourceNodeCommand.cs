using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;

namespace RealTimeStrategy.Core.World;

/// <summary>Places an alloy node at a world position; reserve defaults to the balance value.</summary>
public sealed record SpawnResourceNodeCommand(Vector3 Position, int Reserve = EconomyDef.NodeReserve)
    : WorldCommand;
