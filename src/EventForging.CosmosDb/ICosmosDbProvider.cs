using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb;

internal interface ICosmosDbProvider
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task DisposeAsync(CancellationToken cancellationToken = default);
    Container GetContainer<TAggregate>();
}
