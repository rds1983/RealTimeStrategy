using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.World;
using WorldState = RealTimeStrategy.Core.World.World;

namespace RealTimeStrategy.Core.Orders;

/// <summary>
/// Validates, queues and advances unit orders. Move-like orders delegate pathing to
/// <see cref="World.MoveUnit"/>; Stop/Hold clear movement immediately. Combat, harvesting and
/// production order types are defined but rejected until their systems land.
/// </summary>
public sealed class OrderExecutor
{
    internal bool CanIssue(WorldState world, Entity entity, Order order)
    {
        if (!entity.IsAlive || entity.Kind != EntityKind.Unit || entity.Orders is null)
        {
            return false;
        }

        return order.Type switch
        {
            OrderType.Move or OrderType.AttackMove => IsValidPoint(order.Point),

            OrderType.Attack => world.TryGetEntity(order.Target, out var target) &&
                                target.IsAlive &&
                                target.Id != entity.Id &&
                                target.Owner != entity.Owner,

            OrderType.Stop or OrderType.Hold => true,

            // Harvest/Build/Produce/SetRally are enabled by T-018/T-021/T-024.
            _ => false,
        };
    }

    internal bool Issue(WorldState world, Entity entity, Order order, bool replace)
    {
        if (!CanIssue(world, entity, order))
        {
            return false;
        }

        switch (order.Type)
        {
            case OrderType.Stop:
                ClearAndStop(world, entity);
                entity.HoldPosition = false;
                return true;

            case OrderType.Hold:
                ClearAndStop(world, entity);
                entity.HoldPosition = true;
                return true;
        }

        entity.HoldPosition = false;

        if (replace)
        {
            entity.Orders!.Clear();
            world.StopUnit(entity.Id);
        }

        return entity.Orders!.TryEnqueue(order);
    }

    /// <summary>Starts the head order if it has not begun yet. Called before movement.</summary>
    public void BeginTick(WorldState world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity.Kind != EntityKind.Unit || !entity.IsAlive || entity.Orders is null)
            {
                continue;
            }

            var queue = entity.Orders;

            while (!queue.IsEmpty && !queue.Started)
            {
                var order = queue.Current;

                switch (order.Type)
                {
                    case OrderType.Move:
                    case OrderType.AttackMove:
                        if (!world.MoveUnit(entity.Id, order.Point))
                        {
                            queue.Dequeue();
                            continue;
                        }

                        queue.Started = true;
                        break;

                    case OrderType.Attack:
                        if (!world.TryGetEntity(order.Target, out var target) || !target.IsAlive)
                        {
                            queue.Dequeue();
                            continue;
                        }

                        if (!world.MoveUnit(entity.Id, target.Position))
                        {
                            queue.Dequeue();
                            continue;
                        }

                        queue.Started = true;
                        break;

                    default:
                        queue.Dequeue();
                        continue;
                }

                break;
            }
        }
    }

    /// <summary>Pops completed move-like orders. Called after movement.</summary>
    public void EndTick(WorldState world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity.Kind != EntityKind.Unit || entity.Orders is null)
            {
                continue;
            }

            var queue = entity.Orders;
            if (queue.IsEmpty || !queue.Started)
            {
                continue;
            }

            var order = queue.Current;

            switch (order.Type)
            {
                case OrderType.Move:
                case OrderType.AttackMove:
                    if (entity.Move is null || !entity.Move.HasDestination)
                    {
                        queue.Dequeue();
                    }

                    break;

                case OrderType.Attack:
                    if (!world.TryGetEntity(order.Target, out var target) || !target.IsAlive)
                    {
                        queue.Dequeue();
                    }
                    else if (entity.Move is { HasDestination: false })
                    {
                        // Reach/stop handling is completed by combat targeting in T-028.
                        queue.Started = false;
                    }

                    break;
            }
        }
    }

    private static void ClearAndStop(WorldState world, Entity entity)
    {
        entity.Orders!.Clear();
        world.StopUnit(entity.Id);
        entity.HoldPosition = false;
    }

    private static bool IsValidPoint(Vector3 point) =>
        !float.IsNaN(point.X) &&
        !float.IsNaN(point.Z) &&
        !float.IsInfinity(point.X) &&
        !float.IsInfinity(point.Z) &&
        MapGrid.IsInsideMap(point);
}
