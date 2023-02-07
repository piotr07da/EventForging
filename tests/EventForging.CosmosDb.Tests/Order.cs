namespace EventForging.CosmosDb.Tests;

public class Order : IEventForged
{
    public Order()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

    public Guid Id { get; private set; }
    public bool Completed { get; private set; }

    public static Order Raise(Guid id)
    {
        var order = new Order();
        var events = order.Events;
        events.Apply(new OrderRaisedEvent(id));
        return order;
    }

    public void Complete()
    {
        Events.Apply(new OrderCompletedEvent(Id));
    }

    private void Apply(OrderRaisedEvent e)
    {
        Id = e.OrderId;
    }

    private void Apply(OrderCompletedEvent e)
    {
        Completed = true;
    }
}

public sealed record OrderRaisedEvent(Guid OrderId);

public sealed record OrderCompletedEvent(Guid OrderId);
