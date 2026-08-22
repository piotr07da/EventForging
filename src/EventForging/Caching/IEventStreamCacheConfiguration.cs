namespace EventForging.Caching;

/// <summary>Configures behavior shared by all event stream cache implementations.</summary>
public interface IEventStreamCacheConfiguration
{
    /// <summary>The minimum number of events required before a stream is cached.</summary>
    int MinimumEventCount { get; set; }

    /// <summary>The time after the last cache access when an event stream expires.</summary>
    TimeSpan SlidingExpiration { get; set; }
}
