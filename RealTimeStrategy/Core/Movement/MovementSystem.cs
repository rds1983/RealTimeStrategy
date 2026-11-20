using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Navigation;
using RealTimeStrategy.Core.World;
using WorldState = RealTimeStrategy.Core.World.World;

namespace RealTimeStrategy.Core.Movement;

/// <summary>
/// Path following and local avoidance. Units accelerate toward waypoints, separate from nearby
/// units, re-path when blocked or stuck, and turn toward their velocity.
/// </summary>
public sealed class MovementSystem
{
    private const float ArrivalTolerance = 0.3f;
    private const float WaypointTolerance = 0.5f;
    private const float StuckCheckInterval = 0.5f;
    private const float StuckDistance = 0.1f;
    private const float SeparationWeight = 0.9f;
    private const float IdleSeparationWeight = 0.35f;
    private const float TurnRate = 10f;
    private const float DestinationInset = 0.01f;

    private readonly NavGrid _navigation;
    private readonly PathFinder _pathFinder;
    private readonly List<Vector3> _path = new();
    private readonly List<Entity> _neighbors = new();

    public MovementSystem(NavGrid navigation)
    {
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        _pathFinder = new PathFinder(navigation);
    }

    public bool SetDestination(Entity entity, Vector3 destination)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!entity.IsAlive || entity.Kind != EntityKind.Unit)
        {
            return false;
        }

        destination = ClampDestination(destination);

        if (!_pathFinder.TryFindPath(entity.Position, destination, _path))
        {
            return false;
        }

        _path[^1] = destination;
        entity.Move ??= new MoveState();
        entity.Move.SetPath(destination, _path, entity.Position);
        return true;
    }

    public void Stop(Entity entity)
    {
        entity.Move?.ClearDestination();
    }

    public void Step(WorldState world, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        foreach (var entity in world.Entities)
        {
            if (entity.Kind != EntityKind.Unit || !entity.IsAlive || entity.Move is null)
            {
                continue;
            }

            UpdateUnit(world, entity, deltaSeconds);
        }
    }

    private void UpdateUnit(WorldState world, Entity entity, float deltaSeconds)
    {
        var state = entity.Move!;

        if (entity.HoldPosition)
        {
            state.Velocity = Vector3.Zero;
            return;
        }

        var speed = entity.Speed;
        var desired = Vector3.Zero;

        while (state.HasDestination)
        {
            if (state.WaypointIndex >= state.Path.Count)
            {
                state.ClearDestination();
                break;
            }

            var waypoint = state.Path[state.WaypointIndex];
            if (!_navigation.IsPassable(MapGrid.WorldToCell(waypoint)))
            {
                if (!TryRepath(entity, state))
                {
                    state.ClearDestination();
                    break;
                }

                continue;
            }

            var toWaypoint = waypoint - entity.Position;
            toWaypoint.Y = 0f;
            var distanceSquared = toWaypoint.LengthSquared();
            var isLast = state.WaypointIndex == state.Path.Count - 1;
            var tolerance = isLast ? ArrivalTolerance : WaypointTolerance;

            if (distanceSquared <= tolerance * tolerance)
            {
                if (isLast)
                {
                    state.ClearDestination();
                    break;
                }

                state.WaypointIndex++;
                continue;
            }

            desired = Vector3.Normalize(toWaypoint) * speed;
            break;
        }

        var separation = ComputeSeparation(world, entity);
        var maxSpeed = state.HasDestination ? speed : speed * 0.5f;
        desired += separation * (maxSpeed * (state.HasDestination ? SeparationWeight : IdleSeparationWeight));

        if (desired.LengthSquared() > maxSpeed * maxSpeed)
        {
            desired = Vector3.Normalize(desired) * maxSpeed;
        }

        var acceleration = MathF.Max(3f, speed * 5f);
        var velocityDelta = desired - state.Velocity;
        var maxDelta = acceleration * deltaSeconds;
        if (velocityDelta.LengthSquared() > maxDelta * maxDelta)
        {
            velocityDelta = Vector3.Normalize(velocityDelta) * maxDelta;
        }

        state.Velocity += velocityDelta;
        if (state.Velocity.LengthSquared() > speed * speed)
        {
            state.Velocity = Vector3.Normalize(state.Velocity) * speed;
        }

        var next = entity.Position + state.Velocity * deltaSeconds;
        next.Y = 0f;
        next.X = Math.Clamp(next.X, -MapGrid.HalfSize, MapGrid.HalfSize);
        next.Z = Math.Clamp(next.Z, -MapGrid.HalfSize, MapGrid.HalfSize);

        if (!_navigation.IsPassable(MapGrid.WorldToCell(next)))
        {
            var xOnly = new Vector3(next.X, 0f, entity.Position.Z);
            var zOnly = new Vector3(entity.Position.X, 0f, next.Z);

            if (_navigation.IsPassable(MapGrid.WorldToCell(xOnly)))
            {
                next = xOnly;
            }
            else if (_navigation.IsPassable(MapGrid.WorldToCell(zOnly)))
            {
                next = zOnly;
            }
            else
            {
                next = entity.Position;
                state.Velocity = Vector3.Zero;
                if (state.HasDestination && !TryRepath(entity, state))
                {
                    state.ClearDestination();
                }
            }
        }

        entity.Position = next;

        var velocity = state.Velocity;
        if (velocity.LengthSquared() > 0.01f)
        {
            var targetFacing = MathF.Atan2(-velocity.Z, velocity.X);
            var turn = MathHelper.WrapAngle(targetFacing - entity.Facing);
            var maxTurn = TurnRate * deltaSeconds;
            entity.Facing = MathHelper.WrapAngle(entity.Facing + Math.Clamp(turn, -maxTurn, maxTurn));
        }

        if (state.HasDestination)
        {
            state.StuckTimer += deltaSeconds;
            if (state.StuckTimer >= StuckCheckInterval)
            {
                var moved = Vector3.Distance(entity.Position, state.ProgressAnchor);
                state.ProgressAnchor = entity.Position;
                state.StuckTimer = 0f;

                if (moved < StuckDistance && !TryRepath(entity, state))
                {
                    state.ClearDestination();
                }
            }
        }
    }

    private Vector3 ComputeSeparation(WorldState world, Entity entity)
    {
        var range = entity.Radius + 1.8f;
        world.Spatial.Query(entity.Position, range, _neighbors);
        var push = Vector3.Zero;

        foreach (var other in _neighbors)
        {
            if (ReferenceEquals(other, entity) || other.Kind != EntityKind.Unit || !other.IsAlive)
            {
                continue;
            }

            var away = entity.Position - other.Position;
            away.Y = 0f;
            var distanceSquared = away.LengthSquared();
            if (distanceSquared <= 1e-6f)
            {
                away = new Vector3(1f, 0f, 0f);
                distanceSquared = 1f;
            }

            var distance = MathF.Sqrt(distanceSquared);
            var comfortable = entity.Radius + other.Radius + 0.8f;
            if (distance >= comfortable)
            {
                continue;
            }

            push += away / distance * ((comfortable - distance) / comfortable);
        }

        if (push.LengthSquared() > 1f)
        {
            push = Vector3.Normalize(push);
        }

        return push;
    }

    private bool TryRepath(Entity entity, MoveState state)
    {
        if (!_pathFinder.TryFindPath(entity.Position, state.Destination, _path))
        {
            return false;
        }

        _path[^1] = state.Destination;
        state.SetPath(state.Destination, _path, entity.Position);
        return true;
    }

    private static Vector3 ClampDestination(Vector3 destination)
    {
        var limit = MapGrid.HalfSize - DestinationInset;
        return new Vector3(
            Math.Clamp(destination.X, -limit, limit),
            0f,
            Math.Clamp(destination.Z, -limit, limit));
    }
}
