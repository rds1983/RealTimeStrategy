using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Core.World;

/// <summary>
/// Uniform spatial hash over the map, using the same 2-unit cells as the build/navigation grid.
/// Rebuilt once per simulation tick; all queries are allocation-free apart from the caller's list.
/// </summary>
public sealed class SpatialGrid
{
    private readonly List<Entity>[] _cells;
    private readonly float _halfSize;

    public SpatialGrid(float mapSize, float cellSize)
    {
        if (mapSize <= 0 || float.IsNaN(mapSize) || float.IsInfinity(mapSize))
        {
            throw new ArgumentOutOfRangeException(nameof(mapSize), mapSize, "Map size must be finite and positive.");
        }

        if (cellSize <= 0 || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be finite and positive.");
        }

        _halfSize = mapSize * 0.5f;
        CellSize = cellSize;
        Width = Math.Max(1, (int)MathF.Ceiling(mapSize / cellSize));
        Height = Width;

        _cells = new List<Entity>[Width * Height];
        for (var i = 0; i < _cells.Length; i++)
        {
            _cells[i] = new List<Entity>(8);
        }
    }

    public float CellSize { get; }

    public int Width { get; }

    public int Height { get; }

    public int Count { get; private set; }

    public void Rebuild(IReadOnlyList<Entity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        foreach (var cell in _cells)
        {
            cell.Clear();
        }

        Count = 0;
        foreach (var entity in entities)
        {
            if (!entity.IsAlive)
            {
                continue;
            }

            _cells[CellIndex(entity.Position)].Add(entity);
            Count++;
        }
    }

    /// <summary>Writes up to <paramref name="results"/>.Length matches; returns the total found.</summary>
    public int Query(Vector3 center, float radius, Span<Entity> results)
    {
        if (radius < 0f || float.IsNaN(radius) || float.IsInfinity(radius))
        {
            return 0;
        }

        var radiusSquared = radius * radius;
        var (minX, minZ, maxX, maxZ) = CellRange(center, radius);
        var found = 0;

        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var cell = _cells[z * Width + x];
                foreach (var entity in cell)
                {
                    if (Vector3.DistanceSquared(center, entity.Position) > radiusSquared)
                    {
                        continue;
                    }

                    if (found < results.Length)
                    {
                        results[found] = entity;
                    }

                    found++;
                }
            }
        }

        return found;
    }

    /// <summary>Clears the list and fills it with entities within the radius. Returns the count.</summary>
    public int Query(Vector3 center, float radius, List<Entity> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        results.Clear();

        if (radius < 0f || float.IsNaN(radius) || float.IsInfinity(radius))
        {
            return 0;
        }

        var radiusSquared = radius * radius;
        var (minX, minZ, maxX, maxZ) = CellRange(center, radius);

        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var cell = _cells[z * Width + x];
                foreach (var entity in cell)
                {
                    if (Vector3.DistanceSquared(center, entity.Position) <= radiusSquared)
                    {
                        results.Add(entity);
                    }
                }
            }
        }

        return results.Count;
    }

    /// <summary>Nearest living entity owned by another player, within the radius.</summary>
    public Entity? FindNearestEnemy(Vector3 center, float maxRadius, PlayerId owner)
    {
        if (maxRadius < 0f || float.IsNaN(maxRadius) || float.IsInfinity(maxRadius))
        {
            return null;
        }

        var bestDistance = maxRadius * maxRadius;
        Entity? best = null;
        var (minX, minZ, maxX, maxZ) = CellRange(center, maxRadius);

        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var cell = _cells[z * Width + x];
                foreach (var entity in cell)
                {
                    if (entity.Owner == owner)
                    {
                        continue;
                    }

                    var distance = Vector3.DistanceSquared(center, entity.Position);
                    if (distance <= bestDistance)
                    {
                        bestDistance = distance;
                        best = entity;
                    }
                }
            }
        }

        return best;
    }

    private (int MinX, int MinZ, int MaxX, int MaxZ) CellRange(Vector3 center, float radius)
    {
        var minX = Math.Clamp((int)MathF.Floor((center.X - radius + _halfSize) / CellSize), 0, Width - 1);
        var maxX = Math.Clamp((int)MathF.Floor((center.X + radius + _halfSize) / CellSize), 0, Width - 1);
        var minZ = Math.Clamp((int)MathF.Floor((center.Z - radius + _halfSize) / CellSize), 0, Height - 1);
        var maxZ = Math.Clamp((int)MathF.Floor((center.Z + radius + _halfSize) / CellSize), 0, Height - 1);
        return (minX, minZ, maxX, maxZ);
    }

    private int CellIndex(Vector3 position)
    {
        var x = Math.Clamp((int)MathF.Floor((position.X + _halfSize) / CellSize), 0, Width - 1);
        var z = Math.Clamp((int)MathF.Floor((position.Z + _halfSize) / CellSize), 0, Height - 1);
        return z * Width + x;
    }
}
