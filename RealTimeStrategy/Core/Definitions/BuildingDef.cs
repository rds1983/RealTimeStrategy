namespace RealTimeStrategy.Core.Definitions;

/// <summary>Immutable structure stats, shared by every instance of the structure.</summary>
public sealed record BuildingDef
{
    public required BuildingId Id { get; init; }

    public required string Name { get; init; }

    public required string Role { get; init; }

    public ResourceCost Cost { get; init; }

    public float BuildTime { get; init; }

    public float MaxHealth { get; init; }

    public ArmorClass ArmorType { get; init; } = ArmorClass.Structure;

    public float Armor { get; init; }

    public int SupplyProvided { get; init; }

    public float EnergyPerSecond { get; init; }

    public Footprint Footprint { get; init; }

    public BuildingId? Requires { get; init; }

    public IReadOnlyList<UnitId> Produces { get; init; } = Array.Empty<UnitId>();

    public WeaponId? Weapon { get; init; }

    public bool IsDropOff { get; init; }
}
