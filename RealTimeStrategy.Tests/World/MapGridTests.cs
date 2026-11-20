using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.World;
using Xunit;

namespace RealTimeStrategy.Tests.World;

public sealed class MapGridTests
{
    [Fact]
    public void Map_uses_two_unit_cells()
    {
        Assert.Equal(120f, MapGrid.MapSize);
        Assert.Equal(2f, MapGrid.CellSize);
        Assert.Equal(60, MapGrid.CellCount);
        Assert.Equal(60f, MapGrid.HalfSize);
    }

    [Fact]
    public void World_to_cell_maps_corners_and_center()
    {
        Assert.Equal(new Point(0, 0), MapGrid.WorldToCell(new Vector3(-60f, 0f, -60f)));
        Assert.Equal(new Point(30, 30), MapGrid.WorldToCell(Vector3.Zero));
        Assert.Equal(new Point(59, 59), MapGrid.WorldToCell(new Vector3(59.99f, 0f, 59.99f)));
    }

    [Fact]
    public void Cell_center_round_trips()
    {
        var cell = new Point(12, 41);

        var center = MapGrid.CellCenter(cell);
        var roundTrip = MapGrid.WorldToCell(center);

        Assert.Equal(cell, roundTrip);
        Assert.Equal(-60f + 12.5f * 2f, center.X, 3);
        Assert.Equal(-60f + 41.5f * 2f, center.Z, 3);
    }

    [Fact]
    public void Inside_map_includes_edges_and_rejects_outside()
    {
        Assert.True(MapGrid.IsInsideMap(new Vector3(-60f, 0f, 60f)));
        Assert.True(MapGrid.IsInsideMap(Vector3.Zero));
        Assert.False(MapGrid.IsInsideMap(new Vector3(60.01f, 0f, 0f)));
        Assert.False(MapGrid.IsInsideMap(new Vector3(0f, 0f, -61f)));
    }
}
