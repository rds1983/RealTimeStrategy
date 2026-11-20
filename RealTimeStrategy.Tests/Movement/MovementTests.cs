using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.World;
using Xunit;

namespace RealTimeStrategy.Tests.Movement;

public sealed class MovementTests
{
    private static Core.World.World CreateWorld(int seed = 42)
    {
        return new Core.World.World(DefinitionRegistry.CreateDefault(), seed);
    }

    [Fact]
    public void Unit_reaches_destination_and_stops()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-30f, 0f, -30f));
        world.Step(0.05f);
        var destination = new Vector3(-20f, 0f, -20f);

        Assert.True(world.MoveUnit(unit.Id, destination));

        StepUntil(world, () => !unit.Move!.HasDestination, maxTicks: 600);

        Assert.False(unit.Move!.HasDestination);
        Assert.True(Vector3.Distance(unit.Position, destination) <= 0.35f);
        Assert.True(unit.Move.Velocity.Length() < 0.2f);
    }

    [Fact]
    public void Unit_turns_toward_its_velocity()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-30f, 0f, 0f));
        world.Step(0.05f);

        Assert.True(world.MoveUnit(unit.Id, new Vector3(-10f, 0f, 0f)));
        for (var i = 0; i < 40; i++)
        {
            world.Step(0.05f);
        }

        Assert.True(unit.Move!.Velocity.X > 1f);
        Assert.True(MathF.Abs(MathHelper.WrapAngle(unit.Facing)) < 0.1f);
    }

    [Fact]
    public void Unit_finds_a_path_around_walls()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Lancer, new Vector3(-30f, 0f, 0f));
        world.Step(0.05f);

        // Wall at x = 0, z = -5..5 (cells x=30, z=27..32).
        for (var z = 27; z <= 32; z++)
        {
            world.Navigation.SetBlocked(new Point(30, z), true);
        }

        var destination = new Vector3(30f, 0f, 0f);
        Assert.True(world.MoveUnit(unit.Id, destination));

        StepUntil(world, () => !unit.Move!.HasDestination, maxTicks: 1600);

        Assert.True(Vector3.Distance(unit.Position, destination) <= 0.35f);
        Assert.False(unit.Move!.HasDestination);
    }

    [Fact]
    public void Unit_repaths_when_its_path_is_blocked_later()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-30f, 0f, 0f));
        world.Step(0.05f);

        var destination = new Vector3(30f, 0f, 0f);
        Assert.True(world.MoveUnit(unit.Id, destination));

        for (var z = 28; z <= 32; z++)
        {
            world.Navigation.SetBlocked(new Point(30, z), true);
        }

        StepUntil(world, () => Vector3.Distance(unit.Position, destination) <= 0.35f, maxTicks: 2000);

        Assert.True(Vector3.Distance(unit.Position, destination) <= 0.35f);
    }

    [Fact]
    public void Multiple_units_do_not_pile_up_on_one_destination()
    {
        var world = CreateWorld();
        var units = new List<Entity>();

        for (var i = 0; i < 5; i++)
        {
            var unit = world.SpawnUnit(
                PlayerId.Player,
                UnitId.Striker,
                new Vector3(-24f + i * 0.4f, 0f, -20f));

            units.Add(unit);
        }

        world.Step(0.05f);
        var destination = new Vector3(20f, 0f, 0f);

        foreach (var unit in units)
        {
            Assert.True(world.MoveUnit(unit.Id, destination));
        }

        for (var i = 0; i < 1000; i++)
        {
            world.Step(0.05f);
        }

        Assert.All(units, unit => Assert.True(Vector3.Distance(unit.Position, destination) < 4f));

        for (var i = 0; i < units.Count; i++)
        {
            for (var j = i + 1; j < units.Count; j++)
            {
                Assert.True(
                    Vector3.Distance(units[i].Position, units[j].Position) > 1f,
                    $"Units {i} and {j} are stacked at {units[i].Position}.");
            }
        }
    }

    [Fact]
    public void Blocked_destination_is_rejected()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Scout, Vector3.Zero);
        world.Step(0.05f);
        world.Navigation.SetFootprint(MapGrid.WorldToCell(new Vector3(9f, 0f, 9f)), 2, 2, true);

        Assert.False(world.MoveUnit(unit.Id, new Vector3(10f, 0f, 10f)));
    }

    [Fact]
    public void Buildings_cannot_be_ordered_to_move()
    {
        var world = CreateWorld();
        var building = world.SpawnBuilding(PlayerId.Player, BuildingId.Reactor, Vector3.Zero);

        Assert.False(world.MoveUnit(building.Id, new Vector3(10f, 0f, 10f)));
    }

    [Fact]
    public void Stop_clears_the_destination()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-20f, 0f, 0f));
        world.Step(0.05f);
        Assert.True(world.MoveUnit(unit.Id, new Vector3(20f, 0f, 0f)));

        Assert.True(world.StopUnit(unit.Id));

        Assert.False(unit.Move!.HasDestination);
        Assert.Equal(Vector3.Zero, unit.Move.Velocity);
    }

    [Fact]
    public void Previous_position_tracks_the_last_tick()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-20f, 0f, 0f));
        world.Step(0.05f);
        Assert.True(world.MoveUnit(unit.Id, new Vector3(20f, 0f, 0f)));

        var before = unit.Position;
        world.Step(0.05f);

        Assert.Equal(before, unit.PreviousPosition);
        Assert.NotEqual(before, unit.Position);
    }

    [Fact]
    public void Movement_is_deterministic()
    {
        var first = RunDeterministicMovement();
        var second = RunDeterministicMovement();

        Assert.Equal(first, second);
    }

    private static ulong RunDeterministicMovement()
    {
        var world = CreateWorld(seed: 77);
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, new Vector3(-25f, 0f, -5f));
        world.Step(0.05f);
        world.MoveUnit(unit.Id, new Vector3(25f, 0f, 5f));

        for (var i = 0; i < 120; i++)
        {
            world.Step(0.05f);
        }

        return world.ComputeChecksum();
    }

    private static void StepUntil(Core.World.World world, Func<bool> condition, int maxTicks)
    {
        for (var i = 0; i < maxTicks && !condition(); i++)
        {
            world.Step(0.05f);
        }
    }
}
