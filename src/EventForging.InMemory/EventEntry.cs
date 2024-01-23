using EventForging.Serialization;

namespace EventForging.InMemory;

internal sealed record EventEntry(string StreamId, Guid Id, long Version, string Type, DateTime Timestamp, object Data, EventMetadata Metadata);
