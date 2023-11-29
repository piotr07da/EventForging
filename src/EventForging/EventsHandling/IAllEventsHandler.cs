namespace EventForging.EventsHandling;

public interface IAllEventsHandler : IEventHandler
{
    Task HandleAsync(object e, EventInfo ei, CancellationToken cancellationToken);
}
