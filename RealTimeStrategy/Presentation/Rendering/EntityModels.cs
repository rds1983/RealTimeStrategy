using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.World;

namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>Maps core entity/faction ids to procedural model/palette ids.</summary>
public static class EntityModels
{
    public static ModelId GetModel(Entity entity) => entity.Kind switch
    {
        EntityKind.Unit => GetModel(entity.UnitDef!.Id),
        EntityKind.Building => GetModel(entity.BuildingDef!.Id),
        EntityKind.ResourceNode => ModelId.AlloyNode,
        _ => throw new ArgumentOutOfRangeException(nameof(entity), entity.Kind, "Unknown entity kind."),
    };

    public static ModelId GetModel(UnitId id) => id switch
    {
        UnitId.Harvester => ModelId.Harvester,
        UnitId.Scout => ModelId.Scout,
        UnitId.Striker => ModelId.Striker,
        UnitId.Lancer => ModelId.Lancer,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown unit."),
    };

    public static ModelId GetModel(BuildingId id) => id switch
    {
        BuildingId.Nexus => ModelId.Nexus,
        BuildingId.Reactor => ModelId.Reactor,
        BuildingId.Fabricator => ModelId.Fabricator,
        BuildingId.Foundry => ModelId.Foundry,
        BuildingId.Turret => ModelId.Turret,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown building."),
    };

    public static FactionPaletteId GetPalette(FactionId id) => id switch
    {
        FactionId.Concord => FactionPaletteId.Concord,
        FactionId.Null => FactionPaletteId.Null,
        FactionId.Neutral => FactionPaletteId.Neutral,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown faction."),
    };
}
