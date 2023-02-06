using System.Text.Json;
using System.Text.Json.Serialization;
using EventForging.Serialization;

namespace EventForging.CosmosDb.Serialization;

internal sealed class EventForgingCosmosSerializerOptionsProvider : ISerializerOptionsProvider
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(), },
    };

    public JsonSerializerOptions Get() => _jsonSerializerOptions;
}
