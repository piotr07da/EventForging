using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace EventForging.CosmosDb.DependencyInjection;

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
        await _cosmosDbProvider.InitializeAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;
        await _cosmosDbProvider.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_stopRequested)
        {
            await StopAsync(CancellationToken.None);
        }
    }
}
