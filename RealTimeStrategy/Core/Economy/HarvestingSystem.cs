using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.Navigation;
using RealTimeStrategy.Core.World;
using WorldState = RealTimeStrategy.Core.World.World;

namespace RealTimeStrategy.Core.Economy;

/// <summary>
/// Advances the harvester trip loop: walk to the node, mine a full cargo, walk to the nearest
/// drop-off, unload, repeat. Purely simulation — no wall clock, graphics or orders besides the
/// ones it starts through <see cref="World.MoveUnit"/>.
/// </summary>
public sealed class HarvestingSystem
{
    private const int MaxStandPointRing = 5;

    /// <summary>
    /// Extra distance allowed on top of the stand-point clearance. Path following stops within
    /// its own arrival tolerance, so a harvester can halt slightly outside the nominal radius.
    /// </summary>
    private const float ArrivalSlack = 0.5f;

    /// <summary>Assigns a node to a harvester and starts the trip (depositing first if loaded).</summary>
    public static void Assign(WorldState world, Entity harvester, EntityId node)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(harvester);

        var state = harvester.Harvester!;
        state.Node = node;
        state.Timer = 0f;

        if (state.Cargo > 0)
        {
            BeginReturn(world, harvester, state);
            return;
        }

        state.Phase = HarvestPhase.ToNode;

