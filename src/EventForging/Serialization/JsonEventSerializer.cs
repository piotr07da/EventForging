using System;
using System.Text;
using System.Text.Json;

namespace EventForging.Serialization;

internal sealed class JsonEventSerializer : IEventSerializer
{
    private readonly IEventForgingSerializationConfiguration _serializationConfiguration;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;

    public JsonEventSerializer(IEventForgingSerializationConfiguration serializationConfiguration, IJsonSerializerOptionsProvider serializerOptionsProvider)
    {
        _serializationConfiguration = serializationConfiguration ?? throw new ArgumentNullException(nameof(serializationConfiguration));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
    }

    private JsonSerializerOptions SerializerOptions => _serializerOptionsProvider.Get();

    public byte[] SerializeToBytes(object eventData, out string eventName)
    {
        var serializedEventData = SerializeValue(eventData);
        var bytesSerializedEventData = Encoding.UTF8.GetBytes(serializedEventData);
        eventName = GetEventName(eventData.GetType());
        return bytesSerializedEventData;
    }

    public string SerializeToString(object eventData, out string eventName)
    {
        var serializedEventData = SerializeValue(eventData);
        eventName = GetEventName(eventData.GetType());
        return serializedEventData;
    }

    public object DeserializeFromBytes(string eventName, byte[] serializedEventData)
    {
        var jsonSerializedEventData = Encoding.UTF8.GetString(serializedEventData);
        return DeserializeFromString(eventName, jsonSerializedEventData);
    }

    public object DeserializeFromString(string eventName, string serializedEventData)
    {
        var ed = DeserializeEventData(eventName, serializedEventData);
        return ed;
    }

    private string SerializeValue(object value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    private object DeserializeEventData(string eventName, string jsonSerializedEventData)
    {
        var eventType = TryGetEventType(eventName);
        return JsonSerializer.Deserialize(jsonSerializedEventData, eventType, SerializerOptions)!;
    }

    private string GetEventName(Type eventType)
    {
        var mappers = _serializationConfiguration.EventTypeNameMappers;
        foreach (var mapper in mappers)
        {
            var name = mapper.TryGetName(eventType);
            if (name != null)
            {
                return name;
            }
        }

        throw new EventForgingException($"Event type name not found for event of CLR type '{eventType.FullName}'.");
    }

    private Type TryGetEventType(string eventName)
    {
        var mappers = _serializationConfiguration.EventTypeNameMappers;
        foreach (var mapper in mappers)
        {
            var t = mapper.TryGetType(eventName);
            if (t != null)
            {
                return t;
            }
        }

        throw new EventForgingException($"Event CLR type not found for event '{eventName}'.");
    }
}
