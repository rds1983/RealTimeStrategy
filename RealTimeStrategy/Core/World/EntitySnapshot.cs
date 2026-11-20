using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;

namespace RealTimeStrategy.Core.World;

public readonly record struct EntitySnapshot(
    EntityId Id,
    PlayerId Owner,
    EntityKind Kind,
    int DefinitionId,
    Vector3 Position,
    float Health,
    float Radius);
