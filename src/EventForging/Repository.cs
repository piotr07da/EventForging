using System.Diagnostics;
using EventForging.Diagnostics.Tracing;

namespace EventForging;

internal sealed class Repository<TAggregate> : IRepository<TAggregate>
    where TAggregate : class, IEventForged
{
    private readonly IEventDatabase _database;
    private readonly IRepositorySaveInterceptor[] _genericSaveInterceptors;
    private readonly IRepositorySaveInterceptor<TAggregate>[] _specificSaveInterceptors;

    public Repository(
        IEventDatabase database,
        IEnumerable<IRepositorySaveInterceptor> genericSaveInterceptors,
        IEnumerable<IRepositorySaveInterceptor<TAggregate>> specificSaveInterceptors)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _genericSaveInterceptors = genericSaveInterceptors?.ToArray() ?? Array.Empty<IRepositorySaveInterceptor>();
        _specificSaveInterceptors = specificSaveInterceptors?.ToArray() ?? Array.Empty<IRepositorySaveInterceptor<TAggregate>>();
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

    private async Task InternalSaveAsync(string aggregateId, TAggregate aggregate, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string>? customProperties, Activity? activity, CancellationToken cancellationToken)
    {
        var aggregateMetadata = aggregate.GetAggregateMetadata();
        var retrievedVersion = aggregateMetadata.RetrievedVersion;

        customProperties = customProperties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // clone
        customProperties ??= new Dictionary<string, string>();
        customProperties.StoreCurrentActivityId(); // Can (and should) be later overwritten inside any database-specific implementation. It is here only to ensure that if any database-specific implementation does not store ActivityId then at least it is stored at this level.

        var saveInterceptorContext = new RepositorySaveInterceptorContext<TAggregate>(aggregateId, aggregate, retrievedVersion, expectedVersion, conversationId, initiatorId, customProperties);

        for (var i = 0; i < _genericSaveInterceptors.Length; ++i)
        {
            var forwarder = new RepositorySaveInterceptorContextForwarder<TAggregate>();
            await _genericSaveInterceptors[i].SaveAsync(saveInterceptorContext, forwarder, cancellationToken).ConfigureAwait(false);

            if (!forwarder.Forwarded)
            {
                activity?.EnrichRepositorySaveActivityWithInterceptionPipelineStatus("NOT FORWARDED");
                return;
            }

            saveInterceptorContext = forwarder.ReceivedContext ?? throw new EventForgingException("Repository interception pipeline cannot pass null context to the next interception pipe.");
        }

        for (var i = 0; i < _specificSaveInterceptors.Length; ++i)
        {
            var forwarder = new RepositorySaveInterceptorContextForwarder<TAggregate>();
            await _specificSaveInterceptors[i].SaveAsync(saveInterceptorContext, forwarder, cancellationToken).ConfigureAwait(false);

            if (!forwarder.Forwarded)
            {
                activity?.EnrichRepositorySaveActivityWithInterceptionPipelineStatus("NOT FORWARDED");
                return;
            }

            saveInterceptorContext = forwarder.ReceivedContext ?? throw new EventForgingException("Repository interception pipeline cannot pass null context to the next interception pipe.");
        }

        aggregateId = saveInterceptorContext.AggregateId;
        aggregate = saveInterceptorContext.Aggregate;
        expectedVersion = saveInterceptorContext.ExpectedVersion;
        conversationId = saveInterceptorContext.ConversationId;
        initiatorId = saveInterceptorContext.InitiatorId;
        customProperties = saveInterceptorContext.CustomProperties;

        activity?.EnrichRepositorySaveActivityWithAggregateVersion(retrievedVersion);

        if (expectedVersion.IsNone && retrievedVersion.AggregateExists)
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, null, expectedVersion, retrievedVersion, null);
        }

        if (expectedVersion.IsDefined && (retrievedVersion.AggregateDoesNotExist || expectedVersion != retrievedVersion))
        {
            throw new EventForgingUnexpectedVersionException(aggregateId, null, expectedVersion, retrievedVersion, null);
        }

        var newEvents = aggregate.Events.Get().ToArray();

        await _database.WriteAsync<TAggregate>(aggregateId, newEvents, retrievedVersion, expectedVersion, conversationId, initiatorId, customProperties, cancellationToken).ConfigureAwait(false);
    }
}
