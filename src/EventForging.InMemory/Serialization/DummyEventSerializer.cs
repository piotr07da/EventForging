using EventForging.Serialization;

namespace EventForging.InMemory.Serialization
{
    internal class DummyEventSerializer : IEventSerializer
    {
        public byte[] SerializeToBytes(object eventData, out string eventName)
        {
            throw new NotImplementedException();
        }

        public string SerializeToString(object eventData, out string eventName)
        {
            throw new NotImplementedException();
        }

        public object DeserializeFromBytes(string eventName, byte[] serializedEventData)
        {
            throw new NotImplementedException();
        }

        public object DeserializeFromString(string eventName, string serializedEventData)
        {
            throw new NotImplementedException();
        }
    }
}
