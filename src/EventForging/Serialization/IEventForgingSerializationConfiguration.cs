namespace EventForging.Serialization;

public interface IEventForgingSerializationConfiguration
{
    IEventTypeNameMapper[] EventTypeNameMappers { get; }

    void SetEventTypeNameMappers(params IEventTypeNameMapper[] mappers);
}
