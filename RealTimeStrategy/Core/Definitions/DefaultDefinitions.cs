namespace RealTimeStrategy.Core.Definitions;

/// <summary>
/// Prototype balance values from <c>Design/GameDesign.md</c>. Code is authoritative at runtime;
/// update this file and the design tables together.
/// </summary>
public static class DefaultDefinitions
{
    public static IReadOnlyList<WeaponDef> Weapons { get; } = new[]
    {
        new WeaponDef
        {
            Id = WeaponId.ScoutKinetic,
            Name = "Scout Repeater",
            DamageType = DamageType.Kinetic,
            Damage = 6f,
            Range = 9f,
            CooldownSeconds = 0.8f,
            ProjectileSpeed = 70f,
            Spread = 0.01f,
        },
        new WeaponDef
        {
            Id = WeaponId.StrikerKinetic,
            Name = "Striker Autocannon",
            DamageType = DamageType.Kinetic,
            Damage = 11f,
            Range = 8f,
            CooldownSeconds = 0.9f,
            ProjectileSpeed = 65f,
            Spread = 0.015f,
        },
        new WeaponDef
        {
            Id = WeaponId.LancerPlasma,
            Name = "Lancer Lance",
            DamageType = DamageType.Plasma,
            Damage = 34f,
            Range = 11f,
            CooldownSeconds = 2.4f,
            ProjectileSpeed = 45f,
            Spread = 0.02f,
        },
        new WeaponDef
        {
            Id = WeaponId.TurretPlasma,
            Name = "Turret Plasma Array",
            DamageType = DamageType.Plasma,
            Damage = 16f,
            Range = 13f,
            CooldownSeconds = 1.2f,
            ProjectileSpeed = 50f,
            Spread = 0.02f,
        },
    };

    public static IReadOnlyList<UnitDef> Units { get; } = new[]
    {
        new UnitDef
        {
            Id = UnitId.Harvester,
            Name = "Harvester",
            Role = "Economy",
            Cost = new ResourceCost(50, 0),
            BuildTime = 12f,
            Supply = 1,
            MaxHealth = 90f,
            ArmorType = ArmorClass.Light,
            Armor = 0f,
            Speed = 5f,
            Radius = 1f,
        },
        new UnitDef
        {
            Id = UnitId.Scout,
            Name = "Scout",
            Role = "Harass",
            Cost = new ResourceCost(40, 0),
            BuildTime = 10f,
            Supply = 1,
            MaxHealth = 70f,
            ArmorType = ArmorClass.Light,
            Armor = 0f,
            Speed = 9f,
            Radius = 0.7f,
            Weapon = WeaponId.ScoutKinetic,
        },
        new UnitDef
        {
            Id = UnitId.Striker,
            Name = "Striker",
            Role = "Line",
            Cost = new ResourceCost(80, 0),
            BuildTime = 16f,
            Supply = 2,
            MaxHealth = 130f,
            ArmorType = ArmorClass.Armored,
            Armor = 2f,
            Speed = 5.5f,
            Radius = 1f,
            Weapon = WeaponId.StrikerKinetic,
        },
        new UnitDef
        {
            Id = UnitId.Lancer,
            Name = "Lancer",
            Role = "Anti-armor",
            Cost = new ResourceCost(110, 40),
            BuildTime = 22f,
            Supply = 3,
            MaxHealth = 170f,
            ArmorType = ArmorClass.Armored,
            Armor = 4f,
            Speed = 4.2f,
            Radius = 1.2f,
            Weapon = WeaponId.LancerPlasma,
        },
    };

    public static IReadOnlyList<BuildingDef> Buildings { get; } = new[]
    {
        new BuildingDef
        {
            Id = BuildingId.Nexus,
            Name = "Nexus",
            Role = "Headquarters",
            Cost = new ResourceCost(400, 0),
            BuildTime = 45f,
            MaxHealth = 1600f,
            ArmorType = ArmorClass.Structure,
            Armor = 5f,
            SupplyProvided = 10,
            Footprint = new Footprint(3, 3),
            Produces = new[] { UnitId.Harvester },
            IsDropOff = true,
        },
        new BuildingDef
        {
            Id = BuildingId.Reactor,
            Name = "Reactor",
            Role = "Power and population",
            Cost = new ResourceCost(100, 0),
            BuildTime = 20f,
            MaxHealth = 600f,
            ArmorType = ArmorClass.Structure,
            Armor = 3f,
            SupplyProvided = 5,
            EnergyPerSecond = 0.7f,
            Footprint = new Footprint(2, 2),
        },
        new BuildingDef
        {
            Id = BuildingId.Fabricator,
            Name = "Fabricator",
            Role = "Unit production",
            Cost = new ResourceCost(150, 0),
            BuildTime = 25f,
            MaxHealth = 900f,
            ArmorType = ArmorClass.Structure,
            Armor = 3f,
            Footprint = new Footprint(3, 3),
            Requires = BuildingId.Reactor,
            Produces = new[] { UnitId.Scout, UnitId.Striker },
        },
        new BuildingDef
        {
            Id = BuildingId.Foundry,
            Name = "Foundry",
            Role = "Heavy production",
            Cost = new ResourceCost(250, 0),
            BuildTime = 35f,
            MaxHealth = 1000f,
            ArmorType = ArmorClass.Structure,
            Armor = 3f,
            Footprint = new Footprint(3, 3),
            Requires = BuildingId.Fabricator,
            Produces = new[] { UnitId.Lancer },
        },
        new BuildingDef
        {
            Id = BuildingId.Turret,
            Name = "Turret",
            Role = "Static defense",
            Cost = new ResourceCost(120, 0),
            BuildTime = 20f,
            MaxHealth = 550f,
            ArmorType = ArmorClass.Structure,
            Armor = 4f,
            Footprint = new Footprint(1, 1),
            Requires = BuildingId.Fabricator,
            Weapon = WeaponId.TurretPlasma,
        },
    };

    public static IReadOnlyList<FactionDef> Factions { get; } = new[]
    {
        new FactionDef
        {
            Id = FactionId.Concord,
            Name = "Concord",
            Description = "Architect collective; light alloy chassis with cyan energy accents.",
        },
        new FactionDef
        {
            Id = FactionId.Null,
            Name = "Null",
            Description = "Swarm machine intelligence; charcoal chassis with magenta accents.",
        },
        new FactionDef
        {
            Id = FactionId.Neutral,
            Name = "Neutral",
            Description = "Unowned alloy deposits and environment props; no player affiliation.",
        },
    };
}
