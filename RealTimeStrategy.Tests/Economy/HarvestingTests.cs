using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.Orders;
using RealTimeStrategy.Core.World;
using Xunit;
using WorldState = RealTimeStrategy.Core.World.World;

namespace RealTimeStrategy.Tests.Economy;

public sealed class HarvestingTests
{
    private const float Tick = 0.05f;

    [Fact]
    public void Harvest_cycle_deposits_the_documented_cargo()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(Vector3.Zero);
        var nexus = world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, 14f));
        var harvester = world.SpawnUnit(PlayerId.Player, UnitId.Harvester, new Vector3(-8f, 0f, 0f));
        world.Step(Tick);

        Assert.True(world.TryIssueOrder(harvester.Id, Order.Harvest(node.Id)));
        RunUntil(world, () => world.GetPlayer(PlayerId.Player).Alloy > 0, maxTicks: 2000);

        Assert.Equal(EconomyDef.HarvesterCapacity, world.GetPlayer(PlayerId.Player).Alloy);
        Assert.Equal(EconomyDef.NodeReserve - EconomyDef.HarvesterCapacity, node.Reserve);
        Assert.Equal(0, harvester.Harvester!.Cargo);
        Assert.Equal(nexus.Id, harvester.Harvester.DropOff);
    }

    [Fact]
    public void Harvest_and_deposit_phases_match_documented_timings()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(Vector3.Zero);
        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, 14f));
        var harvester = world.SpawnUnit(PlayerId.Player, UnitId.Harvester, new Vector3(-8f, 0f, 0f));
        world.Step(Tick);

        world.TryIssueOrder(harvester.Id, Order.Harvest(node.Id));

        var harvestSeconds = MeasurePhase(world, harvester, HarvestPhase.Harvesting, HarvestPhase.ToDropOff);
        var depositSeconds = MeasurePhase(world, harvester, HarvestPhase.Depositing, HarvestPhase.ToNode);

        Assert.InRange(harvestSeconds, EconomyDef.HarvestSeconds - Tick, EconomyDef.HarvestSeconds + Tick);
        Assert.InRange(depositSeconds, EconomyDef.DepositSeconds - Tick, EconomyDef.DepositSeconds + Tick);
    }

    [Fact]
    public void Steady_state_cycle_times_stay_within_five_percent()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(Vector3.Zero);
        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, 30f));
        var harvester = world.SpawnUnit(PlayerId.Player, UnitId.Harvester, new Vector3(-8f, 0f, 0f));
        world.Step(Tick);

        world.TryIssueOrder(harvester.Id, Order.Harvest(node.Id));

        var intervals = new List<float>();
        var lastDepositTick = -1;
        var previousAloy = 0;
        var ticks = 0;

        while (intervals.Count < 8 && ticks++ < 6000)
        {
            world.Step(Tick);

            var alloy = world.GetPlayer(PlayerId.Player).Alloy;
            if (alloy == previousAloy)
            {
                continue;
            }

            previousAloy = alloy;

            if (lastDepositTick >= 0)
            {
                intervals.Add((ticks - lastDepositTick) * Tick);
            }

            lastDepositTick = ticks;
        }

        Assert.Equal(8, intervals.Count);
        Assert.Equal(9 * EconomyDef.HarvesterCapacity, world.GetPlayer(PlayerId.Player).Alloy);

        // Skip the first measured interval: the harvester is still on its opening approach.
        var steady = intervals.Skip(1).ToArray();
        var mean = steady.Average();

        foreach (var interval in steady)
        {
            Assert.InRange(interval, mean * 0.95f, mean * 1.05f);
        }
    }

    [Fact]
    public void Cargo_is_capped_by_capacity_and_never_exceeds_the_reserve()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(Vector3.Zero, reserve: 15);
        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, 10f));
        var harvester = world.SpawnUnit(PlayerId.Player, UnitId.Harvester, new Vector3(-6f, 0f, 0f));
        world.Step(Tick);

        world.TryIssueOrder(harvester.Id, Order.Harvest(node.Id));
        RunUntil(world, () => world.GetPlayer(PlayerId.Player).Alloy > 0, maxTicks: 2000);

        Assert.Equal(15, world.GetPlayer(PlayerId.Player).Alloy);
        Assert.Equal(0, node.Reserve);
    }

    [Fact]
    public void Depleted_node_is_removed_and_the_order_finishes()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(Vector3.Zero, reserve: 5);
        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, 10f));
        var harvester = world.SpawnUnit(PlayerId.Player, UnitId.Harvester, new Vector3(-6f, 0f, 0f));
        world.Step(Tick);

        world.TryIssueOrder(harvester.Id, Order.Harvest(node.Id));
        RunUntil(world, () => world.GetPlayer(PlayerId.Player).Alloy > 0, maxTicks: 2000);

        Assert.False(world.TryGetEntity(node.Id, out _));
        Assert.True(harvester.Orders!.IsEmpty);
        Assert.Equal(HarvestPhase.None, harvester.Harvester!.Phase);
        Assert.Equal(5, world.GetPlayer(PlayerId.Player).Alloy);
    }

    [Fact]
    public void Empty_cargo_return_resumes_harvesting_without_income()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(Vector3.Zero);
        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, 14f));
        var harvester = world.SpawnUnit(PlayerId.Player, UnitId.Harvester, new Vector3(-8f, 0f, 0f));
        world.Step(Tick);

        world.TryIssueOrder(harvester.Id, Order.Harvest(node.Id));
        RunUntil(world, () => harvester.Harvester!.Phase == HarvestPhase.Harvesting, maxTicks: 1000);

        Assert.True(world.TryIssueOrder(harvester.Id, Order.ReturnCargo()));
        world.Step(Tick);
        Assert.Equal(HarvestPhase.ToDropOff, harvester.Harvester.Phase);

        RunUntil(world, () => harvester.Harvester!.Phase == HarvestPhase.ToNode, maxTicks: 2000);

        Assert.Equal(0, world.GetPlayer(PlayerId.Player).Alloy);
        Assert.Equal(EconomyDef.NodeReserve, node.Reserve);
    }

    [Fact]
    public void Unreachable_node_aborts_without_leaving_the_harvester_busy()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(Vector3.Zero);
        var harvester = world.SpawnUnit(PlayerId.Player, UnitId.Harvester, new Vector3(-20f, 0f, 0f));
        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, -20f));
        world.Step(Tick);

        // Wall the node in beyond the stand-point search radius.
        var nodeCell = MapGrid.WorldToCell(node.Position);
        for (var dz = -6; dz <= 6; dz++)
        {
            for (var dx = -6; dx <= 6; dx++)
            {
                world.Navigation.SetBlocked(new Point(nodeCell.X + dx, nodeCell.Y + dz), true);
            }
        }

        Assert.True(world.TryIssueOrder(harvester.Id, Order.Harvest(node.Id)));
        var start = harvester.Position;

        for (var i = 0; i < 40; i++)
        {
            world.Step(Tick);
        }

        Assert.Equal(HarvestPhase.None, harvester.Harvester!.Phase);
        Assert.True(harvester.Orders!.IsEmpty);
        Assert.True(Vector3.Distance(start, harvester.Position) < 0.001f);
        Assert.Equal(0, world.GetPlayer(PlayerId.Player).Alloy);
    }

    [Fact]
    public void Node_blocks_its_navigation_cell_and_frees_it_on_removal()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(new Vector3(4f, 0f, 4f));
        world.Step(Tick);

        var cell = MapGrid.WorldToCell(node.Position);
        Assert.False(world.Navigation.IsPassable(cell));

        world.Enqueue(new DespawnCommand(node.Id));
        world.Step(Tick);

        Assert.True(world.Navigation.IsPassable(cell));
    }

    [Fact]
    public void Harvest_is_rejected_for_non_harvesters_and_missing_nodes()
    {
        var world = CreateWorld();
        var node = world.SpawnResourceNode(Vector3.Zero);
        var striker = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-6f, 0f, 0f));
        world.Step(Tick);

        Assert.False(world.TryIssueOrder(striker.Id, Order.Harvest(node.Id)));
        Assert.False(world.TryIssueOrder(striker.Id, Order.Harvest(new EntityId(999))));
        Assert.True(striker.Orders!.IsEmpty);
    }

    [Fact]
    public void Harvesting_is_deterministic()
    {
        static ulong Run()
        {
            var world = CreateWorld(seed: 11);
            var node = world.SpawnResourceNode(Vector3.Zero);
            world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, 12f));
            var harvester = world.SpawnUnit(PlayerId.Player, UnitId.Harvester, new Vector3(-8f, 0f, 0f));
            world.Step(Tick);
            world.TryIssueOrder(harvester.Id, Order.Harvest(node.Id));

            for (var i = 0; i < 400; i++)
            {
                world.Step(Tick);
            }

            return world.ComputeChecksum();
        }

        Assert.Equal(Run(), Run());
    }

    private static float MeasurePhase(
        WorldState world,
        Entity harvester,
        HarvestPhase from,
        HarvestPhase to)
    {
        var ticks = 0;
        var started = false;
        var elapsed = 0f;

        while (ticks++ < 4000)
        {
            world.Step(Tick);
            var phase = harvester.Harvester!.Phase;

            if (!started)
            {
                started = phase == from;
                continue;
            }

            if (phase == to)
            {
                return elapsed;
            }

            elapsed += Tick;
        }

        throw new InvalidOperationException($"Phase {from} never reached {to}.");
    }

    private static void RunUntil(WorldState world, Func<bool> condition, int maxTicks)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            if (condition())
            {
                return;
            }

            world.Step(Tick);
        }

        Assert.True(condition());
    }

    private static WorldState CreateWorld(int seed = 7) =>
        new(DefinitionRegistry.CreateDefault(), seed);
}
