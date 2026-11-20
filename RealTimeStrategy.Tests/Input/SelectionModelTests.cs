using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.World;
using RealTimeStrategy.Presentation.Camera;
using RealTimeStrategy.Presentation.Input;
using Xunit;

namespace RealTimeStrategy.Tests.Input;

public sealed class SelectionModelTests
{
    private const int Width = 800;
    private const int Height = 600;

    [Fact]
    public void Click_selects_one_unit_and_replaces_previous()
    {
        var world = CreateWorld();
        var first = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-2f, 0f, 0f));
        var second = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(2f, 0f, 0f));
        var selection = new SelectionModel();

        selection.Select(first, additive: false);
        Assert.Equal(1, selection.Count);
        Assert.Same(first, selection.Primary(world));

        selection.Select(second, additive: false);
        Assert.Equal(1, selection.Count);
        Assert.Same(second, selection.Primary(world));
    }

    [Fact]
    public void Shift_click_adds_units()
    {
        var world = CreateWorld();
        var first = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-2f, 0f, 0f));
        var second = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(2f, 0f, 0f));
        var selection = new SelectionModel();

        selection.Select(first, additive: false);
        selection.Select(second, additive: true);
        selection.Select(second, additive: true);

        Assert.Equal(2, selection.Count);
        Assert.Same(first, selection.Primary(world));
    }

    [Fact]
    public void Clicking_a_structure_selects_only_the_structure()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        var building = world.SpawnBuilding(PlayerId.Player, BuildingId.Reactor, new Vector3(5f, 0f, 0f));
        var selection = new SelectionModel();
        selection.Select(unit, additive: false);

        selection.Select(building, additive: false);

        Assert.Equal(1, selection.Count);
        Assert.Same(building, selection.Primary(world));
    }

    [Fact]
    public void Primary_prefers_units_in_mixed_selections()
    {
        var world = CreateWorld();
        var building = world.SpawnBuilding(PlayerId.Player, BuildingId.Reactor, new Vector3(5f, 0f, 0f));
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        var selection = new SelectionModel();

        selection.Select(building, additive: false);
        selection.Select(unit, additive: true);

        Assert.Same(unit, selection.Primary(world));
    }

    [Fact]
    public void Box_selection_picks_units_but_ignores_structures()
    {
        var world = CreateWorld();
        world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-1f, 0f, 0f));
        world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(1f, 0f, 0f));
        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, new Vector3(0f, 0f, -4f));
        var camera = CreateCamera();
        var selection = new SelectionModel();

        selection.SelectRectangle(
            world,
            camera,
            new Rectangle(250, 150, 300, 300),
            Width,
            Height,
            additive: false);

        Assert.Equal(2, selection.Count);
        Assert.All(selection.SelectedIds, id => Assert.Equal(EntityKind.Unit, world.GetEntity(id).Kind));
    }

    [Fact]
    public void Empty_box_clears_selection_unless_additive()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        var camera = CreateCamera();
        var selection = new SelectionModel();
        selection.Select(unit, additive: false);

        selection.SelectRectangle(
            world,
            camera,
            new Rectangle(5, 5, 20, 20),
            Width,
            Height,
            additive: false);
        Assert.Equal(0, selection.Count);

        selection.Select(unit, additive: false);
        selection.SelectRectangle(
            world,
            camera,
            new Rectangle(5, 5, 20, 20),
            Width,
            Height,
            additive: true);
        Assert.Equal(1, selection.Count);
    }

    [Fact]
    public void Prune_removes_dead_and_despawned_entities()
    {
        var world = CreateWorld();
        var first = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-5f, 0f, 0f));
        var second = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(5f, 0f, 0f));
        var selection = new SelectionModel();
        selection.Select(first, additive: false);
        selection.Select(second, additive: true);
        world.Step(0.05f);

        world.Enqueue(new DespawnCommand(first.Id));
        world.Step(0.05f);
        selection.Prune(world);

        Assert.Equal(1, selection.Count);
        Assert.Same(second, selection.Primary(world));
    }

    [Fact]
    public void Control_groups_assign_and_recall()
    {
        var world = CreateWorld();
        var first = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-5f, 0f, 0f));
        var second = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(5f, 0f, 0f));
        var building = world.SpawnBuilding(PlayerId.Player, BuildingId.Reactor, new Vector3(10f, 0f, 0f));
        var selection = new SelectionModel();

        selection.Select(first, additive: false);
        selection.Select(second, additive: true);
        selection.AssignGroup(1);

        selection.Select(building, additive: false);
        Assert.True(selection.RecallGroup(1, world));

        Assert.Equal(2, selection.Count);
        Assert.Contains(first.Id, selection.SelectedIds);
        Assert.Contains(second.Id, selection.SelectedIds);
    }

    [Fact]
    public void Control_groups_survive_deaths_and_clear_when_empty()
    {
        var world = CreateWorld();
        var first = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-5f, 0f, 0f));
        var second = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(5f, 0f, 0f));
        var selection = new SelectionModel();
        selection.Select(first, additive: false);
        selection.Select(second, additive: true);
        selection.AssignGroup(2);
        world.Step(0.05f);

        world.Enqueue(new DamageCommand(first.Id, 500f));
        world.Step(0.05f);
        selection.Clear();
        Assert.True(selection.RecallGroup(2, world));
        Assert.Equal(1, selection.Count);
        Assert.Same(second, selection.Primary(world));

        world.Enqueue(new DamageCommand(second.Id, 500f));
        world.Step(0.05f);
        Assert.True(selection.RecallGroup(2, world));
        Assert.Equal(0, selection.Count);
    }

    [Fact]
    public void Unknown_control_group_is_a_no_op()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        var selection = new SelectionModel();
        selection.Select(unit, additive: false);

        Assert.False(selection.RecallGroup(9, world));
        Assert.Equal(1, selection.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Invalid_control_group_throws(int group)
    {
        var selection = new SelectionModel();

        Assert.Throws<ArgumentOutOfRangeException>(() => selection.AssignGroup(group));
    }

    private static CameraView CreateCamera()
    {
        var camera = new CameraView(MathHelper.PiOver4, 0.5f, 300f);
        camera.SetViewport(Width, Height);
        camera.LookAt(new Vector3(0f, 0f, 20f), Vector3.Zero);
        return camera;
    }

    private static Core.World.World CreateWorld()
    {
        return new Core.World.World(DefinitionRegistry.CreateDefault(), seed: 1);
    }
}
