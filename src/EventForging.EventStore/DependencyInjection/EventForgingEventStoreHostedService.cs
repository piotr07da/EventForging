using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace EventForging.EventStore.DependencyInjection;

internal sealed class EventForgingEventStoreHostedService : IHostedService, IAsyncDisposable
{
    private readonly IEventStoreClientProvider _clientProvider;
    private bool _stopRequested;

    public EventForgingEventStoreHostedService(
        IEventStoreClientProvider clientProvider
    )
    {
        _clientProvider = clientProvider ?? throw new ArgumentNullException(nameof(clientProvider));
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopRequested)
        {
            await StopAsync(CancellationToken.None);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _clientProvider.Initialize();
        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;
        _clientProvider.Dispose();
        await Task.CompletedTask;
    }
}
