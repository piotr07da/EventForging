using System;
using System.Collections.Generic;

namespace EventForging
{
    public sealed class EventApplier
    {
        private object _target;
        private IReadOnlyDictionary<Type, EventApplierAction> _eventApplierActions;

        public int ApplyEvents(IEnumerable<object> events, bool throwIfApplyActionNotFound)
        {
            var count = 0;
            foreach (var evt in events)
            {
                ApplyEvent(evt, throwIfApplyActionNotFound);
                ++count;
            }

            return count;
        }

        public void ApplyEvent(object @event, bool throwIfApplyActionNotFound)
        {
            var eventType = @event.GetType();

            if (!_eventApplierActions.TryGetValue(eventType, out var eventApplier))
            {
                if (throwIfApplyActionNotFound)
                {
                    throw new InvalidOperationException($"Target event applier [{_target.GetType().Name}] has no [void Apply({eventType.Name} e)] method. Add and fill body of following method:{Environment.NewLine}{Environment.NewLine}private void Apply({eventType.Name} e){Environment.NewLine}{{{Environment.NewLine}}}{Environment.NewLine}");
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
}
