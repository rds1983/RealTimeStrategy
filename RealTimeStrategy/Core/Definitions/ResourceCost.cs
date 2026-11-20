namespace RealTimeStrategy.Core.Definitions;

/// <summary>Alloy and energy price of a unit or structure.</summary>
public readonly record struct ResourceCost(int Alloy, int Energy)
{
    public static ResourceCost Free => new(0, 0);
}
