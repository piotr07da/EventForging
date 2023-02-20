namespace EventForging.Serialization;

public class EventMetadata
{
    public EventMetadata(Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties = null)
    {
        ConversationId = conversationId;
        InitiatorId = initiatorId;
        CustomProperties = customProperties == null || customProperties.Count == 0 ? null : customProperties;
    }

    public Guid ConversationId { get; set; }
    public Guid InitiatorId { get; set; }
    public IDictionary<string, string>? CustomProperties { get; set; }
}
