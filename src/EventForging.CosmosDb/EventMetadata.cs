using System;
using System.Collections.Generic;

namespace EventForging.CosmosDb;

public class EventMetadata
{
    public EventMetadata(Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties = null)
    {
        ConversationId = conversationId;
        InitiatorId = initiatorId;
        CustomProperties = customProperties ?? new Dictionary<string, string>();
    }

    public Guid ConversationId { get; set; }
    public Guid InitiatorId { get; set; }
    public IDictionary<string, string> CustomProperties { get; set; }
}
