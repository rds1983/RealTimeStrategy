using System.Diagnostics;
using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.World;
using Xunit;

namespace RealTimeStrategy.Tests.World;

public sealed class SpatialGridTests
{
    private static Core.World.World CreateWorld(int seed = 42)
    {
        return new Core.World.World(DefinitionRegistry.CreateDefault(), seed);
    }

    [Fact]
    public void Radius_queries_match_brute_force()
    {
        var world = CreateWorld();
        var rng = new Random(1234);

        for (var i = 0; i < 200; i++)
        {
            var unit = (UnitId)(i % 4);
            var owner = i % 2 == 0 ? PlayerId.Player : PlayerId.Enemy;
            var position = new Vector3(
                (float)(rng.NextDouble() * 110.0 - 55.0),
                0f,
                (float)(rng.NextDouble() * 110.0 - 55.0));

            world.SpawnUnit(owner, unit, position);
        }

        world.Step(0.05f);
        var results = new List<Entity>();

        for (var query = 0; query < 80; query++)
        {
            var center = new Vector3(
                (float)(rng.NextDouble() * 130.0 - 65.0),
                0f,
                (float)(rng.NextDouble() * 130.0 - 65.0));
            var radius = 1f + (float)rng.NextDouble() * 15f;
            var radiusSquared = radius * radius;

            var expected = world.Entities
                .Where(entity => Vector3.DistanceSquared(center, entity.Position) <= radiusSquared)
                .Select(entity => entity.Id)
                .OrderBy(id => id.Value)
                .ToArray();

            var found = world.Spatial.Query(center, radius, results);
            var actual = results.Select(entity => entity.Id).OrderBy(id => id.Value).ToArray();

            Assert.Equal(expected.Length, found);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Nearest_enemy_ignores_allies_and_respects_range()
    {
        var world = CreateWorld();
        var ally = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        var nearEnemy = world.SpawnUnit(PlayerId.Enemy, UnitId.Scout, new Vector3(5f, 0f, 0f));
        world.SpawnUnit(PlayerId.Enemy, UnitId.Striker, new Vector3(12f, 0f, 0f));
        world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(1f, 0f, 0f));
        world.Step(0.05f);

        var nearest = world.Spatial.FindNearestEnemy(Vector3.Zero, 20f, PlayerId.Player);

        Assert.NotNull(nearest);
        Assert.Equal(nearEnemy.Id, nearest.Id);
        Assert.NotEqual(ally.Id, nearest.Id);
        Assert.Null(world.Spatial.FindNearestEnemy(Vector3.Zero, 2f, PlayerId.Player));
    }

    [Fact]
    public void Rebuild_drops_despawned_entities()
    {
        var world = CreateWorld();
        var unit = world.SpawnUnit(PlayerId.Player, UnitId.Striker, Vector3.Zero);
        world.Step(0.05f);
        Assert.Equal(1, world.Spatial.Count);

        world.Enqueue(new DespawnCommand(unit.Id));
        world.Step(0.05f);

        Assert.Equal(0, world.Spatial.Count);
        var results = new List<Entity>();
        Assert.Equal(0, world.Spatial.Query(Vector3.Zero, 10f, results));
    }

    [Fact]
    public void Span_query_reports_total_found_and_fills_available_slots()
    {
        var world = CreateWorld();
        for (var i = 0; i < 10; i++)
        {
            world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(i * 0.5f, 0f, 0f));
        }

        world.Step(0.05f);
        var buffer = new Entity[4];

        var total = world.Spatial.Query(new Vector3(2f, 0f, 0f), 20f, buffer.AsSpan());

        Assert.Equal(10, total);
        Assert.All(buffer, entity => Assert.NotNull(entity));
    }

    [Fact]
    public void Query_at_map_edge_stays_inside_cell_bounds()
    {
        var world = CreateWorld();
        var corner = world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(59.5f, 0f, 59.5f));
        world.SpawnUnit(PlayerId.Player, UnitId.Scout, new Vector3(-59.5f, 0f, -59.5f));
        world.Step(0.05f);

        var results = new List<Entity>();
        var count = world.Spatial.Query(new Vector3(60f, 0f, 60f), 3f, results);

        Assert.Equal(1, count);
        Assert.Same(corner, results[0]);
    }

    [Fact]
    public void List_query_clears_previous_results()
    {
        var world = CreateWorld();
        world.SpawnUnit(PlayerId.Player, UnitId.Scout, Vector3.Zero);
        world.Step(0.05f);
        var results = new List<Entity> { world.Entities[0], world.Entities[0] };

        var count = world.Spatial.Query(new Vector3(20f, 0f, 20f), 2f, results);

        Assert.Equal(0, count);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Invalid_radius_returns_nothing(float radius)
    {
        var world = CreateWorld();
        world.SpawnUnit(PlayerId.Player, UnitId.Scout, Vector3.Zero);
        world.Step(0.05f);
        var results = new List<Entity>();

        Assert.Equal(0, world.Spatial.Query(Vector3.Zero, radius, results));
        Assert.Null(world.Spatial.FindNearestEnemy(Vector3.Zero, radius, PlayerId.Player));
    }

    [Theory]
    [InlineData(0f, 2f)]
    [InlineData(-1f, 2f)]
    [InlineData(120f, 0f)]
    [InlineData(120f, -2f)]
    public void Invalid_grid_arguments_throw(float mapSize, float cellSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpatialGrid(mapSize, cellSize));
    }

    [Fact]
    public void Three_hundred_units_queries_stay_under_budget()
    {
        var world = CreateWorld();
        var rng = new Random(99);

        for (var i = 0; i < 300; i++)
        {
            var position = new Vector3(
                (float)(rng.NextDouble() * 120.0 - 60.0),
                0f,
                (float)(rng.NextDouble() * 120.0 - 60.0));
            world.SpawnUnit(
                i % 2 == 0 ? PlayerId.Player : PlayerId.Enemy,
                (UnitId)(i % 4),
                position);
        }

        world.Step(0.05f);

        var results = new List<Entity>(128);
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < 1_000; i++)
        {
            var center = new Vector3(
                (float)(rng.NextDouble() * 120.0 - 60.0),
                0f,
                (float)(rng.NextDouble() * 120.0 - 60.0));
            world.Spatial.Query(center, 10f, results);
        }

        stopwatch.Stop();

        Assert.Equal(300, world.Spatial.Count);
        Assert.True(
            stopwatch.ElapsedMilliseconds < 200,
            $"1,000 radius queries over 300 units took {stopwatch.ElapsedMilliseconds} ms.");
    }
}
