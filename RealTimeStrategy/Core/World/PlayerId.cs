namespace RealTimeStrategy.Core.World;

public enum PlayerId
{
    Player,
    Enemy,

    /// <summary>Unowned world objects such as alloy deposits; has no economy or supply.</summary>
    Neutral,
}
