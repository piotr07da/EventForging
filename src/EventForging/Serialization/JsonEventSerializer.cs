using System.Text;
using System.Text.Json;

namespace EventForging.Serialization;

public sealed class JsonEventSerializer : IEventSerializer
{
    private const string CheckSetEventTypeNameMappersErrorMessage = "Check if proper event type name mappers were registered using SetEventTypeNameMappers(...) method while registering EventForging using AddEventForging(c => ...).";

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
        var serializedEventData = JsonSerializer.Serialize(eventData, SerializerOptions);
        var bytesSerializedEventData = Encoding.UTF8.GetBytes(serializedEventData);
        eventName = GetEventName(eventData.GetType());
        return bytesSerializedEventData;
    }

    public string SerializeToString(object eventData, out string eventName)
    {
        var serializedEventData = JsonSerializer.Serialize(eventData, SerializerOptions);
        eventName = GetEventName(eventData.GetType());
        return serializedEventData;
    }

    public JsonElement SerializeToJsonElement(object eventData, out string eventName)
    {
        var serializedEventData = JsonSerializer.SerializeToElement(eventData, SerializerOptions);
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

    private object DeserializeEventData(string eventName, string jsonSerializedEventData)
    {
        var eventType = TryGetEventType(eventName);
        try
        {
            return JsonSerializer.Deserialize(jsonSerializedEventData, eventType, SerializerOptions)!;
        }
        catch (Exception ex)
        {
            throw new EventForgingException($"Cannot deserialize event of type '{eventType.FullName}' from the following string:{Environment.NewLine}{jsonSerializedEventData}{Environment.NewLine}Please, create a unit test and check if the '{eventType.FullName}' type is possible to be deserialized from the specified json using System.Text.Json.JsonSerializer.", ex);
        }
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

        throw new EventForgingException($"Event type name not found for event of type '{eventType.FullName}'. {CheckSetEventTypeNameMappersErrorMessage}");
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

        throw new EventForgingException($"Event type not found for event '{eventName}'. {CheckSetEventTypeNameMappersErrorMessage}");
    }
}
