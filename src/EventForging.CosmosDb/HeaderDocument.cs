using System.Text.Json.Serialization;

namespace EventForging.CosmosDb;

internal sealed class HeaderDocument : IDocument
{
    public HeaderDocument()
    {
    }

    public HeaderDocument(string streamId)
    {
        Id = CreateId(streamId);
        StreamId = streamId;
        DocumentType = DocumentType.Header;

        Version = -1;
    }

    public string? StreamId { get; set; }

    public string? Id { get; set; }

    public DocumentType DocumentType { get; set; }

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }

    [JsonPropertyName("_ts")]
    public long Timestamp { get; set; }

    public int Version { get; set; }

    public static string CreateId(string streamId) => $"header@{streamId}";
}
