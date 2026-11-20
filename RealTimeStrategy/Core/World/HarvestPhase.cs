namespace RealTimeStrategy.Core.World;

/// <summary>Stages of the harvester trip loop; <see cref="None"/> means idle.</summary>
public enum HarvestPhase
{
    None,
    ToNode,
    Harvesting,
    ToDropOff,
    Depositing,
}
