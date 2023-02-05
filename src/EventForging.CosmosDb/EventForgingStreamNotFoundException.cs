using System;

namespace EventForging.CosmosDb;

public class EventForgingStreamNotFoundException : EventForgingException
{
    public EventForgingStreamNotFoundException(string streamId, Exception innerException)
        : base($"Stream {streamId} not found.", innerException)
    {
    }
}
