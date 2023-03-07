namespace EventForging.Serialization;

public interface IEventTypeNameMapper
{
    Type? TryGetType(string eventName);
    string? TryGetName(Type eventType);
}