        if (world.TryGetEntity(node, out var nodeEntity) &&
            !TryMoveTo(world, harvester, nodeEntity))
        {
            state.Reset();
        }
    }

    /// <summary>Sends a loaded harvester to the nearest drop-off, keeping the node for the next trip.</summary>
    public static bool TryReturnCargo(WorldState world, Entity harvester)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(harvester);

        var state = harvester.Harvester;
        if (state is null || !state.HasNode)
        {
            return false;
        }

        return BeginReturn(world, harvester, state);
    }

    public void Step(WorldState world, float deltaSeconds)
    {
        if (deltaSeconds <= 0f)
        {
            return;
        }

        foreach (var entity in world.Entities)
        {
            if (entity.Kind != EntityKind.Unit || !entity.IsAlive || entity.Harvester is null)
            {
                continue;
            }

            var state = entity.Harvester;

            if (state.Phase == HarvestPhase.None)
            {
                continue;
            }

            Entity? node = null;

            // The node only matters while the unit is still working it; a depleted node is removed
            // before the loaded harvester reaches the drop-off.
            if (state.Phase is HarvestPhase.ToNode or HarvestPhase.Harvesting)
            {
                if (!world.TryGetEntity(state.Node, out node) || !node.IsResourceNode || !node.IsAlive)
                {
                    Abort(world, entity, state);
                    continue;
                }
            }

            switch (state.Phase)
            {
                case HarvestPhase.ToNode:
                    UpdateTravel(world, entity, state, node!, HarvestPhase.Harvesting);
                    break;

                case HarvestPhase.Harvesting:
                    UpdateHarvesting(world, entity, state, node!, deltaSeconds);
                    break;

                case HarvestPhase.ToDropOff:
                    UpdateDropOffTravel(world, entity, state);
                    break;

                case HarvestPhase.Depositing:
                    UpdateDepositing(world, entity, state, deltaSeconds);
                    break;
            }
        }
    }

    /// <summary>
    /// Point a unit should stand on to work on a target: the closest free spot outside the
    /// target's blocked cell, preferring the side it approaches from.
    /// </summary>
    public static bool TryFindStandPoint(WorldState world, Entity mover, Entity target, out Vector3 point)
    {
        var clearance = target.Radius + mover.Radius + EconomyDef.InteractionPadding;
        var targetPosition = target.Position;
        var away = mover.Position - targetPosition;
        away.Y = 0f;

        if (away.LengthSquared() < 1e-4f)
        {
            away = new Vector3(1f, 0f, 0f);
        }
        else
        {
            away = Vector3.Normalize(away);
        }

        var direct = targetPosition + away * clearance;
        if (IsFree(world, direct, target))
        {
            point = direct;
            return true;
        }

        var center = MapGrid.WorldToCell(targetPosition);
        var best = Vector3.Zero;
        var bestDistanceSquared = float.PositiveInfinity;

        for (var ring = 1; ring <= MaxStandPointRing; ring++)
        {
            for (var dz = -ring; dz <= ring; dz++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring)
                    {
                        continue;
                    }

                    var cell = new Point(center.X + dx, center.Y + dz);
                    if (!world.Navigation.IsPassable(cell))
                    {
                        continue;
                    }

                    var candidate = MapGrid.CellCenter(cell);
                    var distanceSquared = Vector3.DistanceSquared(candidate, mover.Position);
                    if (distanceSquared < bestDistanceSquared)
                    {
                        bestDistanceSquared = distanceSquared;
                        best = candidate;
                    }
                }
            }

            if (bestDistanceSquared < float.PositiveInfinity)
            {
                point = best;
                return true;
            }
        }

        point = default;
        return false;
    }

    private static bool IsFree(WorldState world, Vector3 point, Entity target)
    {
        var cell = MapGrid.WorldToCell(point);
        return world.Navigation.IsPassable(cell) && !cell.Equals(MapGrid.WorldToCell(target.Position));
    }

    private static bool TryMoveTo(WorldState world, Entity mover, Entity target)
    {
        if (IsWithinReach(mover, target))
        {
            world.StopUnit(mover.Id);
            return true;
        }

        return TryFindStandPoint(world, mover, target, out var standPoint) &&
               world.MoveUnit(mover.Id, standPoint);
    }

    private static bool IsWithinReach(Entity mover, Entity target)
    {
        var reach = target.Radius + mover.Radius + EconomyDef.InteractionPadding + ArrivalSlack;
        return Vector3.DistanceSquared(mover.Position, target.Position) <= reach * reach;
    }

    private static void UpdateTravel(WorldState world, Entity entity, HarvesterState state, Entity node, HarvestPhase arrivalPhase)
    {
        if (IsWithinReach(entity, node))
        {
            world.StopUnit(entity.Id);
            state.Phase = arrivalPhase;
            state.Timer = 0f;
            return;
        }

        if (entity.Move is { HasDestination: true })
        {
            return;
        }

        if (!TryMoveTo(world, entity, node))
        {
            Abort(world, entity, state);
        }
    }

    private static void UpdateHarvesting(WorldState world, Entity entity, HarvesterState state, Entity node, float deltaSeconds)
    {
        if (!IsWithinReach(entity, node))
        {
            state.Phase = HarvestPhase.ToNode;
            state.Timer = 0f;

            if (!TryMoveTo(world, entity, node))
            {
                Abort(world, entity, state);
            }

            return;
        }

        state.Timer += deltaSeconds;
        if (state.Timer < EconomyDef.HarvestSeconds)
        {
            return;
        }

        var taken = Math.Min(EconomyDef.HarvesterCapacity, node.Reserve);
        node.Reserve -= taken;
        state.Cargo = taken;
        state.Timer = 0f;

        if (node.Reserve <= 0)
        {
            // Depletion removal is immediate; reassigning idle harvesters is T-021.
            node.PendingRemoval = true;
        }

        if (!BeginReturn(world, entity, state))
        {
            Abort(world, entity, state);
        }
    }

    private static void UpdateDropOffTravel(WorldState world, Entity entity, HarvesterState state)
    {
        if (!world.TryGetEntity(state.DropOff, out var dropOff) || !dropOff.IsAlive || !IsDropOff(dropOff))
        {
            if (!BeginReturn(world, entity, state))
            {
                Abort(world, entity, state);
            }

            return;
        }

        if (IsWithinReach(entity, dropOff))
        {
            world.StopUnit(entity.Id);
            state.Phase = HarvestPhase.Depositing;
            state.Timer = 0f;
            return;
        }

        if (entity.Move is { HasDestination: true })
        {
            return;
        }

        if (!TryMoveTo(world, entity, dropOff))
        {
            Abort(world, entity, state);
        }
    }

    private static void UpdateDepositing(WorldState world, Entity entity, HarvesterState state, float deltaSeconds)
    {
        state.Timer += deltaSeconds;
        if (state.Timer < EconomyDef.DepositSeconds)
        {
            return;
        }

        if (state.Cargo > 0)
        {
            world.GetPlayer(entity.Owner).AddAlloy(state.Cargo);
            state.Cargo = 0;
        }

        state.Timer = 0f;

        if (world.TryGetEntity(state.Node, out var node) &&
            node.IsResourceNode &&
            node.IsAlive &&
            node.Reserve > 0)
        {
            state.Phase = HarvestPhase.ToNode;

            if (!TryMoveTo(world, entity, node))
            {
                Abort(world, entity, state);
            }

            return;
        }

        Abort(world, entity, state);
    }

    private static bool BeginReturn(WorldState world, Entity entity, HarvesterState state)
    {
        if (!world.TryFindNearestDropOff(entity.Owner, entity.Position, out var dropOff))
        {
            return false;
        }

        state.DropOff = dropOff.Id;
        state.Phase = HarvestPhase.ToDropOff;
        state.Timer = 0f;

        if (IsWithinReach(entity, dropOff))
        {
            world.StopUnit(entity.Id);
            state.Phase = HarvestPhase.Depositing;
            return true;
        }

        return TryMoveTo(world, entity, dropOff);
    }

    private static void Abort(WorldState world, Entity entity, HarvesterState state)
    {
        world.StopUnit(entity.Id);
        state.Reset();
    }

    private static bool IsDropOff(Entity entity) =>
        entity.Kind == EntityKind.Building && entity.BuildingDef is { IsDropOff: true };
}
