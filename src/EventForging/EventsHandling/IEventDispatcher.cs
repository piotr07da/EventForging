namespace EventForging.EventsHandling;

public interface IEventDispatcher
{
    Task DispatchAsync(string subscriptionName, object eventData, EventInfo eventInfo, CancellationToken cancellationToken);
    Task DispatchAsync(string subscriptionName, ReceivedEventItem receivedEvent, CancellationToken cancellationToken);
    Task DispatchAsync(string subscriptionName, IReadOnlyList<ReceivedEventItem> receivedEvents, CancellationToken cancellationToken);
}
