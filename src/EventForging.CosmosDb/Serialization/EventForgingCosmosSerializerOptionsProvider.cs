using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;

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

internal sealed class EventForgingCosmosSerializer : CosmosSerializer
{
    private readonly ISerializerOptionsProvider _serializerOptionsProvider;

    public EventForgingCosmosSerializer(ISerializerOptionsProvider serializerOptionsProvider)
    {
        _serializerOptionsProvider = serializerOptionsProvider;
    }

    private JsonSerializerOptions JsonSerializerOptions => _serializerOptionsProvider.Get();

    public override T FromStream<T>(Stream stream)
    {
        var sr = new StreamReader(stream);
        var json = sr.ReadToEnd();
        stream.Dispose();
        var o = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions);
        return o;
    }

    public override Stream ToStream<T>(T input)
    {
        var json = JsonSerializer.Serialize(input, JsonSerializerOptions);
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }
}
