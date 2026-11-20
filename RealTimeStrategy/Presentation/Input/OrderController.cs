using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Orders;
using RealTimeStrategy.Core.World;
using WorldState = RealTimeStrategy.Core.World.World;

namespace RealTimeStrategy.Presentation.Input;

/// <summary>
/// Translates selection + cursor context into orders. Keeps the attack-move arming state so the
/// input layer can show a crosshair and divert the next left click.
/// </summary>
public sealed class OrderController
{
    private readonly SelectionModel _selection;

    public OrderController(SelectionModel selection)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
    }

    public bool AttackMoveArmed { get; private set; }

    public void ArmAttackMove() => AttackMoveArmed = true;

    public void CancelAttackMove() => AttackMoveArmed = false;

    /// <summary>
    /// Right-click context. Units attack enemy targets or move to the point/target; a selected
    /// structure stores a rally point. Returns the number of orders accepted.
    /// </summary>
    public int IssueContext(WorldState world, Entity? target, Vector3? groundPoint, bool queue)
    {
        var primary = _selection.Primary(world);
        if (primary is null)
        {
            return 0;
        }

        if (primary.Kind == EntityKind.Building)
        {
            var point = groundPoint ?? target?.Position;
            if (point is null)
            {
                return 0;
            }

            return world.TryIssueOrder(primary.Id, Order.SetRally(point.Value))
                ? 1
                : 0;
        }

        var ordered = 0;

        foreach (var id in _selection.SelectedIds)
        {
            if (!world.TryGetEntity(id, out var unit) ||
                unit.Kind != EntityKind.Unit ||
                !unit.IsAlive)
            {
                continue;
            }

            Order? order = null;

            if (target is not null &&
                target.IsAlive &&
                target.Owner != unit.Owner)
            {
                order = Order.Attack(target.Id);
            }
            else if (groundPoint is not null)
            {
                order = Order.Move(groundPoint.Value);
            }
            else if (target is not null)
            {
                order = Order.Move(target.Position);
            }

            if (order is not null && world.TryIssueOrder(id, order.Value, replace: !queue))
            {
                ordered++;
            }
        }

        return ordered;
    }

    public int IssueAttackMove(WorldState world, Vector3 point, bool queue)
    {
        var ordered = 0;

        foreach (var id in _selection.SelectedIds)
        {
            if (!world.TryGetEntity(id, out var unit) ||
                unit.Kind != EntityKind.Unit ||
                !unit.IsAlive)
            {
                continue;
            }

            if (world.TryIssueOrder(id, Order.AttackMove(point), replace: !queue))
            {
                ordered++;
            }
        }

        return ordered;
    }

    public int IssueStop(WorldState world) => IssueSimple(world, Order.Stop());

    public int IssueHold(WorldState world) => IssueSimple(world, Order.Hold());

    private int IssueSimple(WorldState world, Order order)
    {
        var ordered = 0;

        foreach (var id in _selection.SelectedIds)
        {
            if (!world.TryGetEntity(id, out var unit) ||
                unit.Kind != EntityKind.Unit ||
                !unit.IsAlive)
            {
                continue;
            }

            if (world.TryIssueOrder(id, order))
            {
                ordered++;
            }
        }

        return ordered;
    }
}
