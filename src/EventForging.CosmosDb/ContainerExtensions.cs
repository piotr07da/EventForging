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
            ResponseMessage response;
            try
            {
                response = await streamIterator.ReadNextAsync(cancellationToken);
            }
            catch (CosmosException ex)
            {
                EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(ex);
                throw;
            }

            using (response)
            {
                onPageEntry(response);
                EventForgingCosmosDbTooManyRequestsException.ThrowIfTooManyRequests(response);

                if (!response.IsSuccessStatusCode)
                {
                    throw new EventForgingException($"Cosmos DB query failed with status code {response.StatusCode} and message: {response.ErrorMessage}");
                }

                await foreach (var containerItem in response.Content.DeserializeStreamAsync(deserializationOptions, cancellationToken))
                {
                    yield return containerItem;
                }
            }
        }
    }
}
