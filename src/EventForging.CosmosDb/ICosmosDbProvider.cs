using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb;

internal interface ICosmosDbProvider
{
    Task InitializeAsync();
    Task DisposeAsync();
    Container GetContainer<TAggregate>();
}
