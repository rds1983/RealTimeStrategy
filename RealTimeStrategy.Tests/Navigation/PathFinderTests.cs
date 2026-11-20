using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.Navigation;
using RealTimeStrategy.Core.World;
using Xunit;

namespace RealTimeStrategy.Tests.Navigation;

public sealed class PathFinderTests
{
    private static readonly float Sqrt2 = MathF.Sqrt(2f);

    [Fact]
    public void Straight_path_is_found_and_smoothed_to_the_endpoints()
    {
        var grid = new NavGrid();
        var finder = new PathFinder(grid);
        var path = new List<Point>();

        Assert.True(finder.TryFindPath(new Point(2, 3), new Point(20, 3), path));

        Assert.Equal(new Point(2, 3), path[0]);
        Assert.Equal(new Point(20, 3), path[^1]);
        Assert.Equal(2, path.Count);
        Assert.Equal(18f, PathCost(path), 3);
    }

    [Fact]
    public void Diagonal_path_is_found()
    {
        var grid = new NavGrid();
        var finder = new PathFinder(grid);
        var path = new List<Point>();

        Assert.True(finder.TryFindPath(new Point(1, 1), new Point(5, 5), path));

        Assert.Equal(4f * Sqrt2, PathCost(path), 3);
        Assert.All(path, point => Assert.True(grid.IsPassable(point)));
    }

    [Fact]
    public void Blocked_goal_or_start_returns_false()
    {
        var grid = new NavGrid();
        grid.SetBlocked(new Point(10, 10), true);
        var finder = new PathFinder(grid);
        var path = new List<Point>();

        Assert.False(finder.TryFindPath(new Point(2, 2), new Point(10, 10), path));
        Assert.False(finder.TryFindPath(new Point(10, 10), new Point(2, 2), path));
        Assert.Empty(path);
    }

    [Fact]
    public void Wall_forces_a_detour_and_path_stays_valid()
    {
        var grid = new NavGrid();
        for (var z = 2; z <= 8; z++)
        {
            grid.SetBlocked(new Point(5, z), true);
        }

        var finder = new PathFinder(grid);
        var path = new List<Point>();
        var start = new Point(2, 5);
        var goal = new Point(8, 5);

        Assert.True(finder.TryFindPath(start, goal, path));
        Assert.True(path.Count > 2);
        Assert.True(PathCost(path) > 6f);

        for (var i = 1; i < path.Count; i++)
        {
            Assert.True(grid.HasLineOfSight(path[i - 1], path[i]));
        }
    }

    [Fact]
    public void Diagonal_corner_is_never_clipped()
    {
        var grid = new NavGrid();
        grid.SetBlocked(new Point(5, 4), true);
        grid.SetBlocked(new Point(4, 5), true);

        var finder = new PathFinder(grid);
        var path = new List<Point>();

        Assert.True(finder.TryFindPath(new Point(4, 4), new Point(5, 5), path));
        Assert.True(path.Count > 2);
        Assert.True(PathCost(path) > 2f * Sqrt2);

        for (var i = 1; i < path.Count; i++)
        {
            Assert.True(grid.HasLineOfSight(path[i - 1], path[i]));
        }
    }

    [Fact]
    public void Smoothing_reduces_waypoints_without_crossing_walls()
    {
        var grid = new NavGrid();
        for (var z = 2; z <= 8; z++)
        {
            grid.SetBlocked(new Point(5, z), true);
        }

        var finder = new PathFinder(grid);
        var raw = new List<Point>();
        var smooth = new List<Point>();

        Assert.True(finder.TryFindPath(new Point(2, 5), new Point(8, 5), raw, smooth: false));
        Assert.True(finder.TryFindPath(new Point(2, 5), new Point(8, 5), smooth, smooth: true));

        Assert.True(smooth.Count < raw.Count);

        for (var i = 1; i < smooth.Count; i++)
        {
            Assert.True(grid.HasLineOfSight(smooth[i - 1], smooth[i]));
        }
    }

