using System.Globalization;

namespace RealTimeStrategy.Core.Definitions;

/// <summary>
/// Validated, read-only set of gameplay definitions. Construction fails fast with a message
/// listing every invalid definition, so bad balance data cannot reach the simulation.
/// </summary>
public sealed class DefinitionRegistry
{
    public const float BuildRadiusUnits = 12f;

    private readonly Dictionary<UnitId, UnitDef> _units;
    private readonly Dictionary<BuildingId, BuildingDef> _buildings;
    private readonly Dictionary<WeaponId, WeaponDef> _weapons;
    private readonly Dictionary<FactionId, FactionDef> _factions;

    public DefinitionRegistry(
        IEnumerable<UnitDef> units,
        IEnumerable<BuildingDef> buildings,
        IEnumerable<WeaponDef> weapons,
        IEnumerable<FactionDef> factions)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(buildings);
        ArgumentNullException.ThrowIfNull(weapons);
        ArgumentNullException.ThrowIfNull(factions);

        var unitList = units.ToArray();
        var buildingList = buildings.ToArray();
        var weaponList = weapons.ToArray();
        var factionList = factions.ToArray();

        var errors = Validate(unitList, buildingList, weaponList, factionList);
        if (errors.Count > 0)
        {
            throw new ArgumentException(
                "Invalid definitions:" + Environment.NewLine + " - " + string.Join(Environment.NewLine + " - ", errors));
        }

