namespace EventForging.EventsHandling;

public interface IEventBatchHandler : IEventHandler
{
    Task HandleAsync(IReadOnlyList<ReceivedEventItem> batch, CancellationToken cancellationToken);
}
