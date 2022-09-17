using System;
using System.Collections.Generic;
using System.Linq;

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

    public void Clear() => _events.Clear();

    public static Events CreateFor(object owner)
    {
        return new Events(EventApplier.CreateFor(owner));
    }
}
