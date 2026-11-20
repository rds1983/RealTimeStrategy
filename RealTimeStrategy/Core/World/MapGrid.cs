using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Core.World;

/// <summary>
/// Map and build-grid geometry shared by simulation and presentation. The map is centered on
/// the origin, 120 x 120 units, with 2 x 2 unit build/navigation cells.
/// </summary>
public static class MapGrid
{
    public const float MapSize = 120f;
    public const float CellSize = 2f;
    public const float HalfSize = MapSize / 2f;
    public const int CellCount = 60;

    public static Point WorldToCell(Vector3 position) => new(
        (int)MathF.Floor((position.X + HalfSize) / CellSize),
        (int)MathF.Floor((position.Z + HalfSize) / CellSize));

    public static Vector3 CellCenter(Point cell) => new(
        -HalfSize + (cell.X + 0.5f) * CellSize,
        0f,
        -HalfSize + (cell.Y + 0.5f) * CellSize);

    public static bool IsInsideMap(Vector3 position) =>
        position.X >= -HalfSize && position.X <= HalfSize &&
        position.Z >= -HalfSize && position.Z <= HalfSize;
}
