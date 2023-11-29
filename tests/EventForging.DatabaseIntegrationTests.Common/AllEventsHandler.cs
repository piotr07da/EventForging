using EventForging.EventsHandling;

namespace EventForging.DatabaseIntegrationTests.Common;

public class AllEventsHandler : IAllEventsHandler
{
    private static readonly HashSet<Guid> _handledEvents = new();

    public string SubscriptionName => "TestSubscription";

    public Task HandleAsync(object e, EventInfo ei, CancellationToken cancellationToken)
    {
        _handledEvents.Add(ei.EventId);
        return Task.CompletedTask;
    }

    public static bool Handled(Guid eventId)
    {
        return _handledEvents.Contains(eventId);
    }
}
