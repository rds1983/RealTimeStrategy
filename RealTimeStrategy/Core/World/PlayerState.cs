using RealTimeStrategy.Core.Definitions;

namespace RealTimeStrategy.Core.World;

/// <summary>Per-player economy and population state.</summary>
public sealed class PlayerState
{
    internal PlayerState(PlayerId id, FactionId faction)
    {
        Id = id;
        Faction = faction;
    }

    public PlayerId Id { get; }

    public FactionId Faction { get; }

    public int Alloy { get; private set; }

    public int Energy { get; private set; }

    public int SupplyUsed { get; private set; }

    public int SupplyCap { get; private set; }

    public void SetResources(int alloy, int energy)
    {
        if (alloy < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(alloy), alloy, "Alloy must be non-negative.");
        }

        if (energy < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(energy), energy, "Energy must be non-negative.");
        }

        Alloy = alloy;
        Energy = energy;
    }

    public void AddAlloy(int amount) => Alloy = Math.Max(0, Alloy + amount);

    public void AddEnergy(int amount) => Energy = Math.Max(0, Energy + amount);

    internal void AddSupplyUsed(int amount) => SupplyUsed = Math.Max(0, SupplyUsed + amount);

    internal void AddSupplyCap(int amount) => SupplyCap = Math.Max(0, SupplyCap + amount);
}
