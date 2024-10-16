using System.Text.Json;
using System.Text.Json.Serialization;
using EventForging.Serialization;

namespace EventForging.CosmosDb;

internal sealed class EventsPacketDocument : IDocument
{
    public EventsPacketDocument()
    {
    }

    public EventsPacketDocument(string streamId, IReadOnlyList<Event> events, EventMetadata metadata)
    {
        var firstEvent = events.First();

        StreamId = streamId;
        Id = firstEvent.EventId.ToString();
        DocumentType = DocumentType.EventsPacket;
        EventNumber = firstEvent.EventNumber;
        Events = events;
        Metadata = metadata;
    }

    public string? StreamId { get; set; }

    public string? Id { get; set; }

    public DocumentType DocumentType { get; set; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }

    public long EventNumber { get; set; }

    public IReadOnlyList<Event> Events { get; set; } = Array.Empty<Event>();

    public EventMetadata? Metadata { get; set; }

    public EventsPacketDocument Clone()
    {
        return new EventsPacketDocument
        {
            StreamId = StreamId,
            Id = Id,
            DocumentType = DocumentType,
            EventNumber = EventNumber,
            Events = Events.Select(e => e.Clone()).ToArray(),
            Metadata = Metadata,
        };
    }

    public sealed class Event
    {
        public Event()
        {
        }

        public Event(Guid eventId, long eventNumber, string eventType, JsonElement data)
        {
            EventId = eventId;
            EventNumber = eventNumber;
            EventType = eventType;
            Data = data;
        }

        public Guid EventId { get; set; }

        public long EventNumber { get; set; }

        public string EventType { get; set; } = string.Empty;

        public object? Data { get; set; }

        public Event Clone()
        {
            return new Event
            {
                EventId = EventId,
                EventNumber = EventNumber,
                EventType = EventType,
                Data = Data,
            };
        }
    }
}
