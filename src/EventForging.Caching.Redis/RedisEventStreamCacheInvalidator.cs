using StackExchange.Redis;
using static EventForging.Caching.Redis.RedisEventStreamCacheFormat;

namespace EventForging.Caching.Redis;

internal sealed class RedisEventStreamCacheInvalidator : IEventStreamCacheInvalidator
{
    private readonly IDatabase _database;
    private readonly IRedisEventStreamCacheConfiguration _configuration;

    public RedisEventStreamCacheInvalidator(
        IConnectionMultiplexer connectionMultiplexer,
        IRedisEventStreamCacheConfiguration configuration)
    {
        _database = connectionMultiplexer.GetDatabase();
        _configuration = configuration;
    }

    public async Task InvalidateAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateKey(typeof(TAggregate), aggregateId, _configuration);
        await _database.KeyDeleteAsync(key).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
