using System;
using System.Text;
using System.Text.Json;

namespace EventForging.Serialization;

public class EventSerializer : IEventSerializer
{
    private readonly IEventForgingSerializationConfiguration _serializationConfiguration;
    private readonly ISerializerOptionsProvider _serializerOptionsProvider;

    public EventSerializer(IEventForgingSerializationConfiguration serializationConfiguration, ISerializerOptionsProvider serializerOptionsProvider)
    {
        _serializationConfiguration = serializationConfiguration ?? throw new ArgumentNullException(nameof(serializationConfiguration));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
    }

    private JsonSerializerOptions SerializerOptions => _serializerOptionsProvider.Get();

    public (string eventTypeName, byte[] serializedEventData, byte[] serializedEventMetadata) SerializeToBytes(object eventData, EventMetadata eventMetadata)
    {
        var serializedEventData = SerializeValue(eventData);
        var serializedEventMetadata = SerializeValue(eventMetadata);
        var bytesSerializedEventData = Encoding.UTF8.GetBytes(serializedEventData);
        var bytesSerializedEventMetadata = Encoding.UTF8.GetBytes(serializedEventMetadata);
        var eventName = GetEventName(eventData.GetType());
        return (eventName, bytesSerializedEventData, bytesSerializedEventMetadata);
    }

    public (string eventTypeName, string serializedEventData, string serializedEventMetadata) SerializeToString(object eventData, EventMetadata eventMetadata)
    {
        var serializedEventData = SerializeValue(eventData);
        var serializedEventMetadata = SerializeValue(eventMetadata);
        var eventName = GetEventName(eventData.GetType());
        return (eventName, serializedEventData, serializedEventMetadata);
    }

    public (object eventData, EventMetadata eventMetadata) DeserializeFromBytes(string eventTypeName, byte[] serializedEventData, byte[] serializedEventMetadata)
    {
        var jsonSerializedEventData = Encoding.UTF8.GetString(serializedEventData);
        var jsonSerializedEventMetadata = Encoding.UTF8.GetString(serializedEventMetadata);

        return DeserializeFromString(eventTypeName, jsonSerializedEventData, jsonSerializedEventMetadata);
    }

    public (object eventData, EventMetadata eventMetadata) DeserializeFromString(string eventTypeName, string serializedEventData, string serializedEventMetadata)
    {
        var ed = DeserializeEventData(eventTypeName, serializedEventData);
        var emd = DeserializeMetadata(serializedEventMetadata);
        return (ed, emd);
    }

    private string SerializeValue(object value)
    {
        return JsonSerializer.Serialize(value, SerializerOptions);
    }

    private object DeserializeEventData(string eventTypeName, string jsonSerializedEventData)
    {
        var eventType = TryGetEventType(eventTypeName);

        var e = DeserializeEventData(eventType, jsonSerializedEventData);

        return e;
    }

    private object DeserializeEventData(Type eventType, string jsonSerializedEventData)
    {
        var e = JsonSerializer.Deserialize(jsonSerializedEventData, eventType, SerializerOptions);
        return e!;
    }

    private EventMetadata DeserializeMetadata(string jsonSerializedEventMetadata)
    {
        var metadata = JsonSerializer.Deserialize<EventMetadata>(jsonSerializedEventMetadata, SerializerOptions);
        return metadata!;
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
