namespace EventForging;

public class AggregateNotFoundEventForgingException : EventForgingException
{
    public AggregateNotFoundEventForgingException(Type aggregateType, string aggregateId)
        : base($"An aggregate of type '{aggregateType.Name}' with Id '{aggregateId}' has not been found (no events found for given Id).")
    {
    }
}
