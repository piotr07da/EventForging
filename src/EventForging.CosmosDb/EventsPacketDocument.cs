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
        StreamId = streamId;
        Id = events.First().EventId.ToString();
        DocumentType = DocumentType.EventsPacket;
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

    public IReadOnlyList<Event> Events { get; set; } = Array.Empty<Event>();

    public EventMetadata? Metadata { get; set; }

    public EventsPacketDocument Clone()
    {
        return new EventsPacketDocument
        {
            StreamId = StreamId,
            Id = Id,
            DocumentType = DocumentType,
            Events = Events.Select(e => e.Clone()).ToArray(),
            Metadata = Metadata,
        };
    }

    public class Event
    {
        public Guid EventId { get; set; }

        public long EventNumber { get; set; }

        public string? EventType { get; set; }

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
