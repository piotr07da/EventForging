using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EventForging.CosmosDb.Serialization;

internal static class CosmosStreamDeserializationExtensions
{
    public static async IAsyncEnumerable<ContainerItem> DeserializeStreamAsync(this Stream stream, JsonSerializerOptions deserializationOptions, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var streamRootDocument = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken) ?? throw new InvalidOperationException("Cannot parse response content.");
        foreach (var documentJsonNode in (streamRootDocument["Documents"] ?? throw new InvalidOperationException("Cannot deserialize Documents node from response content.")).AsArray())
        {
            if (documentJsonNode is null)
            {
                continue;
            }

            var documentType = (string?)documentJsonNode["documentType"];

            if (documentType is null)
            {
                continue;
            }

            var containerItem = new ContainerItem(documentType, documentJsonNode, deserializationOptions);
            yield return containerItem;
        }
    }
}
