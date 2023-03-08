namespace EventForging;

internal sealed class EventApplier
{
    private object? _target;
    private IReadOnlyDictionary<Type, EventApplierAction>? _eventApplierActions;

    public void ApplyEvent(object @event, bool throwIfApplyActionNotFound)
    {
        if (_target == null || _eventApplierActions == null) throw new EventForgingException($"Cannot apply event of type '{@event.GetType().FullName}' because target aggregate has not been registered.");

        var eventType = @event.GetType();

        if (!_eventApplierActions.TryGetValue(eventType, out var eventApplier))
        {
            if (throwIfApplyActionNotFound)
            {
                throw new InvalidOperationException($"Aggregate of type [{_target.GetType().Name}] has no [void Apply({eventType.Name} e)] method. Add and fill body of following method:{Environment.NewLine}{Environment.NewLine}private void Apply({eventType.Name} e){Environment.NewLine}{{{Environment.NewLine}}}{Environment.NewLine}");
            }

            return;
        }

        eventApplier(@event);
    }

    private void Register(object target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _eventApplierActions = EventApplierActionsExtractor.Extract(_target);
    }

    public static EventApplier CreateFor(object target)
    {
        var aea = new EventApplier();
        aea.Register(target);
        return aea;
    }
}
