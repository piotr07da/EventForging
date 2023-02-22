using System.Collections.Concurrent;
using EventForging.EventsHandling;

namespace EventForging.DatabaseIntegrationTests.Common;

public sealed class SucceedingUserEventHandlers :
    IEventHandler<UserRegistered>,
    IEventHandler<UserNamed>,
    IEventHandler<UserApproved>
{
    private static readonly IDictionary<Guid, Action<object, EventInfo>> _onEventHandled = new ConcurrentDictionary<Guid, Action<object, EventInfo>>();

    public string SubscriptionName => "TestSubscription";

    public Task Handle(UserRegistered e, EventInfo ei, CancellationToken cancellationToken)
    {
        if (_onEventHandled.TryGetValue(e.UserId, out var onEventHandled)) onEventHandled(e, ei);
        return Task.CompletedTask;
    }

    public Task Handle(UserNamed e, EventInfo ei, CancellationToken cancellationToken)
    {
        if (_onEventHandled.TryGetValue(e.UserId, out var onEventHandled)) onEventHandled(e, ei);
        return Task.CompletedTask;
    }

    public Task Handle(UserApproved e, EventInfo ei, CancellationToken cancellationToken)
    {
        if (_onEventHandled.TryGetValue(e.UserId, out var onEventHandled)) onEventHandled(e, ei);
        return Task.CompletedTask;
    }

    public static void RegisterOnEventHandled(Guid userId, Action<object, EventInfo> callback)
    {
        _onEventHandled[userId] = callback;
    }
}
