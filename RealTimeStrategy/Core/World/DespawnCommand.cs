namespace RealTimeStrategy.Core.World;

public sealed record DespawnCommand(EntityId Target) : WorldCommand;
