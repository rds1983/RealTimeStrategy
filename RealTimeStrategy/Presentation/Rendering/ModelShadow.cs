namespace RealTimeStrategy.Presentation.Rendering;

/// <summary>Ground footprint used to size fake blob shadows.</summary>
public static class ModelShadow
{
    public static float GetRadius(ModelId id) => id switch
    {
        ModelId.Harvester => 1.7f,
        ModelId.Scout => 1.4f,
        ModelId.Striker => 1.2f,
        ModelId.Lancer => 1.7f,
        ModelId.Nexus => 5.2f,
        ModelId.Reactor => 2.4f,
        ModelId.Fabricator => 3.2f,
        ModelId.Foundry => 3.4f,
        ModelId.Turret => 1.7f,
        ModelId.AlloyNode => 1.9f,
        ModelId.Projectile => 0.3f,
        ModelId.Debris => 0.7f,
        _ => 1f,
    };
}
