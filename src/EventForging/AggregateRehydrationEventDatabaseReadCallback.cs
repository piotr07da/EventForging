namespace EventForging;

internal sealed class AggregateRehydrationEventDatabaseReadCallback : IEventDatabaseReadCallback
{
    private readonly EventApplier _eventApplier;

    public AggregateRehydrationEventDatabaseReadCallback(IEventForged aggregate)
    {
        Aggregate = aggregate;
        _eventApplier = EventApplier.CreateFor(Aggregate);
    }

    public IEventForged Aggregate { get; }
    public long AppliedEventCount { get; private set; }
    public bool Rehydrated => AppliedEventCount > 0;

    public void OnBegin()
    {
    }

    public void OnRead(params object[] events)
    {
        var appliedEventCount = _eventApplier.ApplyEvents(events, false);

        AppliedEventCount += appliedEventCount;
    }

    public void OnEnd()
    {
        Aggregate.ConfigureAggregateMetadata(md => md.ReadVersion = AppliedEventCount - 1);
    }
}
