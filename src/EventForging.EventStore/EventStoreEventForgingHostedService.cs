using EventForging.EventStore.EventHandling;
using Microsoft.Extensions.Hosting;

namespace EventForging.EventStore;

internal sealed class EventStoreEventForgingHostedService : IHostedService, IAsyncDisposable
{
    private readonly IEventsSubscriber _eventSubscriber;

    private bool _stopRequested;

    public EventStoreEventForgingHostedService(IEventsSubscriber eventSubscriber)
    {
        _eventSubscriber = eventSubscriber ?? throw new ArgumentNullException(nameof(eventSubscriber));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _eventSubscriber.StartAsync(cancellationToken);
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;
        await _eventSubscriber.StopAsync(cancellationToken);
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopRequested)
        {
            await StopAsync(CancellationToken.None);
        }
    }
}
