using Microsoft.Xna.Framework;
using RealTimeStrategy.Core.Definitions;

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

        _players[PlayerId.Player] = new PlayerState(PlayerId.Player, FactionId.Concord);
        _players[PlayerId.Enemy] = new PlayerState(PlayerId.Enemy, FactionId.Null);
    }

    public DefinitionRegistry Definitions => _definitions;

    public Random Rng { get; }

    public IReadOnlyList<Entity> Entities => _entities;

    public IReadOnlyDictionary<PlayerId, PlayerState> Players => _players;

    /// <summary>Proximity index rebuilt at the end of every <see cref="Step"/>.</summary>
    public SpatialGrid Spatial { get; }

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
        return entity;
    }

    public void Enqueue(WorldCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
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
