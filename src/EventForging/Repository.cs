﻿using System.Diagnostics;
using EventForging.Diagnostics.Tracing;

namespace EventForging;

internal sealed class Repository<TAggregate> : IRepository<TAggregate>
    where TAggregate : class, IEventForged
{
    private readonly IEventDatabase _database;

    public Repository(IEventDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task<TAggregate> GetAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        return await GetAsync(aggregateId.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<TAggregate> GetAsync(string aggregateId, CancellationToken cancellationToken = default)
    {
        var activity = ActivitySourceProvider.ActivitySource.StartRepositoryGetActivity<TAggregate>(aggregateId, false);

        try
        {
            return await InternalTryGetAsync(aggregateId, activity, cancellationToken) ?? throw new AggregateNotFoundEventForgingException(typeof(TAggregate), aggregateId);
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
        finally
        {
            activity?.Complete();
        }
    }

    public async Task<TAggregate?> TryGetAsync(Guid aggregateId, CancellationToken cancellationToken = default)
    {
        return await TryGetAsync(aggregateId.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<TAggregate?> TryGetAsync(string aggregateId, CancellationToken cancellationToken = default)
    {
        var activity = ActivitySourceProvider.ActivitySource.StartRepositoryGetActivity<TAggregate>(aggregateId, true);

        try
        {
            return await InternalTryGetAsync(aggregateId, activity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
        finally
        {
            activity?.Complete();
        }
    }

    public async Task SaveAsync(Guid aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties, CancellationToken cancellationToken = default)
    {
        await SaveAsync(aggregateId.ToString(), aggregate, expectedVersion, conversationId, initiatorId, customProperties, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties, CancellationToken cancellationToken = default)
    {
        var activity = ActivitySourceProvider.ActivitySource.StartRepositorySaveActivity(aggregateId, aggregate, expectedVersion, conversationId, initiatorId, customProperties);

        try
        {
            await InternalSaveAsync(aggregateId, aggregate, expectedVersion, conversationId, initiatorId, customProperties, activity, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            throw;
        }
        finally
        {
            activity?.Complete();
        }
    }

    private async Task<TAggregate?> InternalTryGetAsync(string aggregateId, Activity? activity, CancellationToken cancellationToken = default)
    {
        var aggregate = AggregateProxyGenerator.Create<TAggregate>();
        var eventApplier = EventApplier.CreateFor(aggregate);

        var events = _database.ReadAsync<TAggregate>(aggregateId, cancellationToken).ConfigureAwait(false);

        var eventCount = 0;

        await foreach (var e in events)
        {
            eventApplier.ApplyEvent(e, false);
            ++eventCount;
        }

        if (eventCount == 0)
        {
            return null;
        }

        var retrievedVersion = AggregateVersion.FromValue(eventCount - 1);
        aggregate.ConfigureAggregateMetadata(md => md.RetrievedVersion = retrievedVersion);

        activity.EnrichRepositoryGetActivityWithAggregateVersion(retrievedVersion);

        return aggregate;
    }

    private async Task InternalSaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties, Activity? activity, CancellationToken cancellationToken = default)
    {
        var aggregateMetadata = aggregate.GetAggregateMetadata();
        var retrievedAggregateVersion = aggregateMetadata.RetrievedVersion;

        activity?.EnrichRepositorySaveActivityWithAggregateVersion(retrievedAggregateVersion);

        if (expectedVersion.IsNone && retrievedAggregateVersion.AggregateExists)
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, null, expectedVersion, retrievedAggregateVersion, null);
        }

        if (expectedVersion.IsDefined && (retrievedAggregateVersion.AggregateDoesNotExist || expectedVersion != retrievedAggregateVersion))
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, null, expectedVersion, retrievedAggregateVersion, null);
        }

        var newEvents = aggregate.Events.Get().ToArray();
        customProperties = customProperties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // clone
        customProperties ??= new Dictionary<string, string>();
        customProperties.StoreCurrentActivityId(); // Can (and should) be later overwritten inside any database-specific implementation. It is here only to ensure that if any database-specific implementation does not store ActivityId then at least it is stored at this level.

        await _database.WriteAsync<TAggregate>(aggregateId, newEvents, retrievedAggregateVersion, expectedVersion, conversationId, initiatorId, customProperties, cancellationToken).ConfigureAwait(false);
    }
}
