using Microsoft.Xna.Framework;

namespace RealTimeStrategy.Core.Movement;

/// <summary>Per-unit path and steering state. Units without one have never been ordered to move.</summary>
public sealed class MoveState
{
    internal readonly List<Vector3> Path = new();

    public Vector3 Destination { get; internal set; }

    public bool HasDestination { get; internal set; }

    public int WaypointIndex { get; internal set; }

    public Vector3 Velocity { get; internal set; }

    internal float StuckTimer { get; set; }

    internal Vector3 ProgressAnchor { get; set; }

    public bool IsMoving => HasDestination;

    internal void SetPath(Vector3 destination, List<Vector3> path, Vector3 position)
    {
        Path.Clear();
        Path.AddRange(path);
        Destination = destination;
        HasDestination = true;
        Velocity = Vector3.Zero;
        StuckTimer = 0f;
        ProgressAnchor = position;

        WaypointIndex = 0;
        const float skipSquared = 0.25f;
        while (WaypointIndex < Path.Count - 1 &&
               Vector3.DistanceSquared(position, Path[WaypointIndex]) <= skipSquared)
        {
            WaypointIndex++;
        }
    }

    internal void ClearDestination()
    {
        HasDestination = false;
        Path.Clear();
        WaypointIndex = 0;
        Velocity = Vector3.Zero;
        StuckTimer = 0f;
    }
}
