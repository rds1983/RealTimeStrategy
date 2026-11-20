using RealTimeStrategy.Core.Definitions;
using Xunit;

namespace RealTimeStrategy.Tests.Definitions;

public sealed class DefinitionRegistryTests
{
    private static DefinitionRegistry CreateRegistry(
        IEnumerable<UnitDef>? units = null,
        IEnumerable<BuildingDef>? buildings = null,
        IEnumerable<WeaponDef>? weapons = null,
        IEnumerable<FactionDef>? factions = null)
    {
        return new DefinitionRegistry(
            units ?? DefaultDefinitions.Units,
            buildings ?? DefaultDefinitions.Buildings,
            weapons ?? DefaultDefinitions.Weapons,
            factions ?? DefaultDefinitions.Factions);
    }

    [Fact]
    public void Default_registry_contains_the_mvp_content()
    {
        var registry = DefinitionRegistry.CreateDefault();

        Assert.Equal(4, registry.Units.Count);
        Assert.Equal(5, registry.Buildings.Count);
        Assert.Equal(4, registry.Weapons.Count);
        Assert.Equal(2, registry.Factions.Count);
        Assert.Equal(12f, DefinitionRegistry.BuildRadiusUnits);
    }

    [Fact]
    public void Default_values_match_the_design_document()
    {
        var registry = DefinitionRegistry.CreateDefault();

        var harvester = registry.GetUnit(UnitId.Harvester);
        Assert.Equal(new ResourceCost(50, 0), harvester.Cost);
        Assert.Equal(12f, harvester.BuildTime);
        Assert.Equal(5f, harvester.Speed);
        Assert.Null(harvester.Weapon);

        var lancer = registry.GetUnit(UnitId.Lancer);
        Assert.Equal(new ResourceCost(110, 40), lancer.Cost);
        Assert.Equal(3, lancer.Supply);
        Assert.Equal(WeaponId.LancerPlasma, lancer.Weapon);

        var nexus = registry.GetBuilding(BuildingId.Nexus);
        Assert.Equal(new Footprint(3, 3), nexus.Footprint);
        Assert.Equal(10, nexus.SupplyProvided);
        Assert.True(nexus.IsDropOff);
        Assert.Equal(new[] { UnitId.Harvester }, nexus.Produces);

        var reactor = registry.GetBuilding(BuildingId.Reactor);
        Assert.Equal(0.7f, reactor.EnergyPerSecond);
        Assert.Equal(new Footprint(2, 2), reactor.Footprint);

        var turret = registry.GetBuilding(BuildingId.Turret);
        Assert.Equal(new Footprint(1, 1), turret.Footprint);
        Assert.Equal(WeaponId.TurretPlasma, turret.Weapon);
    }

    [Fact]
    public void Every_weapon_reference_resolves()
    {
        var registry = DefinitionRegistry.CreateDefault();

        foreach (var unit in registry.Units.Values)
        {
            if (unit.Weapon is WeaponId weapon)
            {
                Assert.True(registry.Weapons.ContainsKey(weapon));
            }
        }

        foreach (var building in registry.Buildings.Values)
        {
            if (building.Weapon is WeaponId weapon)
            {
                Assert.True(registry.Weapons.ContainsKey(weapon));
            }
        }
    }

    [Fact]
    public void Every_prerequisite_and_produced_unit_resolves()
    {
        var registry = DefinitionRegistry.CreateDefault();

        foreach (var building in registry.Buildings.Values)
        {
            if (building.Requires is BuildingId required)
            {
                Assert.True(registry.Buildings.ContainsKey(required));
            }

            foreach (var produced in building.Produces)
            {
                Assert.True(registry.Units.ContainsKey(produced));
            }
        }
    }

    [Fact]
    public void Duplicate_unit_ids_are_rejected()
    {
        var units = DefaultDefinitions.Units.Append(DefaultDefinitions.Units[0]);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(units));

