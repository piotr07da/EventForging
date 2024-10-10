using System.Text.Json;

namespace EventForging.Serialization;

public interface IEventSerializer
{
    byte[] SerializeToBytes(object eventData, out string eventName);
    string SerializeToString(object eventData, out string eventName);
    JsonElement SerializeToJsonElement(object eventData, out string eventName);
    object DeserializeFromBytes(string eventName, byte[] serializedEventData);
    object DeserializeFromString(string eventName, string serializedEventData);
}
