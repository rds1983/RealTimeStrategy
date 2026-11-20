using RealTimeStrategy.Core.Definitions;

namespace RealTimeStrategy.Core.World;

public readonly record struct PlayerSnapshot(
    PlayerId Id,
    FactionId Faction,
    int Alloy,
    int Energy,
    int SupplyUsed,
    int SupplyCap);
