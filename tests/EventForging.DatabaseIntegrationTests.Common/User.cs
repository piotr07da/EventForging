namespace EventForging.DatabaseIntegrationTests.Common;

public class User : IEventForged
{
    public User()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

    public Guid Id { get; private set; }
    public bool Approved { get; private set; }

    public static User Register(Guid id)
    {
        var order = new User();
        var events = order.Events;
        events.Apply(new UserRegistered(id));
        return order;
    }

    public void Approve()
    {
        Events.Apply(new UserApproved(Id));
    }

    private void Apply(UserRegistered e)
    {
        Id = e.UserId;
    }

    private void Apply(UserApproved e)
    {
        Approved = true;
    }
}

public sealed record UserRegistered(Guid UserId);

public sealed record UserApproved(Guid UserId);
