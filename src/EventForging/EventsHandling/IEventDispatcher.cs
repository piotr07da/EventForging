namespace EventForging.EventsHandling;

public interface IEventDispatcher
{
    Task DispatchAsync(string subscriptionName, object e, EventInfo ei, CancellationToken cancellationToken);
}