    [Fact]
    public void Paths_are_within_thirty_percent_of_the_optimal_distance()
    {
        var rng = new Random(2026);

        for (var test = 0; test < 25; test++)
        {
            var grid = CreateRandomGrid(rng, 30, 30, blockedChance: 0.22);
            var finder = new PathFinder(grid);

            var start = FindPassable(grid, rng);
            var goal = FindPassable(grid, rng);
            if (start == goal)
            {
                continue;
            }

            var optimal = ReferenceShortestDistance(grid, start, goal);
            var path = new List<Point>();
            var found = finder.TryFindPath(start, goal, path);

            Assert.Equal(optimal is not null, found);

            if (optimal is null)
            {
                continue;
            }

            var cost = PathCost(path);
            Assert.True(
                cost <= optimal.Value * 1.3f + 0.01f,
                $"Path cost {cost} exceeds 1.3 x optimal {optimal.Value} (test {test}).");

            Assert.All(path, point => Assert.True(grid.IsPassable(point)));
            for (var i = 1; i < path.Count; i++)
            {
                Assert.True(grid.HasLineOfSight(path[i - 1], path[i]));
            }
        }
    }

    [Fact]
    public void World_overload_returns_cell_centers()
    {
        var grid = new NavGrid();
        var finder = new PathFinder(grid);
        var path = new List<Vector3>();

        Assert.True(finder.TryFindPath(new Vector3(-56f, 0f, -56f), new Vector3(-36f, 0f, -36f), path));

        Assert.Equal(MapGrid.CellCenter(MapGrid.WorldToCell(new Vector3(-56f, 0f, -56f))), path[0]);
        Assert.Equal(MapGrid.CellCenter(MapGrid.WorldToCell(new Vector3(-36f, 0f, -36f))), path[^1]);
    }

    [Fact]
    public void World_buildings_block_and_unblock_navigation_cells()
    {
        var world = new Core.World.World(DefinitionRegistry.CreateDefault(), seed: 5);
        var cell = MapGrid.WorldToCell(new Vector3(-3f, 0f, -3f));

        world.SpawnBuilding(PlayerId.Player, BuildingId.Nexus, Vector3.Zero);
        Assert.False(world.Navigation.IsPassable(cell));

        var nexus = world.Entities[0];
        world.Enqueue(new DespawnCommand(nexus.Id));
        world.Step(0.05f);

        Assert.True(world.Navigation.IsPassable(cell));
    }

    private static float PathCost(List<Point> path)
    {
        var cost = 0f;
        for (var i = 1; i < path.Count; i++)
        {
            var dx = path[i].X - path[i - 1].X;
            var dz = path[i].Y - path[i - 1].Y;
            cost += MathF.Sqrt(dx * dx + dz * dz);
        }

        return cost;
    }

    private static NavGrid CreateRandomGrid(Random rng, int width, int height, double blockedChance)
    {
        var grid = new NavGrid(width, height);
        for (var z = 0; z < height; z++)
        {
            for (var x = 0; x < width; x++)
            {
                if (rng.NextDouble() < blockedChance)
                {
                    grid.SetBlocked(new Point(x, z), true);
                }
            }
        }

        return grid;
    }

    private static Point FindPassable(NavGrid grid, Random rng)
    {
        while (true)
        {
            var cell = new Point(rng.Next(grid.Width), rng.Next(grid.Height));
            if (grid.IsPassable(cell))
            {
                return cell;
            }
        }
    }

    private static float? ReferenceShortestDistance(NavGrid grid, Point start, Point goal)
    {
        var width = grid.Width;
        var height = grid.Height;
        var distance = new float[width * height];
        Array.Fill(distance, float.PositiveInfinity);
        var open = new PriorityQueue<int, float>();
        var startIndex = start.Y * width + start.X;
        var goalIndex = goal.Y * width + goal.X;
        distance[startIndex] = 0f;
        open.Enqueue(startIndex, 0f);

        while (open.TryDequeue(out var current, out _))
        {
            if (current == goalIndex)
            {
                return distance[current];
            }

            var x = current % width;
            var z = current / width;

            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    var nx = x + dx;
                    var nz = z + dz;
                    var next = new Point(nx, nz);
                    if (!grid.IsPassable(next))
                    {
                        continue;
                    }

                    if (dx != 0 && dz != 0 &&
                        (!grid.IsPassable(x + dx, z) || !grid.IsPassable(x, z + dz)))
                    {
                        continue;
                    }

                    var step = dx != 0 && dz != 0 ? Sqrt2 : 1f;
                    var candidate = distance[current] + step;
                    var nextIndex = nz * width + nx;
                    if (candidate + 1e-4f >= distance[nextIndex])
                    {
                        continue;
                    }

                    distance[nextIndex] = candidate;
                    open.Enqueue(nextIndex, candidate);
                }
            }
        }

        return null;
    }
}
