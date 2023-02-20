using Microsoft.Extensions.Hosting;

namespace EventForging.EventStore;

internal sealed class EventStoreEventForgingHostedService : IHostedService, IAsyncDisposable
{
    private bool _stopRequested;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;
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
