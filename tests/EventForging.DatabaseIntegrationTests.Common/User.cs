namespace EventForging.DatabaseIntegrationTests.Common;

public class User : IEventForged
{
    public User()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

    public Guid Id { get; private set; }
    public string? Name { get; private set; }
    public int Counter { get; private set; }
    public bool Approved { get; private set; }

    public static User Register(Guid id)
    {
        var order = new User();
        var events = order.Events;
        events.Apply(new UserRegistered(id));
        return order;
    }

    public static User RegisterWithName(Guid id, string name, int amountOfCounterEvents = 0)
    {
        var order = new User();
        var events = order.Events;
        events.Apply(new UserRegistered(id));
        events.Apply(new UserNamed(id, name));
        for(var i = 0; i < amountOfCounterEvents; ++i)
        {
            events.Apply(new UserCounterChanged(id, i));
        }
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

    private void Apply(UserNamed e)
    {
        Name = e.Name;
    }

    private void Apply(UserCounterChanged e)
    {
        Counter = e.Counter;
    }
    
    private void Apply(UserApproved e)
    {
        Approved = true;
    }
}

public sealed record UserRegistered(Guid UserId);

public sealed record UserNamed(Guid UserId, string Name);

public sealed record UserCounterChanged(Guid UserId, int Counter);

public sealed record UserApproved(Guid UserId);
