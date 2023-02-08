namespace EventForging.CosmosDb;

internal interface IDocument
{
    string? StreamId { get; set; }
    string? Id { get; set; }
    DocumentType DocumentType { get; }
    string? ETag { get; set; }
    long Timestamp { get; set; }
}
