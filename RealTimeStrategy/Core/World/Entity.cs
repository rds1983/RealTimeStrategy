using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;

namespace RealTimeStrategy.Core.World;

/// <summary>
/// Runtime instance of a unit or structure. Definition stats are copied at spawn so rendering and
/// simple stat queries do not need a registry lookup.
/// </summary>
public sealed class Entity
{
    internal Entity(
        EntityId id,
        PlayerId owner,
        EntityKind kind,
        UnitDef? unitDef,
        BuildingDef? buildingDef,
        Vector3 position)
    {
        Id = id;
        Owner = owner;
        Kind = kind;
        UnitDef = unitDef;
        BuildingDef = buildingDef;

        if (unitDef is not null)
        {
            DefinitionId = (int)unitDef.Id;
            MaxHealth = unitDef.MaxHealth;
            Armor = unitDef.Armor;
            Radius = unitDef.Radius;
            Speed = unitDef.Speed;
        }
        else if (buildingDef is not null)
        {
            DefinitionId = (int)buildingDef.Id;
            MaxHealth = buildingDef.MaxHealth;
            Armor = buildingDef.Armor;
            Radius = MathF.Max(buildingDef.Footprint.Width, buildingDef.Footprint.Height)
                * MapGrid.CellSize * 0.5f;
            Speed = 0f;
        }
        else
        {
            throw new ArgumentException("Entity requires a unit or building definition.", nameof(unitDef));
        }

        Position = position;
        PreviousPosition = position;
        Health = MaxHealth;
    }

    public EntityId Id { get; }

    public PlayerId Owner { get; }

    public EntityKind Kind { get; }

    public UnitDef? UnitDef { get; }

    public BuildingDef? BuildingDef { get; }

    public int DefinitionId { get; }

    public Vector3 Position { get; set; }

    public Vector3 PreviousPosition { get; internal set; }

    public float Facing { get; set; }

    public float Health { get; set; }

    public float MaxHealth { get; }

    public float Armor { get; }

    public float Radius { get; }

    public float Speed { get; }

    public bool PendingRemoval { get; internal set; }

    public bool IsAlive => Health > 0 && !PendingRemoval;

    internal void BeginTick()
    {
        PreviousPosition = Position;
    }
}
