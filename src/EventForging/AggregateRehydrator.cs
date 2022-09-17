using System.Collections.Generic;

namespace EventForging
{
    public class AggregateRehydrator : IAggregateRehydrator
    {
        public bool TryRehydrate<TAggregate>(TAggregate aggregate, IEnumerable<object> events)
            where TAggregate : IEventForged
        {
            if (events == null)
            {
                return false;
            }

            var eventApplier = EventApplier.CreateFor(aggregate);

            var appliedEventCount = eventApplier.ApplyEvents(events, false);

            aggregate.ConfigureAggregateMetadata(md => md.ReadVersion = appliedEventCount - 1);

            return appliedEventCount > 0;
        }
    }
}
