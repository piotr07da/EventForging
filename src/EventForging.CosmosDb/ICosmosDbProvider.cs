using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb;

internal interface ICosmosDbProvider
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task DisposeAsync(CancellationToken cancellationToken = default);
    Container GetAggregateContainer<TAggregate>();
    Container GetLeaseContainer(string databaseName);
    Container GetContainer(string databaseName, string containerName);
}
