using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventForging
{
    public interface IEventDatabase
    {
        Task<IEnumerable<object>> ReadAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default);
        Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion lastReadAggregateVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default);
    }
}
