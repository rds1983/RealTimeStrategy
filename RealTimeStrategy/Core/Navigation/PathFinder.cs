using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.World;

namespace RealTimeStrategy.Core.Navigation;

/// <summary>
/// 8-way A* over <see cref="NavGrid"/>. Diagonal moves require both adjacent orthogonal cells
/// to be passable, and the resulting path is simplified with grid line-of-sight checks.
/// </summary>
public sealed class PathFinder
{
    private const float Epsilon = 1e-4f;

    private static readonly int[] DeltaX = { 1, 1, 0, -1, -1, -1, 0, 1 };
    private static readonly int[] DeltaZ = { 0, 1, 1, 1, 0, -1, -1, -1 };
    private static readonly float Sqrt2 = MathF.Sqrt(2f);

    private readonly NavGrid _grid;
    private readonly float[] _gScore;
    private readonly int[] _cameFrom;
    private readonly byte[] _state;
    private readonly PriorityQueue<int, float> _open = new();
    private readonly List<Point> _route = new();
    private readonly List<Point> _cellOutput = new();

    public PathFinder(NavGrid grid)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _gScore = new float[grid.Width * grid.Height];
        _cameFrom = new int[grid.Width * grid.Height];
        _state = new byte[grid.Width * grid.Height];
    }

    public bool TryFindPath(Point start, Point goal, List<Point> path, bool smooth = true)
    {
        ArgumentNullException.ThrowIfNull(path);
        path.Clear();

        if (!_grid.IsPassable(start) || !_grid.IsPassable(goal))
        {
            return false;
        }

        if (start == goal)
        {
            path.Add(start);
            return true;
        }

        ResetSearch();

        var startIndex = Index(start);
        var goalIndex = Index(goal);

        _gScore[startIndex] = 0f;
        _state[startIndex] = 1;
        _open.Enqueue(startIndex, Heuristic(start, goal));

        var found = false;

        while (_open.TryDequeue(out var current, out _))
        {
            if (_state[current] == 2)
            {
                continue;
            }

            if (current == goalIndex)
            {
                found = true;
                break;
            }

            _state[current] = 2;
            var x = current % _grid.Width;
            var z = current / _grid.Width;

            for (var direction = 0; direction < DeltaX.Length; direction++)
            {
                var dx = DeltaX[direction];
                var dz = DeltaZ[direction];
                var nx = x + dx;
                var nz = z + dz;
                var next = new Point(nx, nz);

                if (!_grid.IsPassable(next))
                {
                    continue;
                }

                if (dx != 0 && dz != 0 &&
                    (!_grid.IsPassable(x + dx, z) || !_grid.IsPassable(x, z + dz)))
                {
                    continue;
                }

                var stepCost = dx != 0 && dz != 0 ? Sqrt2 : 1f;
                var tentative = _gScore[current] + stepCost;
                var nextIndex = Index(next);

                if (tentative + Epsilon >= _gScore[nextIndex])
                {
                    continue;
                }

                _cameFrom[nextIndex] = current;
                _gScore[nextIndex] = tentative;
                _state[nextIndex] = 1;
                _open.Enqueue(nextIndex, tentative + Heuristic(next, goal));
            }
        }

        if (!found)
        {
            return false;
        }

        _route.Clear();
        for (var node = goalIndex; node != -1; node = _cameFrom[node])
        {
            _route.Add(new Point(node % _grid.Width, node / _grid.Width));
        }

        _route.Reverse();

        if (smooth)
        {
            SmoothInto(path);
        }
        else
        {
            path.AddRange(_route);
        }

        return true;
    }

    /// <summary>Convenience overload returning cell centers; the start cell is included.</summary>
    public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> path, bool smooth = true)
    {
        ArgumentNullException.ThrowIfNull(path);
        path.Clear();

        if (!TryFindPath(MapGrid.WorldToCell(start), MapGrid.WorldToCell(goal), _cellOutput, smooth))
        {
            return false;
        }

        foreach (var cell in _cellOutput)
        {
            path.Add(MapGrid.CellCenter(cell));
        }

        return true;
    }

    private void ResetSearch()
    {
        Array.Fill(_gScore, float.PositiveInfinity);
        Array.Fill(_cameFrom, -1);
        Array.Clear(_state, 0, _state.Length);
        _open.Clear();
        _route.Clear();
    }

    private void SmoothInto(List<Point> output)
    {
        if (_route.Count <= 2)
        {
            output.AddRange(_route);
            return;
        }

        output.Add(_route[0]);
        var anchor = 0;

        for (var i = 2; i < _route.Count; i++)
        {
            if (_grid.HasLineOfSight(_route[anchor], _route[i]))
            {
                continue;
            }

            var previous = _route[i - 1];
            if (output[^1] != previous)
            {
                output.Add(previous);
            }

            anchor = i - 1;
        }

        if (output[^1] != _route[^1])
        {
            output.Add(_route[^1]);
        }
    }

    private int Index(Point cell) => cell.Y * _grid.Width + cell.X;

    private static float Heuristic(Point from, Point to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dz = Math.Abs(to.Y - from.Y);
        return dx + dz + (Sqrt2 - 2f) * Math.Min(dx, dz);
    }
}
