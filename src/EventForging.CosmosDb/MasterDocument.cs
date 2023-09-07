namespace EventForging.CosmosDb;

internal sealed class MasterDocument
{
    public DocumentType DocumentType { get; set; }

    public string Id => HeaderDocument?.Id ?? EventDocument?.Id ?? EventsPacketDocument?.Id ?? string.Empty;
    public string StreamId => HeaderDocument?.StreamId ?? EventDocument?.StreamId ?? EventsPacketDocument?.StreamId ?? string.Empty;

    public HeaderDocument? HeaderDocument { get; set; }
    public EventDocument? EventDocument { get; set; }
    public EventsPacketDocument? EventsPacketDocument { get; set; }
}
