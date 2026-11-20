namespace RealTimeStrategy.Core.World;

public sealed record DamageCommand(EntityId Target, float Amount) : WorldCommand;
