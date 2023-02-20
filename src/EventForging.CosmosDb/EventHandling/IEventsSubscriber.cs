namespace EventForging.CosmosDb.EventHandling;

internal interface IEventsSubscriber
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
