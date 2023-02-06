using EventForging.Serialization;

namespace EventForging;

public interface IEventForgingSerializationConfiguration
{
    IEventTypeNameMapper[] EventTypeNameMappers { get; }

    void SetEventTypeNameMappers(params IEventTypeNameMapper[] mappers);
}
