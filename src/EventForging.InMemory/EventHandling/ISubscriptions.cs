using EventForging.EventsHandling;

namespace EventForging.InMemory.EventHandling;

internal interface ISubscriptions
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    void Send(string subscriptionName, object eventData, EventInfo eventInfo);
}
