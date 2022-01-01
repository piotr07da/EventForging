﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EventForging
{
    public interface IEventDatabase
    {
        Task<IEnumerable<object>> ReadAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default);
        Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default);
    }

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
            var aggregate = new TAggregate();
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
            var newEvents = aggregate.Events.Get().ToArray();
            await _database.WriteAsync<TAggregate>(aggregateId, newEvents, expectedVersion, conversationId, initiatorId, customProperties);
            aggregate.Events.Clear();
        }
    }

    public interface IEventForged
    {
        Events Events { get; }
    }

    public class Events
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
}
