using System.Text.Json.Serialization;
using EventForging.Serialization;

namespace EventForging.CosmosDb;

internal sealed class EventDocument : IDocument
{
    public EventDocument()
    {
    }

    public EventDocument(string streamId, Guid eventId, long eventNumber, object data, EventMetadata metadata)
    {
        StreamId = streamId;
        Id = eventId.ToString();
        DocumentType = DocumentType.Event;
        EventNumber = eventNumber;
        Data = data;
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

    public string? EventType { get; set; }

    public object? Data { get; set; }

    public EventMetadata? Metadata { get; set; }

    public EventDocument Clone()
    {
        return new EventDocument
        {
            StreamId = StreamId,
            Id = Id,
            DocumentType = DocumentType,
            EventNumber = EventNumber,
            EventType = EventType,
            Data = Data,
            Metadata = Metadata,
        };
    }
}
