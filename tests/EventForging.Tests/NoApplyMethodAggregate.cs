namespace EventForging.Tests;

public class NoApplyMethodAggregate : IEventForged
{
    public NoApplyMethodAggregate()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

    public static NoApplyMethodAggregate CreateApplyingAnEvent()
    {
        var a = new NoApplyMethodAggregate();
        a.Events.Apply(new DummyEvent());
        return a;
    }

    public sealed record DummyEvent;
}
