using EventForging.CosmosDb.EventHandling;
using Microsoft.Extensions.Hosting;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbEventForgingHostedService : IHostedService, IAsyncDisposable, IDisposable
{
    private readonly ICosmosDbProvider _cosmosDbProvider;
    private readonly IEventsSubscriber _eventsSubscriber;

    private bool _stopRequested;

    public CosmosDbEventForgingHostedService(
        ICosmosDbProvider cosmosDbProvider,
        IEventsSubscriber eventsSubscriber)
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
        _eventsSubscriber = eventsSubscriber ?? throw new ArgumentNullException(nameof(eventsSubscriber));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _cosmosDbProvider.InitializeAsync(cancellationToken);
        await _eventsSubscriber.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;
        await _cosmosDbProvider.DisposeAsync(cancellationToken);
        await _eventsSubscriber.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopRequested)
        {
            await StopAsync(CancellationToken.None);
        }
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}
