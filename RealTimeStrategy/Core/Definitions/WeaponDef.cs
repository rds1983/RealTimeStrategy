namespace RealTimeStrategy.Core.Definitions;

/// <summary>Immutable weapon stats. Damage multipliers live in the combat rules, not here.</summary>
public sealed record WeaponDef
{
    public required WeaponId Id { get; init; }

    public required string Name { get; init; }

    public DamageType DamageType { get; init; }

    public float Damage { get; init; }

    public float Range { get; init; }

    public float CooldownSeconds { get; init; }

    public float ProjectileSpeed { get; init; }

    public float Spread { get; init; }
}
