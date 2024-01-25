namespace EventForging.EventsHandling;

public interface IEventDispatcher
{
    Task DispatchAsync(string subscriptionName, object eventData, EventInfo eventInfo, CancellationToken cancellationToken);
    Task DispatchAsync(string subscriptionName, ReceivedEvent receivedEvent, CancellationToken cancellationToken);
    Task DispatchAsync(string subscriptionName, ReceivedEventsBatch receivedEventsBatch, CancellationToken cancellationToken);
}
