using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;
using RealTimeStrategy.Core.Movement;
using RealTimeStrategy.Core.Navigation;
using RealTimeStrategy.Core.Orders;

namespace RealTimeStrategy.Core.World;

/// <summary>
/// Headless deterministic simulation. Commands are queued and applied at the start of a tick;
/// entity removal is deferred until the end of the tick. No wall clock, graphics or input.
/// </summary>
public sealed class World
{
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private readonly DefinitionRegistry _definitions;
    private readonly MovementSystem _movement;
    private readonly OrderExecutor _orders = new();
    private readonly List<Entity> _entities = new();
    private readonly Dictionary<EntityId, Entity> _byId = new();
    private readonly List<WorldCommand> _commands = new();
    private readonly Dictionary<PlayerId, PlayerState> _players = new();

    private int _nextEntityId = 1;

    public World(DefinitionRegistry definitions, int seed)
    {
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        Rng = new Random(seed);
        Spatial = new SpatialGrid(MapGrid.MapSize, MapGrid.CellSize);
        _movement = new MovementSystem(Navigation);

        _players[PlayerId.Player] = new PlayerState(PlayerId.Player, FactionId.Concord);
        _players[PlayerId.Enemy] = new PlayerState(PlayerId.Enemy, FactionId.Null);
    }

    public DefinitionRegistry Definitions => _definitions;

    public Random Rng { get; }

    public IReadOnlyList<Entity> Entities => _entities;

    public IReadOnlyDictionary<PlayerId, PlayerState> Players => _players;

    /// <summary>Proximity index rebuilt at the end of every <see cref="Step"/>.</summary>
    public SpatialGrid Spatial { get; }

    /// <summary>Structure passability grid; updated as buildings spawn and are removed.</summary>
    public NavGrid Navigation { get; } = new();

    public long TickCount { get; private set; }

    public PlayerState GetPlayer(PlayerId id) => _players.TryGetValue(id, out var player)
        ? player
        : throw new KeyNotFoundException($"Unknown player '{id}'.");

    public bool TryGetEntity(EntityId id, out Entity entity) => _byId.TryGetValue(id, out entity!);

    public Entity GetEntity(EntityId id) => _byId.TryGetValue(id, out var entity)
        ? entity
        : throw new KeyNotFoundException($"Unknown entity '{id}'.");

    public Entity SpawnUnit(PlayerId owner, UnitId unitId, Vector3 position)
    {
        var def = _definitions.GetUnit(unitId);
        var entity = new Entity(
            new EntityId(_nextEntityId++),
            owner,
            EntityKind.Unit,
            def,
            null,
            ClampToMap(position));

        AddEntity(entity);
        GetPlayer(owner).AddSupplyUsed(def.Supply);
        return entity;
    }

    public Entity SpawnBuilding(PlayerId owner, BuildingId buildingId, Vector3 position)
    {
        var def = _definitions.GetBuilding(buildingId);
        var entity = new Entity(
            new EntityId(_nextEntityId++),
            owner,
            EntityKind.Building,
            null,
            def,
            ClampToMap(position));

        AddEntity(entity);
        GetPlayer(owner).AddSupplyCap(def.SupplyProvided);
        SetBuildingFootprint(def, entity.Position, blocked: true);
        return entity;
    }

    public void Enqueue(WorldCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
    }

    /// <summary>Orders a unit to move to a world position. Returns false if no path exists.</summary>
    public bool MoveUnit(EntityId id, Vector3 destination)
    {
        if (!_byId.TryGetValue(id, out var entity) || entity.Kind != EntityKind.Unit || !entity.IsAlive)
        {
            return false;
        }

        return _movement.SetDestination(entity, destination);
    }

    public bool StopUnit(EntityId id)
    {
        if (!_byId.TryGetValue(id, out var entity))
        {
            return false;
        }

        _movement.Stop(entity);
        return true;
    }

    /// <summary>
    /// Validates and enqueues an order command. The order is applied at the next tick start;
    /// invalid orders are rejected immediately without touching the entity.
    /// </summary>
    public bool TryIssueOrder(EntityId id, Order order, bool replace = true)
    {
        if (!_byId.TryGetValue(id, out var entity) || !_orders.CanIssue(this, entity, order))
        {
            return false;
        }

        if (!replace && entity.Orders!.Count + CountPendingOrders(id) >= OrderQueue.Capacity)
        {
            return false;
        }

        Enqueue(new IssueOrderCommand(id, order, replace));
        return true;
    }

    private int CountPendingOrders(EntityId id)
    {
        var count = 0;
        foreach (var command in _commands)
        {
            if (command is IssueOrderCommand issue && issue.Entity == id && !issue.Replace)
            {
                count++;
            }
        }

        return count;
    }

    public void Step(float deltaSeconds)
    {
        if (deltaSeconds < 0 || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "Delta time must be finite and non-negative.");
        }

        foreach (var entity in _entities)
        {
            entity.BeginTick();
        }