        _units = unitList.ToDictionary(x => x.Id);
        _buildings = buildingList.ToDictionary(x => x.Id);
        _weapons = weaponList.ToDictionary(x => x.Id);
        _factions = factionList.ToDictionary(x => x.Id);
    }

    public IReadOnlyDictionary<UnitId, UnitDef> Units => _units;

    public IReadOnlyDictionary<BuildingId, BuildingDef> Buildings => _buildings;

    public IReadOnlyDictionary<WeaponId, WeaponDef> Weapons => _weapons;

    public IReadOnlyDictionary<FactionId, FactionDef> Factions => _factions;

    public static DefinitionRegistry CreateDefault() => new(
        DefaultDefinitions.Units,
        DefaultDefinitions.Buildings,
        DefaultDefinitions.Weapons,
        DefaultDefinitions.Factions);

    public UnitDef GetUnit(UnitId id) => _units.TryGetValue(id, out var def)
        ? def
        : throw new KeyNotFoundException($"Unknown unit definition '{id}'.");

    public BuildingDef GetBuilding(BuildingId id) => _buildings.TryGetValue(id, out var def)
        ? def
        : throw new KeyNotFoundException($"Unknown building definition '{id}'.");

    public WeaponDef GetWeapon(WeaponId id) => _weapons.TryGetValue(id, out var def)
        ? def
        : throw new KeyNotFoundException($"Unknown weapon definition '{id}'.");

    public FactionDef GetFaction(FactionId id) => _factions.TryGetValue(id, out var def)
        ? def
        : throw new KeyNotFoundException($"Unknown faction definition '{id}'.");

    private static List<string> Validate(
        UnitDef[] units,
        BuildingDef[] buildings,
        WeaponDef[] weapons,
        FactionDef[] factions)
    {
        var errors = new List<string>();
        var unitIds = new HashSet<UnitId>();
        var buildingIds = new HashSet<BuildingId>();
        var weaponIds = new HashSet<WeaponId>();
        var factionIds = new HashSet<FactionId>();

        foreach (var unit in units)
        {
            if (!unitIds.Add(unit.Id))
            {
                errors.Add($"Duplicate unit id '{unit.Id}'.");
                continue;
            }

            RequireText(errors, unit.Name, $"unit {unit.Id} name");
            RequireText(errors, unit.Role, $"unit {unit.Id} role");
            RequireNonNegative(errors, unit.Cost.Alloy, $"unit {unit.Id} alloy cost");
            RequireNonNegative(errors, unit.Cost.Energy, $"unit {unit.Id} energy cost");
            RequirePositive(errors, unit.BuildTime, $"unit {unit.Id} build time");
            RequireNonNegative(errors, unit.Supply, $"unit {unit.Id} supply");
            RequirePositive(errors, unit.MaxHealth, $"unit {unit.Id} health");
            RequireNonNegative(errors, unit.Armor, $"unit {unit.Id} armor");
            RequirePositive(errors, unit.Speed, $"unit {unit.Id} speed");
            RequirePositive(errors, unit.Radius, $"unit {unit.Id} radius");
        }

        foreach (var building in buildings)
        {
            if (!buildingIds.Add(building.Id))
            {
                errors.Add($"Duplicate building id '{building.Id}'.");
                continue;
            }

            RequireText(errors, building.Name, $"building {building.Id} name");
            RequireText(errors, building.Role, $"building {building.Id} role");
            RequireNonNegative(errors, building.Cost.Alloy, $"building {building.Id} alloy cost");
            RequireNonNegative(errors, building.Cost.Energy, $"building {building.Id} energy cost");
            RequirePositive(errors, building.BuildTime, $"building {building.Id} build time");
            RequirePositive(errors, building.MaxHealth, $"building {building.Id} health");
            RequireNonNegative(errors, building.Armor, $"building {building.Id} armor");
            RequireNonNegative(errors, building.SupplyProvided, $"building {building.Id} supply");
            RequireNonNegative(errors, building.EnergyPerSecond, $"building {building.Id} energy rate");

            if (building.Footprint.Width <= 0 || building.Footprint.Height <= 0)
            {
                errors.Add($"building {building.Id} footprint must be positive.");
            }
        }

        foreach (var weapon in weapons)
        {
            if (!weaponIds.Add(weapon.Id))
            {
                errors.Add($"Duplicate weapon id '{weapon.Id}'.");
                continue;
            }

            RequireText(errors, weapon.Name, $"weapon {weapon.Id} name");
            RequirePositive(errors, weapon.Damage, $"weapon {weapon.Id} damage");
            RequirePositive(errors, weapon.Range, $"weapon {weapon.Id} range");
            RequirePositive(errors, weapon.CooldownSeconds, $"weapon {weapon.Id} cooldown");
            RequirePositive(errors, weapon.ProjectileSpeed, $"weapon {weapon.Id} projectile speed");
            RequireNonNegative(errors, weapon.Spread, $"weapon {weapon.Id} spread");
        }

        foreach (var faction in factions)
        {
            if (!factionIds.Add(faction.Id))
            {
                errors.Add($"Duplicate faction id '{faction.Id}'.");
                continue;
            }

            RequireText(errors, faction.Name, $"faction {faction.Id} name");
        }

        foreach (var unit in units)
        {
            if (unit.Weapon is WeaponId weapon && !weaponIds.Contains(weapon))
            {
                errors.Add($"unit {unit.Id} references missing weapon '{weapon}'.");
            }
        }

        foreach (var building in buildings)
        {
            if (building.Weapon is WeaponId weapon && !weaponIds.Contains(weapon))
            {
                errors.Add($"building {building.Id} references missing weapon '{weapon}'.");
            }

            if (building.Requires is BuildingId required)
            {
                if (required == building.Id)
                {
                    errors.Add($"building {building.Id} cannot require itself.");
                }
                else if (!buildingIds.Contains(required))
                {
                    errors.Add($"building {building.Id} requires missing building '{required}'.");
                }
            }

            foreach (var produced in building.Produces)
            {
                if (!unitIds.Contains(produced))
                {
                    errors.Add($"building {building.Id} produces missing unit '{produced}'.");
                }
            }
        }

        return errors;
    }

    private static void RequireText(List<string> errors, string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{field} must not be empty.");
        }
    }

    private static void RequirePositive(List<string> errors, float value, string field)
    {
        if (!(value > 0) || float.IsNaN(value) || float.IsInfinity(value))
        {
            errors.Add($"{field} must be positive but was {value.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void RequireNonNegative(List<string> errors, float value, string field)
    {
        if (value < 0 || float.IsNaN(value) || float.IsInfinity(value))
        {
            errors.Add($"{field} must be non-negative but was {value.ToString(CultureInfo.InvariantCulture)}.");
        }
    }

    private static void RequireNonNegative(List<string> errors, int value, string field)
    {
        if (value < 0)
        {
            errors.Add($"{field} must be non-negative but was {value.ToString(CultureInfo.InvariantCulture)}.");
        }
    }
}
