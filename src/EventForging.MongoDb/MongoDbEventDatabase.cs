using System.Runtime.CompilerServices;
using System.Text.Json;
using EventForging.Diagnostics.Tracing;
using EventForging.Serialization;

namespace EventForging.MongoDb;

internal sealed class MongoDbEventDatabase : IEventDatabase
{
    private readonly IEventForgingConfiguration _configuration;
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;

    public MongoDbEventDatabase(
        IEventForgingConfiguration configuration,
        IEventSerializer eventSerializer,
        IJsonSerializerOptionsProvider serializerOptionsProvider)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
    }

    private JsonSerializerOptions JsonSerializerOptions => _serializerOptionsProvider.Get();

    public IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default) =>
        ReadAsync<TAggregate>(aggregateId, EventStreamReadPosition.Beginning, cancellationToken);

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, EventStreamReadPosition readPosition, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var records = ReadRecordsFromPositionAsync<TAggregate>(aggregateId, readPosition, cancellationToken);
        await foreach (var record in records)
        {
            yield return record.EventData;
        }
    }

    public IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default) =>
        ReadRecordsFromPositionAsync<TAggregate>(aggregateId, EventStreamReadPosition.Beginning, cancellationToken);

    private async IAsyncEnumerable<EventDatabaseRecord> ReadRecordsFromPositionAsync<TAggregate>(string aggregateId, EventStreamReadPosition readPosition, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        customProperties.StoreCurrentActivityId();

        await Task.CompletedTask;
    }
}
