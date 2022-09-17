using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EventForging
{
    public class Repository<TAggregate> : IRepository<TAggregate>
        where TAggregate : class, IEventForged, new()
    {
        private readonly IAggregateRehydrator _aggregateRehydrator;
        private readonly IEventDatabase _database;

        public Repository(
            IAggregateRehydrator aggregateRehydrator,
            IEventDatabase database)
        {
            _aggregateRehydrator = aggregateRehydrator ?? throw new ArgumentNullException(nameof(aggregateRehydrator));
            _database = database;
        }

        public async Task<TAggregate> GetAsync(Guid aggregateId)
        {
            return await GetAsync(aggregateId.ToString());
        }

        public async Task<TAggregate> GetAsync(string aggregateId)
        {
            var events = await _database.ReadAsync<TAggregate>(aggregateId);
            var aggregate = AggregateProxyGenerator.Create<TAggregate>();
            var rehydrated = _aggregateRehydrator.TryRehydrate(aggregate, events);
            if (!rehydrated)
            {
                throw new Exception($"{typeof(TAggregate).Name} with id {aggregateId} not found.");
            }

            return aggregate;
        }

        public async Task SaveAsync(Guid aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties)
        {
            await SaveAsync(aggregateId.ToString(), aggregate, expectedVersion, conversationId, initiatorId, customProperties);
        }

        public async Task SaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties)
        {
            var aggregateMetadata = aggregate.GetAggregateMetadata();
            var lastReadAggregateVersion = aggregateMetadata.ReadVersion;

            if (expectedVersion.IsNone && lastReadAggregateVersion.AggregateExists)
            {
                throw new EventForgingUnexpectedVersionException(expectedVersion, lastReadAggregateVersion, null);
            }

            if (expectedVersion.IsDefined && (lastReadAggregateVersion.AggregateDoesNotExist || expectedVersion != lastReadAggregateVersion))
            {
                throw new EventForgingUnexpectedVersionException(expectedVersion, lastReadAggregateVersion, null);
            }

            var newEvents = aggregate.Events.Get().ToArray();
            customProperties = customProperties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // clone
            await _database.WriteAsync<TAggregate>(aggregateId, newEvents, lastReadAggregateVersion, expectedVersion, conversationId, initiatorId, customProperties);
            aggregate.Events.Clear();
        }
    }
}
