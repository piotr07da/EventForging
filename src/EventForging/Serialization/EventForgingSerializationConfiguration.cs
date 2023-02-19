namespace EventForging.Serialization;

public class EventForgingSerializationConfiguration : IEventForgingSerializationConfiguration
{
    public IEventTypeNameMapper[] EventTypeNameMappers { get; private set; } = Array.Empty<IEventTypeNameMapper>();

    public void SetEventTypeNameMappers(params IEventTypeNameMapper[] mappers)
    {
        EventTypeNameMappers = mappers;
    }
}
