using System.Text.Json;
using System.Text.Json.Serialization;
using EventForging.Serialization;

namespace EventForging.CosmosDb.Serialization;

internal sealed class CosmosJsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(), },
    };

    public JsonSerializerOptions Get() => _jsonSerializerOptions;
}
