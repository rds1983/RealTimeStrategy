namespace RealTimeStrategy.Core.World;

/// <summary>Immutable view of the world for tests and future replay/debug tooling.</summary>
public sealed record WorldSnapshot(
    long TickCount,
    IReadOnlyList<EntitySnapshot> Entities,
    IReadOnlyList<PlayerSnapshot> Players);
