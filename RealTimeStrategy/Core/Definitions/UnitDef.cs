namespace RealTimeStrategy.Core.Definitions;

/// <summary>Immutable unit stats, shared by every instance of the unit.</summary>
public sealed record UnitDef
{
    public required UnitId Id { get; init; }

    public required string Name { get; init; }

    public required string Role { get; init; }

    public ResourceCost Cost { get; init; }

    public float BuildTime { get; init; }

    public int Supply { get; init; }

    public float MaxHealth { get; init; }

    public ArmorClass ArmorType { get; init; }

    public float Armor { get; init; }

    public float Speed { get; init; }

    /// <summary>Body radius used for collision and picking.</summary>
    public float Radius { get; init; }

    public WeaponId? Weapon { get; init; }
}
