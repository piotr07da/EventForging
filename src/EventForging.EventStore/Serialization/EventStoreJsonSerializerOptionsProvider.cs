using System.Text.Json;
using System.Text.Json.Serialization;
using EventForging.Serialization;

namespace EventForging.EventStore.Serialization;

internal sealed class EventStoreJsonSerializerOptionsProvider : IJsonSerializerOptionsProvider
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), },
    };

    public JsonSerializerOptions Get() => _jsonSerializerOptions;
}
