namespace EventForging.Caching.Redis;

internal sealed class RedisEventStreamCacheConfiguration : IRedisEventStreamCacheConfiguration
{
    internal const int DefaultMinimumEventCount = 3000;
    internal const string DefaultKeyPrefix = "eventforging:event-stream-cache:";
    internal const int DefaultEventsPerChunk = 1000;
    internal static readonly TimeSpan DefaultSlidingExpiration = TimeSpan.FromSeconds(60);

    public int MinimumEventCount { get; set; } = DefaultMinimumEventCount;
    public TimeSpan SlidingExpiration { get; set; } = DefaultSlidingExpiration;
    public string ConnectionString { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = DefaultKeyPrefix;
    public int EventsPerChunk { get; set; } = DefaultEventsPerChunk;
    public bool CompressionEnabled { get; set; } = true;

    internal void Validate(bool connectionMultiplexerRegistered)
    {
        if (MinimumEventCount <= 0)
        {
            throw new EventForgingConfigurationException($"{nameof(MinimumEventCount)} must be greater than zero.");
        }

        if (SlidingExpiration <= TimeSpan.Zero)
        {
            throw new EventForgingConfigurationException($"{nameof(SlidingExpiration)} must be greater than zero.");
        }

        if (!connectionMultiplexerRegistered && string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new EventForgingConfigurationException($"{nameof(ConnectionString)} must be defined when an IConnectionMultiplexer is not registered.");
        }

        if (string.IsNullOrWhiteSpace(KeyPrefix))
        {
            throw new EventForgingConfigurationException($"{nameof(KeyPrefix)} must be defined.");
        }

        if (EventsPerChunk <= 0)
        {
            throw new EventForgingConfigurationException($"{nameof(EventsPerChunk)} must be greater than zero.");
        }
    }
}
