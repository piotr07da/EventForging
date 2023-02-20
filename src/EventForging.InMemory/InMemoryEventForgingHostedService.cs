using EventForging.InMemory.EventHandling;
using Microsoft.Extensions.Hosting;

namespace EventForging.InMemory;

internal sealed class InMemoryEventForgingHostedService : IHostedService, IAsyncDisposable
{
    private readonly ISubscriptions _subscriptions;

    private bool _stopRequested;

    public InMemoryEventForgingHostedService(ISubscriptions subscriptions)
    {
        _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _subscriptions.StartAsync(cancellationToken).ConfigureAwait(false);

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _subscriptions.StopAsync(cancellationToken);

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
