using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.World;

namespace RealTimeStrategy.Core.Orders;

/// <summary>
/// A player or AI order. Only the fields relevant to <see cref="Type"/> are used; use the
/// factories instead of constructing ambiguous values by hand.
/// </summary>
public readonly record struct Order
{
    public OrderType Type { get; init; }

    public Vector3 Point { get; init; }

    public EntityId Target { get; init; }

    public BuildingId Building { get; init; }

    public UnitId Unit { get; init; }

    public static Order Move(Vector3 point) => new() { Type = OrderType.Move, Point = point };

    public static Order AttackMove(Vector3 point) => new() { Type = OrderType.AttackMove, Point = point };

    public static Order Attack(EntityId target) => new() { Type = OrderType.Attack, Target = target };

    public static Order Hold() => new() { Type = OrderType.Hold };

    public static Order Stop() => new() { Type = OrderType.Stop };

    public static Order Harvest(EntityId node) => new() { Type = OrderType.Harvest, Target = node };

    public static Order ReturnCargo() => new() { Type = OrderType.ReturnCargo };

    public static Order Build(BuildingId building, Vector3 point) => new()
    {
        Type = OrderType.Build,
        Building = building,
        Point = point,
    };

    public static Order Produce(UnitId unit) => new() { Type = OrderType.Produce, Unit = unit };

    public static Order SetRally(Vector3 point) => new() { Type = OrderType.SetRally, Point = point };
}
