using System.Runtime.CompilerServices;
using System.Text.Json;
using EventForging.CosmosDb.Serialization;
using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb;

internal static class ContainerExtensions
{
    public static async IAsyncEnumerable<ContainerItem> IterateAsync(this Container container, QueryDefinition queryDefinition, QueryRequestOptions requestOptions, JsonSerializerOptions deserializationOptions, Action<ResponseMessage> onPageEntry, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var streamIterator = container.GetItemQueryStreamIterator(queryDefinition, requestOptions: requestOptions);
        while (streamIterator.HasMoreResults)
        {
            using var response = await streamIterator.ReadNextAsync(cancellationToken);

            onPageEntry(response!);

            await foreach (var containerItem in response.Content.DeserializeStreamAsync(deserializationOptions, cancellationToken))
            {
                yield return containerItem;
            }
        }
    }
}
