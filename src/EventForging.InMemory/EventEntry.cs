using EventForging.Serialization;

namespace EventForging.InMemory;

internal sealed record EventEntry(Guid Id, long Version, string Type, object Data, EventMetadata Metadata);
