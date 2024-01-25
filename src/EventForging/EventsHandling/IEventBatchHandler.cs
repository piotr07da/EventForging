namespace EventForging.EventsHandling;

public interface IEventBatchHandler : IEventHandler
{
    Task HandleAsync(ReceivedEventsBatch batch, CancellationToken cancellationToken);
}
