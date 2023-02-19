using EventForging.EventsHandling;

namespace EventForging.DatabaseIntegrationTests.Common;

public sealed class UserEventHandlers :
    IEventHandler<UserRegistered>,
    IEventHandler<UserNamed>
{
    public string SubscriptionName => "TestSubscription";

    public Task Handle(UserRegistered e, EventInfo ei, CancellationToken cancellationToken)
    {
        ReadModel.AddOrUpdate(e.UserId, u => u.Id = e.UserId);
        return Task.CompletedTask;
    }

    public Task Handle(UserNamed e, EventInfo ei, CancellationToken cancellationToken)
    {
        ReadModel.AddOrUpdate(e.UserId, u => u.Name = e.Name);
        return Task.CompletedTask;
    }
}
