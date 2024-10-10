using System.Text.Json;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb.Serialization;

internal sealed class EventForgingSystemTextJsonSerializer : CosmosSerializer
{
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;

    public EventForgingSystemTextJsonSerializer(IJsonSerializerOptionsProvider serializerOptionsProvider)
    {
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
    }

    private JsonSerializerOptions JsonSerializerOptions => _serializerOptionsProvider.Get();

    public override T FromStream<T>(Stream stream)
    {
        var sr = new StreamReader(stream);
        var json = sr.ReadToEnd();
        stream.Dispose();
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions)!;
    }

    public override Stream ToStream<T>(T input)
    {
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(input, JsonSerializerOptions);
        return new MemoryStream(jsonBytes);
    }
}
