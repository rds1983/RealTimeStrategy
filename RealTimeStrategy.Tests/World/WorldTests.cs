using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.World;
using Xunit;

namespace RealTimeStrategy.Tests.World;

public sealed class WorldTests
{
    private static Core.World.World CreateWorld(int seed = 42)
    {
        return new Core.World.World(DefinitionRegistry.CreateDefault(), seed);
    }

    [Fact]
    public void Spawn_commands_apply_at_the_start_of_the_tick()
    {
        var world = CreateWorld();
        world.Enqueue(new SpawnUnitCommand(PlayerId.Player, UnitId.Striker, new Vector3(1f, 0f, 2f)));

        Assert.Empty(world.Entities);
        Assert.Equal(0, world.TickCount);

        world.Step(0.05f);

        var entity = Assert.Single(world.Entities);
        Assert.Equal(new Vector3(1f, 0f, 2f), entity.Position);
        Assert.Equal(2, world.GetPlayer(PlayerId.Player).SupplyUsed);
        Assert.Equal(1, world.TickCount);
    }

    [Fact]
    public void Buildings_raise_the_supply_cap()
    {
        var world = CreateWorld();
        world.Enqueue(new SpawnBuildingCommand(PlayerId.Player, BuildingId.Nexus, Vector3.Zero));
        world.Enqueue(new SpawnBuildingCommand(PlayerId.Player, BuildingId.Reactor, new Vector3(10f, 0f, 0f)));

        world.Step(0.05f);

        var player = world.GetPlayer(PlayerId.Player);
        Assert.Equal(2, world.Entities.Count);
        Assert.Equal(0, player.SupplyUsed);
        Assert.Equal(15, player.SupplyCap);
    }

    [Fact]
    public void Despawn_removes_the_entity_at_tick_end_and_frees_supply()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        world.Step(0.05f);

        world.Enqueue(new DespawnCommand(unit.Id));
        world.Step(0.05f);

        Assert.Empty(world.Entities);
        Assert.Equal(0, world.GetPlayer(PlayerId.Player).SupplyUsed);
    }

    [Fact]
    public void Lethal_damage_removes_the_entity_and_frees_supply()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        world.Step(0.05f);

        world.Enqueue(new DamageCommand(unit.Id, 500f));
        world.Step(0.05f);

        Assert.Empty(world.Entities);
        Assert.Equal(0, world.GetPlayer(PlayerId.Player).SupplyUsed);
    }

    [Fact]
    public void Commands_against_missing_entities_are_ignored()
    {
        var world = CreateWorld();
        world.Enqueue(new DamageCommand(new EntityId(999), 10f));
        world.Enqueue(new DespawnCommand(new EntityId(999)));

        world.Step(0.05f);

        Assert.Empty(world.Entities);
    }

    [Fact]
    public void Spawn_positions_clamp_to_the_map()
    {
        var world = CreateWorld();

        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(500f, 0f, -500f));

        Assert.Equal(MapGrid.HalfSize, unit.Position.X);
        Assert.Equal(-MapGrid.HalfSize, unit.Position.Z);
        Assert.Equal(0f, unit.Position.Y);
    }

    [Fact]
    public void Same_seed_and_commands_produce_the_same_checksum()
    {
        var first = RunScriptedMatch();
        var second = RunScriptedMatch();

        Assert.Equal(first, second);
    }

    [Fact]
    public void Different_commands_produce_a_different_checksum()
    {
        var baseline = RunScriptedMatch(damage: false);
        var damaged = RunScriptedMatch(damage: true);

        Assert.NotEqual(baseline, damaged);
    }

    [Fact]
    public void Checksum_includes_player_resources()
    {
        var first = CreateWorld();
        var second = CreateWorld();
        first.GetPlayer(PlayerId.Player).SetResources(500, 100);
        second.GetPlayer(PlayerId.Player).SetResources(400, 100);

        Assert.NotEqual(first.ComputeChecksum(), second.ComputeChecksum());
    }

    [Fact]
    public void Snapshot_reports_entities_players_and_tick()
    {
        var world = CreateWorld();
        world.Enqueue(new SpawnBuildingCommand(PlayerId.Player, BuildingId.Nexus, Vector3.Zero));
        world.Enqueue(new SpawnUnitCommand(PlayerId.Enemy, UnitId.Scout, new Vector3(3f, 0f, 4f)));
        world.Step(0.05f);

        var snapshot = world.CreateSnapshot();

        Assert.Equal(1, snapshot.TickCount);
        Assert.Equal(2, snapshot.Entities.Count);
        Assert.Equal(2, snapshot.Players.Count);
        Assert.Contains(snapshot.Entities, entity => entity.Kind == EntityKind.Building);
        Assert.Contains(snapshot.Players, player => player.Id == PlayerId.Enemy && player.SupplyUsed == 1);
    }

    [Fact]
    public void Seeded_rng_is_reproducible()
    {
        var first = CreateWorld(seed: 7);
        var second = CreateWorld(seed: 7);

        Assert.Equal(first.Rng.Next(), second.Rng.Next());
        Assert.Equal(first.Rng.Next(), second.Rng.Next());
    }

    [Fact]
    public void Negative_delta_time_throws()
    {
        var world = CreateWorld();

        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(-0.05f));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(float.NaN));
    }

    [Fact]
    public void Unknown_entity_lookup_throws()
    {
        var world = CreateWorld();

        Assert.Throws<KeyNotFoundException>(() => world.GetEntity(new EntityId(999)));
        Assert.False(world.TryGetEntity(new EntityId(999), out _));
    }

    [Fact]
    public void Players_start_with_no_resources_or_supply()
    {
        var world = CreateWorld();

        foreach (var player in world.Players.Values)
        {
            Assert.Equal(0, player.Alloy);
            Assert.Equal(0, player.Energy);
            Assert.Equal(0, player.SupplyUsed);
            Assert.Equal(0, player.SupplyCap);
        }
    }

    private static ulong RunScriptedMatch(bool damage = true)
    {
        var world = CreateWorld(seed: 123);
        world.GetPlayer(PlayerId.Player).SetResources(500, 100);
        world.GetPlayer(PlayerId.Enemy).SetResources(500, 100);

        world.Enqueue(new SpawnBuildingCommand(PlayerId.Player, BuildingId.Nexus, new Vector3(-10f, 0f, -10f)));
        world.Enqueue(new SpawnUnitCommand(PlayerId.Player, UnitId.Striker, new Vector3(-5f, 0f, -5f)));
        world.Enqueue(new SpawnUnitCommand(PlayerId.Enemy, UnitId.Scout, new Vector3(10f, 0f, 10f)));
        world.Step(0.05f);

        if (damage)
        {
            var target = world.Entities[1];
            world.Enqueue(new DamageCommand(target.Id, 10f));
        }

        world.Step(0.05f);
        return world.ComputeChecksum();
    }
}
