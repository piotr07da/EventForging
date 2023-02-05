using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventForging;

public interface IRepository<TAggregate>
{
    Task<TAggregate> GetAsync(Guid aggregateId);
    Task<TAggregate> GetAsync(string aggregateId);
    Task SaveAsync(Guid aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties);
    Task SaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties);
}
