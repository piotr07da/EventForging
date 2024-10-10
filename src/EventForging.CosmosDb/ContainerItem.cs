using System.Text.Json;
using System.Text.Json.Nodes;

namespace EventForging.CosmosDb;

internal sealed class ContainerItem
{
    private readonly string _documentType;
    private readonly JsonNode _itemJsonNode;
    private readonly JsonSerializerOptions _deserializationOptions;

    public ContainerItem(string documentType, JsonNode documentJsonNode, JsonSerializerOptions deserializationOptions)
    {
        _documentType = documentType;
        _itemJsonNode = documentJsonNode;
        _deserializationOptions = deserializationOptions;
    }

    public bool TryHandleAs<T>(string documentType, out T document)
    {
        if (_documentType == documentType)
        {
            document = _itemJsonNode.Deserialize<T>(_deserializationOptions) ?? throw new InvalidOperationException("Cannot deserialize item.");
            return true;
        }

        document = default!;
        return false;
    }

    public string GetStringValue(string propertyName)
    {
        return _itemJsonNode[propertyName]?.ToString() ?? string.Empty;
    }
}
