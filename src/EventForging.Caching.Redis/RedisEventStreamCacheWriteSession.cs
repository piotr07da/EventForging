using EventForging.Serialization;
using StackExchange.Redis;
using static EventForging.Caching.Redis.RedisEventStreamCacheFormat;

namespace EventForging.Caching.Redis;

internal sealed class RedisEventStreamCacheWriteSession : IEventStreamCacheWriteSession
{
    private readonly IDatabase _database;
    private readonly RedisKey _key;
    private readonly AggregateVersion? _lastCachedEventVersion;
    private readonly IEventSerializer _eventSerializer;
    private readonly IRedisEventStreamCacheConfiguration _configuration;
    private readonly List<SerializedEvent> _currentChunk;
    private readonly List<(long ChunkStart, byte[] SerializedChunk)> _pendingCompleteChunks = new();
    private long? _lastEventVersion;
    private bool _cacheUpdateRejected;
    private bool _completed;

    private RedisEventStreamCacheWriteSession(
        IDatabase database,
        RedisKey key,
        AggregateVersion? lastCachedEventVersion,
        IEnumerable<SerializedEvent> lastCachedChunk,
        IEventSerializer eventSerializer,
        IRedisEventStreamCacheConfiguration configuration)
    {
        _database = database;
        _key = key;
        _lastCachedEventVersion = lastCachedEventVersion;
        _eventSerializer = eventSerializer;
        _configuration = configuration;
        _currentChunk = new List<SerializedEvent>(_configuration.EventsPerChunk);
        _currentChunk.AddRange(lastCachedChunk);
    }

    internal static async Task<RedisEventStreamCacheWriteSession> CreateAsync(
        IDatabase database,
        RedisKey key,
        IEventSerializer eventSerializer,
        IRedisEventStreamCacheConfiguration redisConfiguration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lastCachedEventVersionValue = await database.HashGetAsync(key, LastCachedEventVersionField)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        SerializedEvent[] lastCachedChunk = Array.Empty<SerializedEvent>();
        AggregateVersion? lastCachedEventVersion = null;
        if (lastCachedEventVersionValue.HasValue)
        {
            lastCachedEventVersion = AggregateVersion.FromValue((long)lastCachedEventVersionValue);
            var nextEventVersion = lastCachedEventVersion.Value.Next();
            var cachedEventCountInChunk = (int)(nextEventVersion.Value % redisConfiguration.EventsPerChunk);
            if (cachedEventCountInChunk > 0)
            {
                var chunkStart = GetChunkStart(nextEventVersion.Value, redisConfiguration.EventsPerChunk);
                var chunk = await database.HashGetAsync(
                        key,
                        CreateChunkFieldName(chunkStart))
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                lastCachedChunk = DeserializeChunk((byte[])chunk!, redisConfiguration.CompressionEnabled)
                    .Take(cachedEventCountInChunk)
                    .ToArray();
            }
        }

        return new RedisEventStreamCacheWriteSession(
            database,
            key,
            lastCachedEventVersion,
            lastCachedChunk,
            eventSerializer,
            redisConfiguration);
    }

    public async Task AppendAsync(object @event, AggregateVersion version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_completed)
        {
            throw new EventForgingException("Cannot append an event to a completed Redis event stream cache write session.");
        }

        if (_cacheUpdateRejected)
        {
            return;
        }

        if (_lastEventVersion is null
            && version != (_lastCachedEventVersion ?? AggregateVersion.NotExistingAggregate).Next())
        {
            _cacheUpdateRejected = true;
            _currentChunk.Clear();
            return;
        }

        var data = _eventSerializer.SerializeToBytes(@event, out var eventName);
        _currentChunk.Add(new SerializedEvent(eventName, data));
        _lastEventVersion = version.Value;
        if (_currentChunk.Count == _configuration.EventsPerChunk)
        {
            _pendingCompleteChunks.Add((
                GetChunkStart(version.Value, _configuration.EventsPerChunk),
                SerializeChunk(_currentChunk, _configuration.CompressionEnabled)));
            _currentChunk.Clear();
        }

        if (_pendingCompleteChunks.Count > 0
            && version.Next().Value >= _configuration.MinimumEventCount)
        {
            await StorePendingCompleteChunksAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task CompleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_completed)
        {
            throw new EventForgingException("Redis event stream cache write session has already been completed.");
        }

        _completed = true;
        if (_cacheUpdateRejected
            || _lastEventVersion is null
            || AggregateVersion.FromValue(_lastEventVersion.Value).Next().Value < _configuration.MinimumEventCount)
        {
            return;
        }

        var transaction = _database.CreateTransaction();
        transaction.AddCondition(_lastCachedEventVersion is null
            ? Condition.HashNotExists(_key, LastCachedEventVersionField)
            : Condition.HashEqual(_key, LastCachedEventVersionField, _lastCachedEventVersion.Value.Value));
        var chunkStorage = _currentChunk.Count > 0
            ? transaction.HashSetAsync(
                _key,
                CreateChunkFieldName(
                    GetChunkStart(_lastEventVersion.Value, _configuration.EventsPerChunk)),
                SerializeChunk(_currentChunk, _configuration.CompressionEnabled))
            : null;
        var versionStorage = transaction.HashSetAsync(_key, LastCachedEventVersionField, _lastEventVersion.Value);
        var expirationUpdate = transaction.KeyExpireAsync(_key, _configuration.SlidingExpiration);
        if (await transaction.ExecuteAsync().ConfigureAwait(false))
        {
            await Task.WhenAll(
                versionStorage,
                expirationUpdate,
                chunkStorage is null ? Task.CompletedTask : chunkStorage).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task StorePendingCompleteChunksAsync(CancellationToken cancellationToken)
    {
        var storageTasks = new Task[_pendingCompleteChunks.Count + 1];
        for (var chunkIndex = 0; chunkIndex < _pendingCompleteChunks.Count; ++chunkIndex)
        {
            var chunk = _pendingCompleteChunks[chunkIndex];
            storageTasks[chunkIndex] = _database.HashSetAsync(
                _key,
                CreateChunkFieldName(chunk.ChunkStart),
                chunk.SerializedChunk);
        }

        storageTasks[_pendingCompleteChunks.Count] = _database.KeyExpireAsync(
            _key,
            _configuration.SlidingExpiration);
        await Task.WhenAll(storageTasks).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        _pendingCompleteChunks.Clear();
    }
}
