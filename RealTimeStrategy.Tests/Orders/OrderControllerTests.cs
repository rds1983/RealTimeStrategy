using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.Orders;
using RealTimeStrategy.Core.World;
using RealTimeStrategy.Presentation.Input;
using Xunit;

namespace RealTimeStrategy.Tests.Orders;

public sealed class OrderControllerTests
{
    [Fact]
    public void Context_move_sends_selected_units_to_the_point()
    {
        var world = CreateWorld();
        var selection = new SelectionModel();
        var controller = new OrderController(selection);
        var first = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-30f, 0f, -10f));
        var second = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(-30f, 0f, -6f));
        selection.Select(first, additive: false);
        selection.Select(second, additive: true);
        world.Step(0.05f);

        var ordered = controller.IssueContext(world, null, new Vector3(20f, 0f, 20f), queue: false);

        Assert.Equal(2, ordered);
        world.Step(0.05f);
        Assert.Equal(OrderType.Move, first.Orders!.Current.Type);
        Assert.Equal(OrderType.Move, second.Orders!.Current.Type);
    }

    [Fact]
    public void Context_attack_targets_an_enemy()
    {
        var world = CreateWorld();
        var selection = new SelectionModel();
        var controller = new OrderController(selection);
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-20f, 0f, 0f));
        var enemy = world.SpawnUnit(PlayerId.Enemy, UnitId.Scout, new Vector3(10f, 0f, 0f));
        selection.Select(unit, additive: false);
        world.Step(0.05f);

        var ordered = controller.IssueContext(world, enemy, null, queue: false);

        Assert.Equal(1, ordered);
        world.Step(0.05f);
        Assert.Equal(OrderType.Attack, unit.Orders!.Current.Type);
        Assert.Equal(enemy.Id, unit.Orders.Current.Target);
    }

    [Fact]
    public void Context_on_a_structure_stores_a_rally_point()
    {
        var world = CreateWorld();
        var selection = new SelectionModel();
        var controller = new OrderController(selection);
        var building = world.SpawnBuilding(PlayerId.Player, BuildingId.Fabricator, Vector3.Zero);
        selection.Select(building, additive: false);
        var rally = new Vector3(12f, 0f, -8f);

        var ordered = controller.IssueContext(world, null, rally, queue: false);
        world.Step(0.05f);

        Assert.Equal(1, ordered);
        Assert.Equal(rally, building.RallyPoint);
        Assert.Equal(1, selection.Count);
    }

    [Fact]
    public void Attack_move_issues_attack_move_orders()
    {
        var world = CreateWorld();
        var selection = new SelectionModel();
        var controller = new OrderController(selection);
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(-20f, 0f, 0f));
        selection.Select(unit, additive: false);
        world.Step(0.05f);

        Assert.Equal(1, controller.IssueAttackMove(world, new Vector3(20f, 0f, 0f), queue: false));
        world.Step(0.05f);

        Assert.Equal(OrderType.AttackMove, unit.Orders!.Current.Type);
        Assert.False(controller.AttackMoveArmed);
    }

    [Fact]
    public void Attack_move_arming_state_can_be_toggled()
    {
        var controller = new OrderController(new SelectionModel());

        Assert.False(controller.AttackMoveArmed);
        controller.ArmAttackMove();
        Assert.True(controller.AttackMoveArmed);
        controller.CancelAttackMove();
        Assert.False(controller.AttackMoveArmed);
    }

    [Fact]
    public void Stop_and_hold_apply_to_selected_units()
    {
        var world = CreateWorld();
        var selection = new SelectionModel();
        var controller = new OrderController(selection);
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-20f, 0f, 0f));
        selection.Select(unit, additive: false);
        world.Step(0.05f);
        Assert.Equal(1, controller.IssueContext(world, null, new Vector3(20f, 0f, 0f), queue: false));
        world.Step(0.05f);

        Assert.Equal(1, controller.IssueStop(world));
        world.Step(0.05f);
        Assert.True(unit.Orders!.IsEmpty);

        Assert.Equal(1, controller.IssueHold(world));
        world.Step(0.05f);
        Assert.True(unit.HoldPosition);
    }

    [Fact]
    public void Shift_queue_context_appends_orders()
    {
        var world = CreateWorld();
        var selection = new SelectionModel();
        var controller = new OrderController(selection);
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(-30f, 0f, 0f));
        selection.Select(unit, additive: false);
        world.Step(0.05f);

        Assert.Equal(1, controller.IssueContext(world, null, new Vector3(-20f, 0f, 0f), queue: false));
        Assert.Equal(1, controller.IssueContext(world, null, new Vector3(-10f, 0f, 0f), queue: true));
        world.Step(0.05f);

        Assert.Equal(2, unit.Orders!.Count);
    }

    [Fact]
    public void No_selection_is_a_no_op()
    {
        var world = CreateWorld();
        var controller = new OrderController(new SelectionModel());

        Assert.Equal(0, controller.IssueContext(world, null, Vector3.Zero, queue: false));
        Assert.Equal(0, controller.IssueAttackMove(world, Vector3.Zero, queue: false));
        Assert.Equal(0, controller.IssueStop(world));
        Assert.Equal(0, controller.IssueHold(world));
    }

    [Fact]
    public void Context_order_drives_a_unit_across_the_map()
    {
        var world = CreateWorld();
        var selection = new SelectionModel();
        var controller = new OrderController(selection);
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-50f, 0f, 0f));
        selection.Select(unit, additive: false);
        world.Step(0.05f);
        var destination = new Vector3(50f, 0f, 0f);

        Assert.Equal(1, controller.IssueContext(world, null, destination, queue: false));

        world.Step(0.05f);

        for (var i = 0; i < 4000 && !unit.Orders!.IsEmpty; i++)
        {
            world.Step(0.05f);
        }

        Assert.True(Vector3.Distance(unit.Position, destination) <= 0.35f);
        Assert.True(unit.Orders!.IsEmpty);
    }

    [Fact]
    public void Building_rally_order_is_applied_by_the_world()
    {
        var world = CreateWorld();
        var building = world.SpawnBuilding(PlayerId.Player, BuildingId.Fabricator, Vector3.Zero);
        world.Step(0.05f);

        Assert.True(world.TryIssueOrder(building.Id, Order.SetRally(new Vector3(4f, 0f, 6f))));
        world.Step(0.05f);

        Assert.Equal(new Vector3(4f, 0f, 6f), building.RallyPoint);
    }

    private static Core.World.World CreateWorld()
    {
        return new Core.World.World(DefinitionRegistry.CreateDefault(), seed: 7);
    }
}
