namespace EventForging.Caching.Redis;

/// <summary>Configures the Redis connection and event chunking behavior.</summary>
public interface IRedisEventStreamCacheConfiguration : IEventStreamCacheConfiguration
{
    /// <summary>The Redis connection string. It is not required when an <c>IConnectionMultiplexer</c> is already registered.</summary>
    string ConnectionString { get; set; }

    /// <summary>The text prepended to every Redis key created by this provider, used to isolate its data from other applications or environments.</summary>
    string KeyPrefix { get; set; }

    /// <summary>The maximum number of events serialized into one Redis hash field.</summary>
    int EventsPerChunk { get; set; }

    /// <summary>Enables GZip compression of serialized event chunks.</summary>
    bool CompressionEnabled { get; set; }
}
