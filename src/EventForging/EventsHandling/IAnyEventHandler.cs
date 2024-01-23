namespace EventForging.EventsHandling;

public interface IAnyEventHandler : IEventHandler
{
    Task HandleAsync(object e, EventInfo ei, CancellationToken cancellationToken);
}
