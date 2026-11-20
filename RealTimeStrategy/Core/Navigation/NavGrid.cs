using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Core.Navigation;

/// <summary>
/// 60 × 60 passability grid using the same 2-unit cells as <c>MapGrid</c>. Separate from the
/// spatial hash: this grid answers "can I walk here", not "who is nearby".
/// </summary>
public sealed class NavGrid
{
    private readonly bool[] _blocked;

    public NavGrid(int width = 60, int height = 60)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        }

        Width = width;
        Height = height;
        _blocked = new bool[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    public int BlockedCount { get; private set; }

    public bool IsInside(Point cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

    public bool IsPassable(Point cell) => IsInside(cell) && !_blocked[cell.Y * Width + cell.X];

    public bool IsPassable(int x, int z) => IsPassable(new Point(x, z));

    public void SetBlocked(Point cell, bool blocked)
    {
        if (!IsInside(cell))
        {
            return;
        }

        var index = cell.Y * Width + cell.X;
        if (_blocked[index] == blocked)
        {
            return;
        }

        _blocked[index] = blocked;
        BlockedCount += blocked ? 1 : -1;
    }

    public void SetFootprint(Point minCell, int width, int height, bool blocked)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Footprint must be positive.");
        }

        for (var z = 0; z < height; z++)
        {
            for (var x = 0; x < width; x++)
            {
                SetBlocked(new Point(minCell.X + x, minCell.Y + z), blocked);
            }
        }
    }

    public void ClearBlockers()
    {
        Array.Clear(_blocked, 0, _blocked.Length);
        BlockedCount = 0;
    }

    /// <summary>
    /// Supercover-style line of sight. Diagonal steps are rejected when either adjacent
    /// orthogonal cell is blocked, so paths cannot clip wall corners.
    /// </summary>
    public bool HasLineOfSight(Point from, Point to)
    {
        if (!IsPassable(from) || !IsPassable(to))
        {
            return false;
        }

        var x = from.X;
        var z = from.Y;
        var targetX = to.X;
        var targetZ = to.Y;
        var dx = Math.Abs(targetX - x);
        var dz = Math.Abs(targetZ - z);
        var stepX = Math.Sign(targetX - x);
        var stepZ = Math.Sign(targetZ - z);
        var error = dx - dz;

        while (x != targetX || z != targetZ)
        {
            var e2 = 2 * error;
            var nextX = x;
            var nextZ = z;

            if (e2 > -dz)
            {
                error -= dz;
                nextX += stepX;
            }

            if (e2 < dx)
            {
                error += dx;
                nextZ += stepZ;
            }

            if (nextX != x && nextZ != z)
            {
                if (!IsPassable(nextX, z) || !IsPassable(x, nextZ))
                {
                    return false;
                }
            }

            x = nextX;
            z = nextZ;

            if (!IsPassable(x, z))
            {
                return false;
            }
        }

        return true;
    }
}
