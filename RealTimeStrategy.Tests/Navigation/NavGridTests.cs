using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Navigation;
using RealTimeStrategy.Core.World;
using Xunit;

namespace RealTimeStrategy.Tests.Navigation;

public sealed class NavGridTests
{
    [Fact]
    public void New_grid_is_open_and_reports_size()
    {
        var grid = new NavGrid();

        Assert.Equal(60, grid.Width);
        Assert.Equal(60, grid.Height);
        Assert.Equal(0, grid.BlockedCount);
        Assert.True(grid.IsPassable(new Point(0, 0)));
        Assert.True(grid.IsPassable(new Point(59, 59)));
        Assert.False(grid.IsPassable(new Point(60, 0)));
    }

    [Fact]
    public void Block_and_clear_updates_count()
    {
        var grid = new NavGrid();

        grid.SetBlocked(new Point(5, 5), true);
        grid.SetBlocked(new Point(5, 5), true);
        grid.SetBlocked(new Point(6, 5), true);

        Assert.Equal(2, grid.BlockedCount);
        Assert.False(grid.IsPassable(new Point(5, 5)));

        grid.SetBlocked(new Point(5, 5), false);
        Assert.Equal(1, grid.BlockedCount);

        grid.ClearBlockers();
        Assert.Equal(0, grid.BlockedCount);
        Assert.True(grid.IsPassable(new Point(5, 5)));
    }

    [Fact]
    public void Footprint_blocks_rectangular_region()
    {
        var grid = new NavGrid();

        grid.SetFootprint(new Point(10, 20), 3, 2, true);

        Assert.Equal(6, grid.BlockedCount);
        Assert.False(grid.IsPassable(new Point(10, 20)));
        Assert.False(grid.IsPassable(new Point(12, 21)));
        Assert.True(grid.IsPassable(new Point(13, 21)));
        Assert.True(grid.IsPassable(new Point(10, 22)));
    }

    [Fact]
    public void Line_of_sight_is_clear_on_open_ground()
    {
        var grid = new NavGrid();

        Assert.True(grid.HasLineOfSight(new Point(2, 3), new Point(20, 3)));
        Assert.True(grid.HasLineOfSight(new Point(2, 3), new Point(12, 13)));
        Assert.True(grid.HasLineOfSight(new Point(7, 7), new Point(7, 7)));
    }

    [Fact]
    public void Line_of_sight_rejects_blocked_cells()
    {
        var grid = new NavGrid();
        grid.SetBlocked(new Point(10, 5), true);

        Assert.False(grid.HasLineOfSight(new Point(2, 5), new Point(20, 5)));
    }

    [Fact]
    public void Line_of_sight_cannot_cut_diagonal_corners()
    {
        var grid = new NavGrid();
        grid.SetBlocked(new Point(5, 4), true);
        grid.SetBlocked(new Point(4, 5), true);

        Assert.False(grid.HasLineOfSight(new Point(4, 4), new Point(5, 5)));
    }
}
