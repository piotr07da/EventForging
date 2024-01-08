using System.Runtime.CompilerServices;
using System.Text.Json;
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

    public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var records = ReadRecordsAsync<TAggregate>(aggregateId, cancellationToken);
        await foreach (var record in records)
        {
            yield return record.EventData;
        }
    }

    public async IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }
}
