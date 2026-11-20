namespace RealTimeStrategy.Core.World;

/// <summary>
/// Per-harvester trip state. Only Harvester units own one; <see cref="Core.Economy.HarvestingSystem"/>
/// advances it. Cargo survives order replacement so a loaded harvester never loses alloy.
/// </summary>
public sealed class HarvesterState
{
    public HarvestPhase Phase { get; internal set; }

    /// <summary>Node being mined; kept across a deposit so the trip can resume.</summary>
    public EntityId Node { get; internal set; }

    public EntityId DropOff { get; internal set; }

    public int Cargo { get; internal set; }

    /// <summary>Seconds spent in the current gated phase (harvest or deposit).</summary>
    public float Timer { get; internal set; }

    public bool HasNode => Node.Value != 0;

    internal void Reset()
    {
        Phase = HarvestPhase.None;
        Node = default;
        DropOff = default;
        Timer = 0f;
    }
}
