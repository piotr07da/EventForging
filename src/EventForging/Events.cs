namespace EventForging;

public sealed class Events
{
    private readonly IList<object> _events = new List<object>();
    private readonly EventApplier _eventApplier;

    private Events(EventApplier eventApplier)
    {
        _eventApplier = eventApplier ?? throw new ArgumentNullException(nameof(eventApplier));
    }

    public object[] Get() => _events.ToArray();

    public void Apply(object @event)
    {
        _eventApplier.ApplyEvent(@event, true);
        _events.Add(@event);
    }

    public static Events CreateFor(object owner)
    {
        var ownerType = owner.GetType();
        if (ownerType.IsSealed)
        {
            throw new EventForgingException("An aggregate class cannot be sealed.");
        }

        return new Events(EventApplier.CreateFor(owner));
    }
}
