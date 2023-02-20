using System.Collections.Concurrent;
using EventForging.EventsHandling;

namespace EventForging.DatabaseIntegrationTests.Common;

public sealed class UserEventHandlers :
    IEventHandler<UserRegistered>,
    IEventHandler<UserNamed>
{
    private static readonly IDictionary<Guid, Action<object, EventInfo>> _onEventHandled = new ConcurrentDictionary<Guid, Action<object, EventInfo>>();

    public string SubscriptionName => "TestSubscription";

    public Task Handle(UserRegistered e, EventInfo ei, CancellationToken cancellationToken)
    {
        ReadModel.AddOrUpdate(e.UserId, u => u.Id = e.UserId);
        if (_onEventHandled.TryGetValue(e.UserId, out var onEventHandled)) onEventHandled(e, ei);
        return Task.CompletedTask;
    }

    public Task Handle(UserNamed e, EventInfo ei, CancellationToken cancellationToken)
    {
        ReadModel.AddOrUpdate(e.UserId, u => u.Name = e.Name);
        if (_onEventHandled.TryGetValue(e.UserId, out var onEventHandled)) onEventHandled(e, ei);
        return Task.CompletedTask;
    }

    public static void RegisterOnEventHandled(Guid userId, Action<object, EventInfo> callback)
    {
        _onEventHandled[userId] = callback;
    }
}
