using System.Runtime.CompilerServices;
using EventForging.Serialization;
using StackExchange.Redis;
using static EventForging.Caching.Redis.RedisEventStreamCacheFormat;

namespace EventForging.Caching.Redis;

internal sealed class RedisEventStreamCacheReadSession : IEventStreamCacheReadSession
{
    private readonly IDatabase _database;
    private readonly RedisKey _key;
    private readonly IEventSerializer _eventSerializer;
    private readonly IRedisEventStreamCacheConfiguration _redisConfiguration;

    private RedisEventStreamCacheReadSession(
        IDatabase database,
        RedisKey key,
        AggregateVersion version,
        IEventSerializer eventSerializer,
        IRedisEventStreamCacheConfiguration redisConfiguration)
    {
        _database = database;
        _key = key;
        Version = version;
        _eventSerializer = eventSerializer;
        _redisConfiguration = redisConfiguration;
    }

    internal static async Task<IEventStreamCacheReadSession?> TryCreateAsync(
        IDatabase database,
        RedisKey key,
        IEventSerializer eventSerializer,
        IRedisEventStreamCacheConfiguration redisConfiguration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var versionRead = database.HashGetAsync(key, LastCachedEventVersionField);
        var expirationUpdate = database.KeyExpireAsync(key, redisConfiguration.SlidingExpiration);
        await Task.WhenAll(versionRead, expirationUpdate).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var lastCachedEventVersion = await versionRead.ConfigureAwait(false);
        return lastCachedEventVersion.HasValue
            ? new RedisEventStreamCacheReadSession(
                database,
                key,
                AggregateVersion.FromValue((long)lastCachedEventVersion),
                eventSerializer,
                redisConfiguration)
            : null;
    }

    public AggregateVersion Version { get; }

    public async IAsyncEnumerable<object> GetEventsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var eventCount = Version.Next().Value;
        for (var chunkStart = 0L; chunkStart < eventCount; chunkStart += _redisConfiguration.EventsPerChunk)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var chunk = await _database.HashGetAsync(_key, CreateChunkFieldName(chunkStart))
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var serializedEvents = DeserializeChunk((byte[])chunk!, _redisConfiguration.CompressionEnabled);
            var eventsInChunk = (int)Math.Min(_redisConfiguration.EventsPerChunk, eventCount - chunkStart);
            for (var eventIndex = 0; eventIndex < eventsInChunk; ++eventIndex)
            {
                var serializedEvent = serializedEvents[eventIndex];
                yield return _eventSerializer.DeserializeFromBytes(serializedEvent.EventName, serializedEvent.SerializedData);
            }
        }
    }
}
