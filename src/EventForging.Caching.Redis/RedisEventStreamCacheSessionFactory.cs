using EventForging.Caching.Redis.Serialization;
using EventForging.Serialization;
using StackExchange.Redis;
using static EventForging.Caching.Redis.RedisEventStreamCacheFormat;

namespace EventForging.Caching.Redis;

internal sealed class RedisEventStreamCacheSessionFactory : IEventStreamCacheSessionFactory
{
    private readonly IDatabase _database;
    private readonly IRedisEventStreamCacheConfiguration _redisConfiguration;
    private readonly IEventSerializer _eventSerializer;

    public RedisEventStreamCacheSessionFactory(
        IConnectionMultiplexer connectionMultiplexer,
        IRedisEventStreamCacheConfiguration redisConfiguration,
        IEventForgingSerializationConfiguration serializationConfiguration)
    {
        _database = connectionMultiplexer.GetDatabase();
        _redisConfiguration = redisConfiguration;
        _eventSerializer = new JsonEventSerializer(
            serializationConfiguration,
            new RedisEventStreamCacheJsonSerializerOptionsProvider());
    }

    public Task<IEventStreamCacheReadSession?> TryCreateReadSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        return RedisEventStreamCacheReadSession.TryCreateAsync(
            _database,
            CreateKey(typeof(TAggregate), aggregateId, _redisConfiguration),
            _eventSerializer,
            _redisConfiguration,
            cancellationToken);
    }

    public async Task<IEventStreamCacheWriteSession?> TryCreateWriteSessionAsync<TAggregate>(
        string aggregateId,
        CancellationToken cancellationToken = default)
    {
        return await RedisEventStreamCacheWriteSession.CreateAsync(
            _database,
            CreateKey(typeof(TAggregate), aggregateId, _redisConfiguration),
            _eventSerializer,
            _redisConfiguration,
            cancellationToken).ConfigureAwait(false);
    }
}
