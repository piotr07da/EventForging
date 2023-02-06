﻿namespace EventForging.CosmosDb;

public interface IDocument
{
    string Id { get; set; }
    string StreamId { get; set; }
    DocumentType DocumentType { get; }
    string ETag { get; set; }
    long Timestamp { get; set; }
}