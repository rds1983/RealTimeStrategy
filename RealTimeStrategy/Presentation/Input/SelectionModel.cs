using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.World;
using RealTimeStrategy.Presentation.Camera;
using WorldState = RealTimeStrategy.Core.World.World;

namespace RealTimeStrategy.Presentation.Input;

/// <summary>
/// Current selection and numbered control groups. Selection is stored as entity ids and pruned
/// as entities die, so control groups survive deaths without dangling references.
/// </summary>
public sealed class SelectionModel
{
    private readonly List<EntityId> _selected = new();
    private readonly List<Entity> _query = new();
    private readonly Dictionary<int, List<EntityId>> _groups = new();

    public IReadOnlyList<EntityId> SelectedIds => _selected;

    public int Count => _selected.Count;

    public Entity? Primary(WorldState world)
    {
        Entity? firstEntity = null;

        foreach (var id in _selected)
        {
            if (!world.TryGetEntity(id, out var entity) || !entity.IsAlive)
            {
                continue;
            }

            firstEntity ??= entity;
            if (entity.Kind == EntityKind.Unit)
            {
                return entity;
            }
        }

        return firstEntity;
    }

    public void Clear() => _selected.Clear();

    public void Select(Entity? entity, bool additive)
    {
        if (!additive)
        {
            _selected.Clear();
        }

        if (entity is null || !entity.IsAlive)
        {
            return;
        }

        if (!_selected.Contains(entity.Id))
        {
            _selected.Add(entity.Id);
        }
    }

    /// <summary>Box selection only ever picks units, matching the design.</summary>
    public void SelectRectangle(
        WorldState world,
        CameraView camera,
        Rectangle rectangle,
        int viewportWidth,
        int viewportHeight,
        bool additive)
    {
        Picking.QueryUnitsInRectangle(
            world,
            camera,
            rectangle,
            viewportWidth,
            viewportHeight,
            _query);

        if (!additive)
        {
            _selected.Clear();
        }

        foreach (var entity in _query)
        {
            if (!_selected.Contains(entity.Id))
            {
                _selected.Add(entity.Id);
            }
        }
    }

    public void Prune(WorldState world)
    {
        for (var i = _selected.Count - 1; i >= 0; i--)
        {
            if (!world.TryGetEntity(_selected[i], out var entity) || !entity.IsAlive)
            {
                _selected.RemoveAt(i);
            }
        }
    }

    /// <summary>Copies the current selection into group 1–9.</summary>
    public void AssignGroup(int group)
    {
        if (group is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(group), group, "Control groups are 1–9.");
        }

        _groups[group] = new List<EntityId>(_selected);
    }

    /// <summary>Returns the lowest control-group number containing the entity, if any.</summary>
    public bool TryGetFirstGroup(EntityId id, out int group)
    {
        var best = int.MaxValue;

        foreach (var pair in _groups)
        {
            if (pair.Key < best && pair.Value.Contains(id))
            {
                best = pair.Key;
            }
        }

        group = best == int.MaxValue ? 0 : best;
        return best != int.MaxValue;
    }

    /// <summary>Replaces the selection with the surviving members of a control group.</summary>
    public bool RecallGroup(int group, WorldState world)
    {
        if (!_groups.TryGetValue(group, out var ids))
        {
            return false;
        }

        _selected.Clear();
        foreach (var id in ids)
        {
            if (world.TryGetEntity(id, out var entity) && entity.IsAlive)
            {
                _selected.Add(id);
            }
        }

        return true;
    }
}
