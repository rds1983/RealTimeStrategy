namespace RealTimeStrategy.Core.Orders;

/// <summary>Bounded FIFO of orders for one unit; the head order is executing.</summary>
public sealed class OrderQueue
{
    public const int Capacity = 4;

    private readonly List<Order> _orders = new(Capacity);

    public int Count => _orders.Count;

    public bool IsEmpty => _orders.Count == 0;

    public Order Current => _orders.Count > 0
        ? _orders[0]
        : throw new InvalidOperationException("Order queue is empty.");

    public IReadOnlyList<Order> Orders => _orders;

    public bool Started { get; internal set; }

    public bool TryEnqueue(Order order)
    {
        if (_orders.Count >= Capacity)
        {
            return false;
        }

        _orders.Add(order);
        return true;
    }

    public Order Dequeue()
    {
        var order = Current;
        _orders.RemoveAt(0);
        Started = false;
        return order;
    }

    public void Clear()
    {
        _orders.Clear();
        Started = false;
    }
}
