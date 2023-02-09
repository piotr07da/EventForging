namespace EventForging.DatabaseIntegrationTests.Common;

internal sealed class EventDatabaseReadCallback : IEventDatabaseReadCallback
{
    public List<object> Events { get; } = new();

    public void OnBegin()
    {
    }

    public void OnRead(params object[] events)
    {
        Events.AddRange(events);
    }

    public void OnEnd()
    {
    }
}
