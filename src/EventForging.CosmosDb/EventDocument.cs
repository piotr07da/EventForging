using System;
using System.Text.Json.Serialization;
using EventForging.Serialization;

namespace EventForging.CosmosDb;

internal sealed class EventDocument : IDocument
{
    public EventDocument()
    {
    }

    public EventDocument(string streamId, int eventNumber, object data, EventMetadata metadata)
    {
        Id = $"{eventNumber}@{streamId}";
        StreamId = streamId;
        EventId = Guid.NewGuid();
        DocumentType = DocumentType.Event;

        EventNumber = eventNumber;
        Data = data;
        Metadata = metadata;
    }

    public string? Id { get; set; }

    public string? StreamId { get; set; }

    public DocumentType DocumentType { get; set; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }

    public Guid EventId { get; set; }

    public int EventNumber { get; set; }

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
            EventId = EventId,
            EventNumber = EventNumber,
            EventType = EventType,
            Data = Data,
            Metadata = Metadata,
        };
    }
}
