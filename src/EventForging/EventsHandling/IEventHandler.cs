namespace EventForging.EventsHandling;

public interface IEventHandler
{
    string SubscriptionName { get; }
}

public interface IEventHandler<in TEvent> : IEventHandler
{
    Task HandleAsync(TEvent e, EventInfo ei, CancellationToken cancellationToken);
}
