using RealTimeStrategy.Core.Orders;

namespace RealTimeStrategy.Core.World;

public sealed record IssueOrderCommand(EntityId Entity, Order Order, bool Replace) : WorldCommand;