        ApplyCommands();
        RemoveDeadEntities();
        Spatial.Rebuild(_entities);
        _orders.BeginTick(this);
        _movement.Step(this, deltaSeconds);
        _orders.EndTick(this);
        RemoveDeadEntities();
        Spatial.Rebuild(_entities);
        TickCount++;
    }

    public WorldSnapshot CreateSnapshot()
    {
        var entities = new EntitySnapshot[_entities.Count];
        for (var i = 0; i < _entities.Count; i++)
        {
            var entity = _entities[i];
            entities[i] = new EntitySnapshot(
                entity.Id,
                entity.Owner,
                entity.Kind,
                entity.DefinitionId,
                entity.Position,
                entity.Health,
                entity.Radius);
        }

        var players = new[]
        {
            SnapshotPlayer(_players[PlayerId.Player]),
            SnapshotPlayer(_players[PlayerId.Enemy]),
        };

        return new WorldSnapshot(TickCount, entities, players);
    }

    /// <summary>Deterministic FNV-1a hash of players and entities in stable order.</summary>
    public ulong ComputeChecksum()
    {
        var hash = FnvOffset;

        hash = Mix(hash, (ulong)TickCount);
        hash = Mix(hash, (ulong)_entities.Count);

        foreach (var entity in _entities)
        {
            hash = Mix(hash, (ulong)entity.Id.Value);
            hash = Mix(hash, (ulong)entity.Owner);
            hash = Mix(hash, (ulong)entity.Kind);
            hash = Mix(hash, (ulong)entity.DefinitionId);
            hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(entity.Position.X));
            hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(entity.Position.Y));
            hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(entity.Position.Z));
            hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(entity.Health));
            hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(entity.Radius));
            hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(entity.Facing));

            if (entity.Move is { } move)
            {
                hash = Mix(hash, move.HasDestination ? 1UL : 0UL);
                hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(move.Velocity.X));
                hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(move.Velocity.Y));
                hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(move.Velocity.Z));
                hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(move.Destination.X));
                hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(move.Destination.Z));
            }

            hash = Mix(hash, entity.HoldPosition ? 1UL : 0UL);

            if (entity.Orders is { } orders)
            {
                hash = Mix(hash, (ulong)orders.Count);
                foreach (var order in orders.Orders)
                {
                    hash = Mix(hash, (ulong)order.Type);
                    hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(order.Point.X));
                    hash = Mix(hash, (ulong)BitConverter.SingleToUInt32Bits(order.Point.Z));
                    hash = Mix(hash, (ulong)order.Target.Value);
                    hash = Mix(hash, (ulong)order.Building);
                    hash = Mix(hash, (ulong)order.Unit);
                }
            }
        }

        foreach (var playerId in new[] { PlayerId.Player, PlayerId.Enemy })
        {
            var player = _players[playerId];
            hash = Mix(hash, (ulong)player.Id);
            hash = Mix(hash, (ulong)player.Faction);
            hash = Mix(hash, (ulong)player.Alloy);
            hash = Mix(hash, (ulong)player.Energy);
            hash = Mix(hash, (ulong)player.SupplyUsed);
            hash = Mix(hash, (ulong)player.SupplyCap);
        }

        return hash;
    }

    private void ApplyCommands()
    {
        foreach (var command in _commands)
        {
            switch (command)
            {
                case SpawnUnitCommand spawnUnit:
                    SpawnUnit(spawnUnit.Owner, spawnUnit.Unit, spawnUnit.Position);
                    break;
                case SpawnBuildingCommand spawnBuilding:
                    SpawnBuilding(spawnBuilding.Owner, spawnBuilding.Building, spawnBuilding.Position);
                    break;
                case DamageCommand damage:
                    if (_byId.TryGetValue(damage.Target, out var target) && damage.Amount > 0)
                    {
                        target.Health = MathF.Max(0f, target.Health - damage.Amount);
                    }
                    break;
                case DespawnCommand despawn:
                    if (_byId.TryGetValue(despawn.Target, out var removed))
                    {
                        removed.PendingRemoval = true;
                    }
                    break;
                case IssueOrderCommand issueOrder:
                    if (_byId.TryGetValue(issueOrder.Entity, out var ordered))
                    {
                        _orders.Issue(this, ordered, issueOrder.Order, issueOrder.Replace);
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported world command '{command.GetType().Name}'.");
            }
        }

        _commands.Clear();
    }

    private void AddEntity(Entity entity)
    {
        _entities.Add(entity);
        _byId.Add(entity.Id, entity);
    }

    private void RemoveDeadEntities()
    {
        for (var i = _entities.Count - 1; i >= 0; i--)
        {
            var entity = _entities[i];
            if (entity.IsAlive)
            {
                continue;
            }

            _entities.RemoveAt(i);
            _byId.Remove(entity.Id);

            var player = GetPlayer(entity.Owner);
            if (entity.Kind == EntityKind.Unit)
            {
                player.AddSupplyUsed(-entity.UnitDef!.Supply);
            }
            else
            {
                player.AddSupplyCap(-entity.BuildingDef!.SupplyProvided);
                SetBuildingFootprint(entity.BuildingDef, entity.Position, blocked: false);
            }
        }
    }

    private static PlayerSnapshot SnapshotPlayer(PlayerState player) => new(
        player.Id,
        player.Faction,
        player.Alloy,
        player.Energy,
        player.SupplyUsed,
        player.SupplyCap);

    private void SetBuildingFootprint(BuildingDef def, Vector3 position, bool blocked)
    {
        var halfWidth = def.Footprint.Width * MapGrid.CellSize * 0.5f;
        var halfHeight = def.Footprint.Height * MapGrid.CellSize * 0.5f;
        var minCell = MapGrid.WorldToCell(new Vector3(
            position.X - halfWidth,
            0f,
            position.Z - halfHeight));

        Navigation.SetFootprint(minCell, def.Footprint.Width, def.Footprint.Height, blocked);
    }

    private static Vector3 ClampToMap(Vector3 position) => new(
        Math.Clamp(position.X, -MapGrid.HalfSize, MapGrid.HalfSize),
        0f,
        Math.Clamp(position.Z, -MapGrid.HalfSize, MapGrid.HalfSize));

    private static ulong Mix(ulong hash, ulong value)
    {
        hash ^= value;
        return hash * FnvPrime;
    }
}
