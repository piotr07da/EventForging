namespace EventForging.Tests;

public sealed class SealedClassAggregate : IEventForged
{
    public SealedClassAggregate()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }
}
