using System.Collections.Generic;

namespace EventForging
{
    public interface IAggregateRehydrator
    {
        bool TryRehydrate<TAggregate>(TAggregate aggregate, IEnumerable<object> events)
            where TAggregate : IEventForged;
    }
}
