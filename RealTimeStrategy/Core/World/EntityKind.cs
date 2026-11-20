namespace RealTimeStrategy.Core.World;

public enum EntityKind
{
    Unit,
    Building,

    /// <summary>Unowned alloy deposit. Blocks navigation but is not selectable as a unit.</summary>
    ResourceNode,
}
