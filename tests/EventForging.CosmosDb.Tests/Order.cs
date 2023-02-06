namespace EventForging.CosmosDb.Tests;

public class Order : IEventForged
{
    public Order()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

    public Guid Id { get; private set; }

    public static Order Raise(Guid id)
    {
        var order = new Order();
        var events = order.Events;
        events.Apply(new OrderRaisedEvent(id));
        return order;
    }

    private void Apply(OrderRaisedEvent e)
    {
        Id = e.OrderId;
    }
}
