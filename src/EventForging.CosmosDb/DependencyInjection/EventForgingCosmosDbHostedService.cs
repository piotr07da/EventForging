using Microsoft.Extensions.Hosting;

// ReSharper disable once CheckNamespace
namespace EventForging.CosmosDb;

internal sealed class EventForgingCosmosDbHostedService : IHostedService, IAsyncDisposable
{
    private readonly ICosmosDbProvider _cosmosDbProvider;
    private bool _stopRequested;

    public EventForgingCosmosDbHostedService(
        ICosmosDbProvider cosmosDbProvider
    )
    {
        _cosmosDbProvider = cosmosDbProvider ?? throw new ArgumentNullException(nameof(cosmosDbProvider));
    }


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _cosmosDbProvider.InitializeAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;
        await _cosmosDbProvider.DisposeAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopRequested)
        {
            await StopAsync(CancellationToken.None);
        }
    }
}
