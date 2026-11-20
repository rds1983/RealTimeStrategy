namespace RealTimeStrategy.Core.Definitions;

/// <summary>
/// Harvesting balance values from <c>Design/GameDesign.md</c>. Kept together because there is a
/// single node type for now; promote to a full definition if node variants appear.
/// </summary>
public static class EconomyDef
{
    /// <summary>Alloy held by a fresh node.</summary>
    public const int NodeReserve = 1800;

    /// <summary>Alloy a Harvester carries per trip.</summary>
    public const int HarvesterCapacity = 22;

    /// <summary>Seconds spent filling one cargo load.</summary>
    public const float HarvestSeconds = 2.2f;

    /// <summary>Seconds spent unloading at a drop-off.</summary>
    public const float DepositSeconds = 0.6f;

    /// <summary>Collision/nav radius of an alloy node.</summary>
    public const float NodeRadius = 1.6f;

    /// <summary>Gap kept between a Harvester and the node or drop-off it works on.</summary>
    public const float InteractionPadding = 1f;
}
