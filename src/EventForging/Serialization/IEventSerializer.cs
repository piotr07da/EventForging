namespace EventForging.Serialization;

public interface IEventSerializer
{
    (string eventTypeName, byte[] serializedEventData, byte[] serializedEventMetadata) SerializeToBytes(object eventData, EventMetadata eventMetadata);
    (string eventTypeName, string serializedEventData, string serializedEventMetadata) SerializeToString(object eventData, EventMetadata eventMetadata);
    (object eventData, EventMetadata eventMetadata) DeserializeFromBytes(string eventTypeName, byte[] serializedEventData, byte[] serializedEventMetadata);
    (object eventData, EventMetadata eventMetadata) DeserializeFromString(string eventTypeName, string serializedEventData, string serializedEventMetadata);
}