        Assert.Contains("Duplicate unit id", exception.Message);
    }

    [Fact]
    public void Unknown_weapon_reference_is_rejected()
    {
        var units = DefaultDefinitions.Units.Select(unit => unit.Id == UnitId.Striker
            ? unit with { Weapon = (WeaponId)999 }
            : unit);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(units));

        Assert.Contains("missing weapon", exception.Message);
        Assert.Contains("Striker", exception.Message);
    }

    [Fact]
    public void Unknown_building_requirement_is_rejected()
    {
        var buildings = DefaultDefinitions.Buildings.Select(building => building.Id == BuildingId.Fabricator
            ? building with { Requires = (BuildingId)999 }
            : building);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(buildings: buildings));

        Assert.Contains("missing building", exception.Message);
        Assert.Contains("Fabricator", exception.Message);
    }

    [Fact]
    public void Self_requirement_is_rejected()
    {
        var buildings = DefaultDefinitions.Buildings.Select(building => building.Id == BuildingId.Fabricator
            ? building with { Requires = BuildingId.Fabricator }
            : building);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(buildings: buildings));

        Assert.Contains("cannot require itself", exception.Message);
    }

    [Fact]
    public void Unknown_produced_unit_is_rejected()
    {
        var buildings = DefaultDefinitions.Buildings.Select(building => building.Id == BuildingId.Fabricator
            ? building with { Produces = new[] { (UnitId)999 } }
            : building);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(buildings: buildings));

        Assert.Contains("produces missing unit", exception.Message);
    }

    [Fact]
    public void Invalid_unit_stats_are_rejected_with_field_names()
    {
        var units = DefaultDefinitions.Units.Select(unit => unit.Id == UnitId.Striker
            ? unit with { MaxHealth = 0f }
            : unit);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(units));

        Assert.Contains("Striker health must be positive", exception.Message);
    }

    [Fact]
    public void Negative_costs_are_rejected()
    {
        var units = DefaultDefinitions.Units.Select(unit => unit.Id == UnitId.Scout
            ? unit with { Cost = new ResourceCost(-1, 0) }
            : unit);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(units));

        Assert.Contains("Scout alloy cost", exception.Message);
    }

    [Fact]
    public void Invalid_weapon_stats_are_rejected()
    {
        var weapons = DefaultDefinitions.Weapons.Select(weapon => weapon.Id == WeaponId.TurretPlasma
            ? weapon with { Range = 0f }
            : weapon);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(weapons: weapons));

        Assert.Contains("TurretPlasma range must be positive", exception.Message);
    }

    [Fact]
    public void Invalid_footprint_is_rejected()
    {
        var buildings = DefaultDefinitions.Buildings.Select(building => building.Id == BuildingId.Nexus
            ? building with { Footprint = new Footprint(0, 3) }
            : building);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(buildings: buildings));

        Assert.Contains("Nexus footprint must be positive", exception.Message);
    }

    [Fact]
    public void Blank_names_are_rejected()
    {
        var weapons = DefaultDefinitions.Weapons.Select(weapon => weapon.Id == WeaponId.ScoutKinetic
            ? weapon with { Name = " " }
            : weapon);

        var exception = Assert.Throws<ArgumentException>(() => CreateRegistry(weapons: weapons));

        Assert.Contains("ScoutKinetic name must not be empty", exception.Message);
    }

    [Fact]
    public void Missing_ids_throw_clear_errors()
    {
        var registry = DefinitionRegistry.CreateDefault();

        Assert.Throws<KeyNotFoundException>(() => registry.GetUnit((UnitId)999));
        Assert.Throws<KeyNotFoundException>(() => registry.GetBuilding((BuildingId)999));
        Assert.Throws<KeyNotFoundException>(() => registry.GetWeapon((WeaponId)999));
        Assert.Throws<KeyNotFoundException>(() => registry.GetFaction((FactionId)999));
    }
}
