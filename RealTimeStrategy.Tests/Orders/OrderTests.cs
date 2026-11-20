using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.Orders;
using RealTimeStrategy.Core.World;
using Xunit;

namespace RealTimeStrategy.Tests.Orders;

public sealed class OrderTests
{
    private static Core.World.World CreateWorld(int seed = 42)
    {
        return new Core.World.World(DefinitionRegistry.CreateDefault(), seed);
    }

    [Fact]
    public void Issued_order_is_applied_at_the_next_tick()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-30f, 0f, -30f));
        world.Step(0.05f);

        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(-20f, 0f, -20f))));
        Assert.Empty(unit.Orders!.Orders);

        world.Step(0.05f);

        Assert.Single(unit.Orders.Orders);
        Assert.True(unit.Move!.HasDestination);
    }

    [Fact]
    public void Move_order_completes_and_clears_the_queue()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-30f, 0f, -30f));
        world.Step(0.05f);

        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(-20f, 0f, -20f))));
        world.Step(0.05f);
        StepUntil(world, () => unit.Orders!.IsEmpty, maxTicks: 600);

        Assert.True(unit.Orders!.IsEmpty);
        Assert.False(unit.Move!.HasDestination);
        Assert.True(Vector3.Distance(unit.Position, new Vector3(-20f, 0f, -20f)) <= 0.35f);
    }

    [Fact]
    public void Queued_moves_execute_sequentially()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(-30f, 0f, 0f));
        world.Step(0.05f);

        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(-20f, 0f, 0f)), replace: false));
        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(-10f, 0f, 0f)), replace: false));
        world.Step(0.05f);

        Assert.Equal(2, unit.Orders!.Count);

        StepUntil(
            world,
            () => unit.Orders!.IsEmpty &&
                  Vector3.Distance(unit.Position, new Vector3(-10f, 0f, 0f)) <= 0.35f,
            maxTicks: 1600);

        Assert.True(unit.Orders!.IsEmpty);
        Assert.True(Vector3.Distance(unit.Position, new Vector3(-10f, 0f, 0f)) <= 0.35f);
    }

    [Fact]
    public void Replace_clears_previous_orders_and_movement()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-30f, 0f, 0f));
        world.Step(0.05f);
        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(-20f, 0f, 0f))));
        world.Step(0.05f);

        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(20f, 0f, 0f))));
        world.Step(0.05f);

        Assert.Single(unit.Orders!.Orders);
        Assert.Equal(new Vector3(20f, 0f, 0f), unit.Orders.Current.Point);
        Assert.True(unit.Move!.HasDestination);
        Assert.Equal(new Vector3(20f, 0f, 0f), unit.Move.Destination);
    }

    [Fact]
    public void Queue_capacity_is_four()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(-30f, 0f, 0f));
        world.Step(0.05f);

        for (var i = 0; i < 4; i++)
        {
            Assert.True(world.TryIssueOrder(
                unit.Id,
                Order.Move(new Vector3(-20f + i, 0f, 0f)),
                replace: false));
        }

        Assert.False(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(0f, 0f, 0f)), replace: false));

        world.Step(0.05f);
        Assert.Equal(OrderQueue.Capacity, unit.Orders!.Count);
    }

    [Fact]
    public void Stop_clears_orders_and_holds_the_unit_in_place()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-30f, 0f, 0f));
        world.Step(0.05f);
        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(20f, 0f, 0f))));

        for (var i = 0; i < 30; i++)
        {
            world.Step(0.05f);
        }

        var position = unit.Position;
        Assert.True(world.TryIssueOrder(unit.Id, Order.Stop()));
        world.Step(0.05f);

        Assert.True(unit.Orders!.IsEmpty);
        Assert.False(unit.HoldPosition);
        Assert.Equal(Vector3.Zero, unit.Move!.Velocity);

        for (var i = 0; i < 20; i++)
        {
            world.Step(0.05f);
        }

        Assert.True(Vector3.Distance(position, unit.Position) < 0.01f);
    }

    [Fact]
    public void Hold_clears_orders_and_prevents_movement_until_a_new_order()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-20f, 0f, 0f));
        world.Step(0.05f);

        Assert.True(world.TryIssueOrder(unit.Id, Order.Hold()));
        world.Step(0.05f);

        Assert.True(unit.HoldPosition);
        Assert.True(unit.Orders!.IsEmpty);

        var position = unit.Position;
        for (var i = 0; i < 20; i++)
        {
            world.Step(0.05f);
        }

        Assert.True(Vector3.Distance(position, unit.Position) < 0.01f);

        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(-10f, 0f, 0f))));
        world.Step(0.05f);

        Assert.False(unit.HoldPosition);
        Assert.True(unit.Move!.HasDestination);
    }

    [Fact]
    public void Invalid_orders_are_rejected()
    {
        var world = CreateWorld();
        var player = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-20f, 0f, 0f));
        var ally = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(-10f, 0f, 0f));
        var enemy = world.SpawnUnit(PlayerId.Enemy, UnitId.Scout, new Vector3(10f, 0f, 0f));
        var building = world.SpawnBuilding(PlayerId.Player, BuildingId.Reactor, new Vector3(0f, 0f, -20f));
        world.Step(0.05f);

        Assert.False(world.TryIssueOrder(player.Id, Order.Attack(ally.Id)));
        Assert.False(world.TryIssueOrder(player.Id, Order.Attack(new EntityId(9999))));
        Assert.False(world.TryIssueOrder(player.Id, Order.Move(new Vector3(500f, 0f, 0f))));
        Assert.False(world.TryIssueOrder(player.Id, Order.Move(new Vector3(float.NaN, 0f, 0f))));
        Assert.False(world.TryIssueOrder(building.Id, Order.Move(new Vector3(1f, 0f, 1f))));
        Assert.False(world.TryIssueOrder(player.Id, Order.Harvest(enemy.Id)));
        Assert.False(world.TryIssueOrder(player.Id, Order.ReturnCargo()));
        Assert.False(world.TryIssueOrder(player.Id, Order.Build(BuildingId.Reactor, new Vector3(1f, 0f, 1f))));
        Assert.False(world.TryIssueOrder(player.Id, Order.Produce(UnitId.Scout)));
        Assert.False(world.TryIssueOrder(player.Id, Order.SetRally(Vector3.Zero)));

        Assert.True(world.TryIssueOrder(player.Id, Order.Attack(enemy.Id)));
        Assert.True(world.TryIssueOrder(player.Id, Order.Move(new Vector3(0f, 0f, 0f))));
    }

    [Fact]
    public void Dead_targets_are_rejected()
    {
        var world = CreateWorld();
        var player = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-20f, 0f, 0f));
        var enemy = world.SpawnUnit(PlayerId.Enemy, UnitId.Scout, new Vector3(10f, 0f, 0f));
        world.Step(0.05f);

        world.Enqueue(new DamageCommand(enemy.Id, 500f));
        world.Step(0.05f);

        Assert.False(world.TryIssueOrder(player.Id, Order.Attack(enemy.Id)));
    }

    [Fact]
    public void Replacing_orders_mid_queue_keeps_only_the_new_order()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(-30f, 0f, 0f));
        world.Step(0.05f);

        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(-20f, 0f, 0f)), replace: false));
        Assert.True(world.TryIssueOrder(unit.Id, Order.Move(new Vector3(-10f, 0f, 0f)), replace: false));
        Assert.True(world.TryIssueOrder(unit.Id, Order.Stop()));
        world.Step(0.05f);

        Assert.True(unit.Orders!.IsEmpty);
    }

    private static void StepUntil(Core.World.World world, Func<bool> condition, int maxTicks)
    {
        for (var i = 0; i < maxTicks && !condition(); i++)
        {
            world.Step(0.05f);
        }
    }
}
