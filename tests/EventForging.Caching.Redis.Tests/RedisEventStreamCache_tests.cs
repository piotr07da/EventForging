// ReSharper disable InconsistentNaming

using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.Caching.Redis.Tests;

public class RedisEventStreamCache_tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Integration")]
    public async Task stored_events_are_read_in_order_and_an_older_version_does_not_replace_them(bool compressionEnabled)
    {
        using var serviceProvider = CreateServiceProvider(compressionEnabled);
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var invalidator = serviceProvider.GetRequiredService<IEventStreamCacheInvalidator>();
        var aggregateId = Guid.NewGuid().ToString();
        var firstEvents = CreateEvents(1, 2, 3);

        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, firstEvents, 0);
        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(90, 91), 0);
        var firstReadSession = await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId);

        Assert.NotNull(firstReadSession);
        Assert.Equal(2, firstReadSession.Version.Value);
        Assert.Equal(new[] { 1, 2, 3, }, await GetNumbersAsync(firstReadSession));

        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(4, 5), 3);
        var extendedReadSession = await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId);

        Assert.NotNull(extendedReadSession);
        Assert.Equal(4, extendedReadSession.Version.Value);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, }, await GetNumbersAsync(extendedReadSession));

        await invalidator.InvalidateAsync<RedisTestAggregate>(aggregateId);
        Assert.Null(await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task entry_expires_after_the_sliding_expiration()
    {
        using var serviceProvider = CreateServiceProvider(true, slidingExpiration: TimeSpan.FromMilliseconds(100));
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();

        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(1, 2), 0);
        await Task.Delay(300);

        Assert.Null(await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task stored_chunks_are_not_visible_before_the_write_session_is_completed()
    {
        using var serviceProvider = CreateServiceProvider(true);
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();
        var writeSession = await sessionFactory.TryCreateWriteSessionAsync<RedisTestAggregate>(aggregateId);
        Assert.NotNull(writeSession);

        await writeSession.AppendAsync(new CachedNumberEvent(1), AggregateVersion.FromValue(0));
        await writeSession.AppendAsync(new CachedNumberEvent(2), AggregateVersion.FromValue(1));
        await writeSession.AppendAsync(new CachedNumberEvent(3), AggregateVersion.FromValue(2));
        var readSessionBeforeCompletion = await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId);
        await writeSession.CompleteAsync();
        var readSessionAfterCompletion = await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId);

        Assert.Null(readSessionBeforeCompletion);
        Assert.NotNull(readSessionAfterCompletion);
        Assert.Equal(2, readSessionAfterCompletion.Version.Value);
        Assert.Equal(new[] { 1, 2, 3, }, await GetNumbersAsync(readSessionAfterCompletion));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task extending_chunks_do_not_advance_the_cached_version_before_completion()
    {
        using var serviceProvider = CreateServiceProvider(true);
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();
        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(1, 2), 0);
        var writeSession = await sessionFactory.TryCreateWriteSessionAsync<RedisTestAggregate>(aggregateId);
        Assert.NotNull(writeSession);

        await writeSession.AppendAsync(new CachedNumberEvent(3), AggregateVersion.FromValue(2));
        await writeSession.AppendAsync(new CachedNumberEvent(4), AggregateVersion.FromValue(3));
        var readSessionBeforeCompletion = await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId);
        await writeSession.CompleteAsync();
        var readSessionAfterCompletion = await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId);

        Assert.NotNull(readSessionBeforeCompletion);
        Assert.Equal(1, readSessionBeforeCompletion.Version.Value);
        Assert.Equal(new[] { 1, 2, }, await GetNumbersAsync(readSessionBeforeCompletion));
        Assert.NotNull(readSessionAfterCompletion);
        Assert.Equal(3, readSessionAfterCompletion.Version.Value);
        Assert.Equal(new[] { 1, 2, 3, 4, }, await GetNumbersAsync(readSessionAfterCompletion));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task a_stale_write_session_does_not_replace_a_newer_cached_version()
    {
        using var serviceProvider = CreateServiceProvider(true);
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();
        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(1, 2), 0);
        var staleWriteSession = await sessionFactory.TryCreateWriteSessionAsync<RedisTestAggregate>(aggregateId);
        Assert.NotNull(staleWriteSession);
        await staleWriteSession.AppendAsync(new CachedNumberEvent(3), AggregateVersion.FromValue(2));

        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(3, 4), 2);
        await staleWriteSession.CompleteAsync();
        var readSession = await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId);

        Assert.NotNull(readSession);
        Assert.Equal(3, readSession.Version.Value);
        Assert.Equal(new[] { 1, 2, 3, 4, }, await GetNumbersAsync(readSession));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task stream_shorter_than_minimum_event_count_is_not_cached()
    {
        using var serviceProvider = CreateServiceProvider(true, minimumEventCount: 3);
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();

        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(1, 2), 0);

        Assert.Null(await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId));
    }

    [Theory]
    [InlineData(2L)]
    [InlineData(3L)]
    [Trait("Category", "Integration")]
    public async Task a_stream_fragment_without_an_existing_predecessor_is_not_cached(long firstEventVersion)
    {
        using var serviceProvider = CreateServiceProvider(true);
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();

        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(3), firstEventVersion);

        Assert.Null(await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task read_is_limited_to_the_version_captured_before_the_last_chunk_is_extended()
    {
        using var serviceProvider = CreateServiceProvider(true);
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();

        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(1), 0);
        var readSession = await sessionFactory.TryCreateReadSessionAsync<RedisTestAggregate>(aggregateId);
        await WriteAsync<RedisTestAggregate>(sessionFactory, aggregateId, CreateEvents(2), 1);

        Assert.NotNull(readSession);
        Assert.Equal(0, readSession.Version.Value);
        Assert.Equal(new[] { 1, }, await GetNumbersAsync(readSession));
    }

    private static ServiceProvider CreateServiceProvider(
        bool compressionEnabled,
        int minimumEventCount = 1,
        TimeSpan? slidingExpiration = null)
    {
        var services = new ServiceCollection();
        services.AddEventForging(registration =>
        {
            registration.ConfigureEventForging(configuration =>
                configuration.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(typeof(CachedNumberEvent).Assembly)));
            registration.UseRedisEventStreamCache(configuration =>
            {
                configuration.MinimumEventCount = minimumEventCount;
                configuration.SlidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(1);
                configuration.ConnectionString = "localhost:6379,abortConnect=false,connectTimeout=5000,syncTimeout=5000";
                configuration.KeyPrefix = $"eventforging-tests:{Guid.NewGuid()}:";
                configuration.EventsPerChunk = 2;
                configuration.CompressionEnabled = compressionEnabled;
            });
        });

        return services.BuildServiceProvider();
    }

    private static object[] CreateEvents(params int[] numbers)
    {
        return numbers.Select(n => (object)new CachedNumberEvent(n)).ToArray();
    }

    private static async Task WriteAsync<TAggregate>(
        IEventStreamCacheSessionFactory sessionFactory,
        string aggregateId,
        IReadOnlyList<object> events,
        long firstEventVersion)
    {
        var writeSession = await sessionFactory.TryCreateWriteSessionAsync<TAggregate>(aggregateId);
        Assert.NotNull(writeSession);
        for (var eventIndex = 0; eventIndex < events.Count; ++eventIndex)
        {
            await writeSession.AppendAsync(events[eventIndex], AggregateVersion.FromValue(firstEventVersion + eventIndex));
        }

        await writeSession.CompleteAsync();
    }

    private static async Task<int[]> GetNumbersAsync(IEventStreamCacheReadSession readSession)
    {
        var numbers = new List<int>();
        await foreach (var @event in readSession.GetEventsAsync())
        {
            numbers.Add(((CachedNumberEvent)@event).Number);
        }

        return numbers.ToArray();
    }

    private sealed class RedisTestAggregate
    {
    }
}

public sealed class CachedNumberEvent
{
    public CachedNumberEvent(int number)
    {
        Number = number;
    }

    public int Number { get; }
}
