// ReSharper disable InconsistentNaming

using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using EventForging.Caching;
using EventForging.Diagnostics;
using EventForging.Caching.Memory;
using EventForging.Caching.Memory.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.Tests;

public class EventStreamCache_tests
{
    [Fact]
    public void default_configurations_have_expected_limits()
    {
        var (_, _, configuration) = CreateRepository();
        var memoryConfiguration = Assert.IsAssignableFrom<IMemoryEventStreamCacheConfiguration>(configuration);

        Assert.Equal(3000, memoryConfiguration.MinimumEventCount);
        Assert.Equal(TimeSpan.FromSeconds(60), memoryConfiguration.SlidingExpiration);
        Assert.Equal(1000, memoryConfiguration.MaximumCachedStreamCount);
        Assert.Equal(200_000, memoryConfiguration.MaximumTotalCachedEventCount);
    }

    [Fact]
    public async Task when_cache_is_disabled_then_every_get_reads_entire_stream()
    {
        var (repository, database, _) = CreateRepository(useMemoryCache: false);
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(aggregateId, new NumberBeerBrewedEvent(1));

        await repository.GetAsync(aggregateId);
        await repository.GetAsync(aggregateId);

        Assert.Equal(new AggregateVersion?[] { null, null, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task when_event_count_reaches_minimum_then_next_get_reads_only_events_after_cached_version()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 3;
        });
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(
            aggregateId,
            new NumberBeerBrewedEvent(1),
            new NumberBeerBrewedEvent(2),
            new NumberBeerBrewedEvent(3));

        var first = await repository.GetAsync(aggregateId);
        first.BrewNumberBeer(999);
        database.Add(aggregateId, new NumberBeerBrewedEvent(4));
        var second = await repository.GetAsync(aggregateId);
        var third = await repository.GetAsync(aggregateId);

        Assert.NotSame(first, second);
        Assert.NotSame(second, third);
        Assert.Equal(999, first.NumberBeerBrewed);
        Assert.Equal(4, second.NumberBeerBrewed);
        Assert.Equal(4, third.NumberBeerBrewed);
        Assert.Equal(new AggregateVersion?[] { null, 2, 3, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task when_event_count_is_below_minimum_then_stream_is_not_cached()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 3;
        });
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(
            aggregateId,
            new NumberBeerBrewedEvent(1),
            new NumberBeerBrewedEvent(2));

        await repository.GetAsync(aggregateId);
        await repository.GetAsync(aggregateId);

        Assert.Equal(new AggregateVersion?[] { null, null, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task when_capacity_is_exceeded_then_least_recently_used_stream_is_removed()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 1;
            c.MaximumCachedStreamCount = 2;
        });
        var firstId = Guid.NewGuid().ToString();
        var secondId = Guid.NewGuid().ToString();
        var thirdId = Guid.NewGuid().ToString();
        database.Add(firstId, new NumberBeerBrewedEvent(1));
        database.Add(secondId, new NumberBeerBrewedEvent(2));
        database.Add(thirdId, new NumberBeerBrewedEvent(3));

        await repository.GetAsync(firstId);
        await repository.GetAsync(secondId);
        await repository.GetAsync(firstId);
        await repository.GetAsync(thirdId);
        await repository.GetAsync(secondId);

        Assert.Equal(new AggregateVersion?[] { null, 0, }, database.ReadsFor(firstId));
        Assert.Equal(new AggregateVersion?[] { null, null, }, database.ReadsFor(secondId));
        Assert.Equal(new AggregateVersion?[] { null, }, database.ReadsFor(thirdId));
    }

    [Fact]
    public async Task when_total_event_capacity_is_exceeded_then_least_recently_used_streams_are_removed()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 1;
            c.MaximumTotalCachedEventCount = 4;
        });
        var firstId = Guid.NewGuid().ToString();
        var secondId = Guid.NewGuid().ToString();
        var thirdId = Guid.NewGuid().ToString();
        database.Add(firstId, new NumberBeerBrewedEvent(1), new NumberBeerBrewedEvent(2));
        database.Add(secondId, new NumberBeerBrewedEvent(3));
        database.Add(thirdId, new NumberBeerBrewedEvent(4), new NumberBeerBrewedEvent(5));

        await repository.GetAsync(firstId);
        await repository.GetAsync(secondId);
        await repository.GetAsync(firstId);
        await repository.GetAsync(thirdId);
        await repository.GetAsync(firstId);
        await repository.GetAsync(secondId);

        Assert.Equal(new AggregateVersion?[] { null, 1, 1, }, database.ReadsFor(firstId));
        Assert.Equal(new AggregateVersion?[] { null, null, }, database.ReadsFor(secondId));
        Assert.Equal(new AggregateVersion?[] { null, }, database.ReadsFor(thirdId));
    }

    [Fact]
    public async Task when_stream_is_larger_than_total_event_capacity_then_it_is_not_cached()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 1;
            c.MaximumTotalCachedEventCount = 2;
        });
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(
            aggregateId,
            new NumberBeerBrewedEvent(1),
            new NumberBeerBrewedEvent(2),
            new NumberBeerBrewedEvent(3));

        await repository.GetAsync(aggregateId);
        await repository.GetAsync(aggregateId);

        Assert.Equal(new AggregateVersion?[] { null, null, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task when_cached_stream_grows_beyond_total_event_capacity_then_it_is_removed()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 1;
            c.MaximumTotalCachedEventCount = 2;
        });
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(
            aggregateId,
            new NumberBeerBrewedEvent(1),
            new NumberBeerBrewedEvent(2));

        await repository.GetAsync(aggregateId);
        database.Add(aggregateId, new NumberBeerBrewedEvent(3));
        await repository.GetAsync(aggregateId);
        await repository.GetAsync(aggregateId);

        Assert.Equal(new AggregateVersion?[] { null, 1, null, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task when_cached_stream_grows_then_its_previous_event_count_is_replaced_in_total_capacity()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 1;
            c.MaximumTotalCachedEventCount = 4;
        });
        var growingStreamId = Guid.NewGuid().ToString();
        var otherStreamId = Guid.NewGuid().ToString();
        database.Add(growingStreamId, new NumberBeerBrewedEvent(1), new NumberBeerBrewedEvent(2));
        database.Add(otherStreamId, new NumberBeerBrewedEvent(3), new NumberBeerBrewedEvent(4));

        await repository.GetAsync(growingStreamId);
        await repository.GetAsync(otherStreamId);
        database.Add(growingStreamId, new NumberBeerBrewedEvent(5), new NumberBeerBrewedEvent(6));
        await repository.GetAsync(growingStreamId);
        await repository.GetAsync(growingStreamId);
        await repository.GetAsync(otherStreamId);

        Assert.Equal(new AggregateVersion?[] { null, 1, 3, }, database.ReadsFor(growingStreamId));
        Assert.Equal(new AggregateVersion?[] { null, null, }, database.ReadsFor(otherStreamId));
    }

    [Fact]
    public async Task when_sliding_expiration_passes_then_entire_stream_is_read_again()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 1;
            c.SlidingExpiration = TimeSpan.FromMilliseconds(20);
        });
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(aggregateId, new NumberBeerBrewedEvent(1));

        await repository.GetAsync(aggregateId);
        await Task.Delay(100);
        await repository.GetAsync(aggregateId);

        Assert.Equal(new AggregateVersion?[] { null, null, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task cache_operations_emit_lookup_size_events_served_and_entry_removal_metrics()
    {
        var measurements = new List<MetricMeasurement>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if ((instrument.Meter.Name == EventForgingDiagnosticsInfo.MeterName
                 || instrument.Meter.Name == EventForgingMemoryEventStreamCacheDiagnosticsInfo.MeterName)
                && instrument.Name.StartsWith("eventforging.event_stream_cache."))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            var measurementTags = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                measurementTags[tag.Key] = tag.Value;
            }

            measurements.Add(new MetricMeasurement(instrument.Name, measurement, measurementTags));
        });
        meterListener.Start();

        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 1;
            c.MaximumTotalCachedEventCount = 2;
        });
        var cachedStreamId = Guid.NewGuid().ToString();
        var replacingStreamId = Guid.NewGuid().ToString();
        database.Add(cachedStreamId, new NumberBeerBrewedEvent(1), new NumberBeerBrewedEvent(2));
        database.Add(replacingStreamId, new NumberBeerBrewedEvent(3));

        await repository.GetAsync(cachedStreamId);
        await repository.GetAsync(cachedStreamId);
        await repository.GetAsync(replacingStreamId);

        Assert.Equal(1L, SumMeasurements(measurements, "eventforging.event_stream_cache.lookup", "ef.cache.lookup.result", "hit"));
        Assert.Equal(2L, SumMeasurements(measurements, "eventforging.event_stream_cache.lookup", "ef.cache.lookup.result", "miss"));
        Assert.Equal(2L, SumMeasurements(measurements, "eventforging.event_stream_cache.events_served"));
        Assert.Equal(1L, SumMeasurements(measurements, "eventforging.event_stream_cache.cached_streams"));
        Assert.Equal(1L, SumMeasurements(measurements, "eventforging.event_stream_cache.cached_events"));
        Assert.Equal(1L, SumMeasurements(measurements, "eventforging.event_stream_cache.entry_removal", "ef.cache.entry_removal.reason", "event_count_limit"));
        Assert.All(measurements, m => Assert.Equal(nameof(BreweryAggregate), m.Tags["ef.aggregate.type"]));
    }

    [Fact]
    public async Task when_incremental_read_reports_deleted_stream_then_cached_events_are_removed()
    {
        var (repository, database, _) = CreateRepository(c =>
        {
            c.MinimumEventCount = 1;
        });
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(aggregateId, new NumberBeerBrewedEvent(1));

        await repository.GetAsync(aggregateId);
        database.Delete(aggregateId);

        Assert.Null(await repository.TryGetAsync(aggregateId));
        Assert.Null(await repository.TryGetAsync(aggregateId));
        Assert.Equal(new AggregateVersion?[] { null, 0, null, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task concurrent_memory_cache_write_sessions_do_not_move_cached_stream_backwards()
    {
        using var serviceProvider = CreateMemoryEventStreamCacheServiceProvider();
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();
        var longerWriteSession = await sessionFactory.TryCreateWriteSessionAsync<BreweryAggregate>(aggregateId);
        var shorterWriteSession = await sessionFactory.TryCreateWriteSessionAsync<BreweryAggregate>(aggregateId);
        Assert.NotNull(longerWriteSession);
        Assert.NotNull(shorterWriteSession);
        await AppendNumbersAsync(longerWriteSession, 0, 1, 2, 3, 4);
        await AppendNumbersAsync(shorterWriteSession, 0, 1, 2);

        await Task.WhenAll(
            Task.Run(() => longerWriteSession.CompleteAsync()),
            Task.Run(() => shorterWriteSession.CompleteAsync()));

        var readSession = await sessionFactory.TryCreateReadSessionAsync<BreweryAggregate>(aggregateId);
        Assert.NotNull(readSession);
        Assert.Equal(3, readSession.Version.Value);
        Assert.Equal(new[] { 1, 2, 3, 4, }, await GetCachedNumbersAsync(readSession));
    }

    [Fact]
    public async Task memory_cache_read_session_keeps_its_snapshot_when_the_stream_is_extended()
    {
        using var serviceProvider = CreateMemoryEventStreamCacheServiceProvider();
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();
        await StoreNumbersAsync(sessionFactory, aggregateId, 0, 1, 2);
        var existingReadSession = await sessionFactory.TryCreateReadSessionAsync<BreweryAggregate>(aggregateId);
        Assert.NotNull(existingReadSession);

        await StoreNumbersAsync(sessionFactory, aggregateId, 2, 3, 4);

        var updatedReadSession = await sessionFactory.TryCreateReadSessionAsync<BreweryAggregate>(aggregateId);
        Assert.NotNull(updatedReadSession);
        Assert.Equal(1, existingReadSession.Version.Value);
        Assert.Equal(new[] { 1, 2, }, await GetCachedNumbersAsync(existingReadSession));
        Assert.Equal(3, updatedReadSession.Version.Value);
        Assert.Equal(new[] { 1, 2, 3, 4, }, await GetCachedNumbersAsync(updatedReadSession));
    }

    [Fact]
    public async Task removing_a_memory_cache_entry_does_not_break_an_existing_read_session()
    {
        using var serviceProvider = CreateMemoryEventStreamCacheServiceProvider();
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateId = Guid.NewGuid().ToString();
        await StoreNumbersAsync(sessionFactory, aggregateId, 0, 1, 2);
        var existingReadSession = await sessionFactory.TryCreateReadSessionAsync<BreweryAggregate>(aggregateId);
        Assert.NotNull(existingReadSession);

        var invalidator = serviceProvider.GetRequiredService<IEventStreamCacheInvalidator>();
        await invalidator.InvalidateAsync<BreweryAggregate>(aggregateId);

        Assert.Null(await sessionFactory.TryCreateReadSessionAsync<BreweryAggregate>(aggregateId));
        Assert.Equal(new[] { 1, 2, }, await GetCachedNumbersAsync(existingReadSession));
    }

    [Fact]
    public async Task parallel_memory_cache_reads_and_updates_return_consistent_snapshots()
    {
        using var serviceProvider = CreateMemoryEventStreamCacheServiceProvider();
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateIds = Enumerable.Range(0, 12).Select(_ => Guid.NewGuid().ToString()).ToArray();
        const int initialEventCount = 10;
        const int finalEventCount = 60;
        foreach (var aggregateId in aggregateIds)
        {
            await StoreNumbersAsync(sessionFactory, aggregateId, 0, Enumerable.Range(0, initialEventCount).ToArray());
        }

        var writers = aggregateIds.Select(aggregateId => Task.Run(async () =>
        {
            for (var eventVersion = initialEventCount; eventVersion < finalEventCount; ++eventVersion)
            {
                await StoreNumbersAsync(sessionFactory, aggregateId, eventVersion, eventVersion);
            }
        }));
        var readers = Enumerable.Range(0, 6).Select(readerIndex => Task.Run(async () =>
        {
            for (var iteration = 0; iteration < 250; ++iteration)
            {
                var aggregateId = aggregateIds[(readerIndex + iteration) % aggregateIds.Length];
                var readSession = await sessionFactory.TryCreateReadSessionAsync<BreweryAggregate>(aggregateId);
                Assert.NotNull(readSession);
                var numbers = await GetCachedNumbersAsync(readSession);
                Assert.Equal(readSession.Version.Next().Value, numbers.LongLength);
                for (var eventIndex = 0; eventIndex < numbers.Length; ++eventIndex)
                {
                    Assert.Equal(eventIndex, numbers[eventIndex]);
                }
            }
        }));

        await Task.WhenAll(writers.Concat(readers));

        foreach (var aggregateId in aggregateIds)
        {
            var readSession = await sessionFactory.TryCreateReadSessionAsync<BreweryAggregate>(aggregateId);
            Assert.NotNull(readSession);
            Assert.Equal(finalEventCount - 1, readSession.Version.Value);
            Assert.Equal(Enumerable.Range(0, finalEventCount), await GetCachedNumbersAsync(readSession));
        }
    }

    [Fact]
    public async Task parallel_memory_cache_writes_respect_the_stream_count_limit()
    {
        using var serviceProvider = CreateMemoryEventStreamCacheServiceProvider(
            configureMemoryCache: configuration => configuration.MaximumCachedStreamCount = 10);
        var sessionFactory = serviceProvider.GetRequiredService<IEventStreamCacheSessionFactory>();
        var aggregateIds = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid().ToString()).ToArray();

        await Task.WhenAll(aggregateIds.Select((aggregateId, eventNumber) =>
            Task.Run(() => StoreNumbersAsync(sessionFactory, aggregateId, 0, eventNumber))));

        var cachedStreamCount = 0;
        foreach (var aggregateId in aggregateIds)
        {
            if (await sessionFactory.TryCreateReadSessionAsync<BreweryAggregate>(aggregateId) is not null)
            {
                ++cachedStreamCount;
            }
        }

        Assert.Equal(10, cachedStreamCount);
    }

    [Fact]
    public async Task custom_cache_can_be_registered()
    {
        var physicalCache = new RecordingEventStreamCache();
        var sessionFactory = new RecordingEventStreamCacheSessionFactory(physicalCache);
        var (repository, database, _) = CreateRepository(
            useMemoryCache: false,
            customCache: (sessionFactory, physicalCache));
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(aggregateId, new NumberBeerBrewedEvent(1));

        await repository.GetAsync(aggregateId);
        await repository.GetAsync(aggregateId);

        Assert.Equal(2, sessionFactory.TryCreateReadSessionCallCount);
        Assert.Equal(1, sessionFactory.TryCreateWriteSessionCallCount);
        Assert.Equal(1, sessionFactory.AppendCallCount);
        Assert.Equal(1, sessionFactory.CompleteCallCount);
        Assert.Equal(new AggregateVersion?[] { null, 0, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public void cache_session_factory_and_invalidator_must_be_registered_together()
    {
        var servicesWithOnlySessionFactory = new ServiceCollection();
        Assert.Throws<EventForgingConfigurationException>(() =>
            servicesWithOnlySessionFactory.AddEventForging(registration =>
                registration.Services.AddSingleton<IEventStreamCacheSessionFactory>(
                    new FailingEventStreamCacheSessionFactory())));

        var servicesWithOnlyInvalidator = new ServiceCollection();
        Assert.Throws<EventForgingConfigurationException>(() =>
            servicesWithOnlyInvalidator.AddEventForging(registration =>
                registration.Services.AddSingleton<IEventStreamCacheInvalidator>(
                    new NoOpEventStreamCacheInvalidator())));
    }

    [Fact]
    public async Task database_events_are_passed_to_the_cache_while_the_stream_is_being_read()
    {
        var physicalCache = new RecordingEventStreamCache();
        var sessionFactory = new RecordingEventStreamCacheSessionFactory(physicalCache);
        var (repository, database, _) = CreateRepository(
            useMemoryCache: false,
            customCache: (sessionFactory, physicalCache));
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(
            aggregateId,
            new NumberBeerBrewedEvent(1),
            new NumberBeerBrewedEvent(2),
            new NumberBeerBrewedEvent(3));
        database.AfterEventYielded = yieldedEventCount => Assert.Equal(yieldedEventCount, sessionFactory.AppendCallCount);

        await repository.GetAsync(aggregateId);

        Assert.Equal(3, sessionFactory.AppendCallCount);
    }

    [Fact]
    public async Task cache_failure_falls_back_to_reading_the_entire_stream()
    {
        var (repository, database, _) = CreateRepository(
            useMemoryCache: false,
            customCache: (
                new FailingEventStreamCacheSessionFactory(),
                new NoOpEventStreamCacheInvalidator()));
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(aggregateId, new NumberBeerBrewedEvent(1));

        await repository.GetAsync(aggregateId);
        await repository.GetAsync(aggregateId);

        Assert.Equal(new AggregateVersion?[] { null, null, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task when_cache_stream_fails_then_partial_aggregate_is_discarded_and_database_is_read_from_beginning()
    {
        var (repository, database, _) = CreateRepository(
            useMemoryCache: false,
            customCache: (
                new FailingReadEventStreamCacheSessionFactory(),
                new NoOpEventStreamCacheInvalidator()));
        var aggregateId = Guid.NewGuid().ToString();
        database.Add(aggregateId, new TextBeerBrewedEvent("database"));

        var aggregate = await repository.GetAsync(aggregateId);

        Assert.Equal(0, aggregate.NumberBeerBrewed);
        Assert.Equal("database", aggregate.TextBeerBrewed);
        Assert.Equal(new AggregateVersion?[] { null, }, database.ReadsFor(aggregateId));
    }

    [Fact]
    public async Task when_cached_event_count_does_not_match_its_version_then_database_is_read_from_beginning()
    {
        var physicalCache = new RecordingEventStreamCache();
        var sessionFactory = new RecordingEventStreamCacheSessionFactory(physicalCache);
        var aggregateId = Guid.NewGuid().ToString();
        physicalCache.CompleteWrite(
            (typeof(BreweryAggregate), aggregateId),
            new object[] { new NumberBeerBrewedEvent(1), },
            AggregateVersion.FromValue(1),
            true);
        var (repository, database, _) = CreateRepository(
            useMemoryCache: false,
            customCache: (sessionFactory, physicalCache));
        database.Add(aggregateId, new TextBeerBrewedEvent("database"));

        var aggregate = await repository.GetAsync(aggregateId);

        Assert.Equal(0, aggregate.NumberBeerBrewed);
        Assert.Equal("database", aggregate.TextBeerBrewed);
        Assert.Equal(new AggregateVersion?[] { null, }, database.ReadsFor(aggregateId));
    }

    private static (IRepository<BreweryAggregate> Repository, RecordingEventDatabase Database, IMemoryEventStreamCacheConfiguration? Configuration) CreateRepository(
        Action<IMemoryEventStreamCacheConfiguration>? configureCache = null,
        bool useMemoryCache = true,
        (IEventStreamCacheSessionFactory SessionFactory, IEventStreamCacheInvalidator Invalidator)? customCache = null)
    {
        var services = new ServiceCollection();
        services.AddEventForging(registration =>
        {
            if (customCache is not null)
            {
                registration.Services.AddSingleton(customCache.Value.SessionFactory);
                registration.Services.AddSingleton(customCache.Value.Invalidator);
            }
            else if (useMemoryCache)
            {
                registration.UseMemoryEventStreamCache(configureCache);
            }
        });

        var database = new RecordingEventDatabase();
        services.AddSingleton<IEventDatabase>(database);
        var serviceProvider = services.BuildServiceProvider();

        return (
            serviceProvider.GetRequiredService<IRepository<BreweryAggregate>>(),
            database,
            serviceProvider.GetService<IMemoryEventStreamCacheConfiguration>());
    }

    private static ServiceProvider CreateMemoryEventStreamCacheServiceProvider(
        Action<IMemoryEventStreamCacheConfiguration>? configureMemoryCache = null)
    {
        var services = new ServiceCollection();
        services.AddEventForging(registration =>
        {
            registration.UseMemoryEventStreamCache(configuration =>
            {
                configuration.MinimumEventCount = 1;
                configureMemoryCache?.Invoke(configuration);
            });
        });
        return services.BuildServiceProvider();
    }

    private static async Task StoreNumbersAsync(
        IEventStreamCacheSessionFactory sessionFactory,
        string aggregateId,
        long firstEventVersion,
        params int[] numbers)
    {
        var writeSession = await sessionFactory.TryCreateWriteSessionAsync<BreweryAggregate>(aggregateId);
        Assert.NotNull(writeSession);
        await AppendNumbersAsync(writeSession, firstEventVersion, numbers);
        await writeSession.CompleteAsync();
    }

    private static async Task AppendNumbersAsync(
        IEventStreamCacheWriteSession writeSession,
        long firstEventVersion,
        params int[] numbers)
    {
        for (var eventIndex = 0; eventIndex < numbers.Length; ++eventIndex)
        {
            await writeSession.AppendAsync(
                new NumberBeerBrewedEvent(numbers[eventIndex]),
                AggregateVersion.FromValue(firstEventVersion + eventIndex));
        }
    }

    private static async Task<int[]> GetCachedNumbersAsync(IEventStreamCacheReadSession readSession)
    {
        var numbers = new List<int>();
        await foreach (var @event in readSession.GetEventsAsync())
        {
            numbers.Add(((NumberBeerBrewedEvent)@event).Number);
        }

        return numbers.ToArray();
    }

    private static long SumMeasurements(IReadOnlyCollection<MetricMeasurement> measurements, string instrumentName, string? tagName = null, string? tagValue = null)
    {
        return measurements
            .Where(m => m.InstrumentName == instrumentName
                        && (tagName is null || m.Tags.TryGetValue(tagName, out var value) && Equals(value, tagValue)))
            .Sum(m => m.Value);
    }

    private sealed class RecordingEventStreamCacheSessionFactory : IEventStreamCacheSessionFactory
    {
        private readonly RecordingEventStreamCache _cache;

        public RecordingEventStreamCacheSessionFactory(RecordingEventStreamCache cache)
        {
            _cache = cache;
        }

        public int TryCreateReadSessionCallCount { get; private set; }
        public int TryCreateWriteSessionCallCount { get; private set; }
        public int AppendCallCount => _cache.AppendCallCount;
        public int CompleteCallCount => _cache.CompleteCallCount;

        public Task<IEventStreamCacheReadSession?> TryCreateReadSessionAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default)
        {
            ++TryCreateReadSessionCallCount;
            if (!_cache.TryGet((typeof(TAggregate), aggregateId), out var cachedEventStream))
            {
                return Task.FromResult<IEventStreamCacheReadSession?>(null);
            }

            return Task.FromResult<IEventStreamCacheReadSession?>(new ReadSession(cachedEventStream.Events, cachedEventStream.Version));
        }

        public Task<IEventStreamCacheWriteSession?> TryCreateWriteSessionAsync<TAggregate>(
            string aggregateId,
            CancellationToken cancellationToken = default)
        {
            ++TryCreateWriteSessionCallCount;
            var key = (typeof(TAggregate), aggregateId);
            _cache.TryGet(key, out var cachedEventStream);
            return Task.FromResult<IEventStreamCacheWriteSession?>(new WriteSession(_cache, key, cachedEventStream));
        }

        private sealed class ReadSession : IEventStreamCacheReadSession
        {
            private readonly IReadOnlyList<object> _events;

            public ReadSession(IReadOnlyList<object> events, AggregateVersion version)
            {
                _events = events;
                Version = version;
            }

            public AggregateVersion Version { get; }

            public IAsyncEnumerable<object> GetEventsAsync(CancellationToken cancellationToken = default)
            {
                return ReadEvents(_events, cancellationToken);
            }
        }

        private sealed class WriteSession : IEventStreamCacheWriteSession
        {
            private readonly RecordingEventStreamCache _cache;
            private readonly (Type AggregateType, string AggregateId) _key;
            private readonly List<object> _events = new();
            private AggregateVersion? _lastVersion;
            private bool _eventAppended;

            public WriteSession(
                RecordingEventStreamCache cache,
                (Type AggregateType, string AggregateId) key,
                RecordingEventStreamCache.StoredEventStream? cachedEventStream)
            {
                _cache = cache;
                _key = key;
                if (cachedEventStream is not null)
                {
                    _events.AddRange(cachedEventStream.Events);
                    _lastVersion = cachedEventStream.Version;
                }
            }

            public Task AppendAsync(object @event, AggregateVersion version, CancellationToken cancellationToken = default)
            {
                _cache.RecordEventAppended();
                _events.Add(@event);
                _lastVersion = version;
                _eventAppended = true;
                return Task.CompletedTask;
            }

            public Task CompleteAsync(CancellationToken cancellationToken = default)
            {
                _cache.CompleteWrite(_key, _events, _lastVersion, _eventAppended);
                return Task.CompletedTask;
            }
        }

        private static async IAsyncEnumerable<object> ReadEvents(
            IReadOnlyList<object> events,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var @event in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return @event;
            }
        }
    }

    private sealed class RecordingEventStreamCache : IEventStreamCacheInvalidator
    {
        private readonly Dictionary<(Type AggregateType, string AggregateId), StoredEventStream> _cachedEventStreams = new();

        public int AppendCallCount { get; private set; }
        public int CompleteCallCount { get; private set; }

        public Task InvalidateAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default)
        {
            _cachedEventStreams.Remove((typeof(TAggregate), aggregateId));
            return Task.CompletedTask;
        }

        internal bool TryGet(
            (Type AggregateType, string AggregateId) key,
            [NotNullWhen(true)] out StoredEventStream? cachedEventStream)
        {
            return _cachedEventStreams.TryGetValue(key, out cachedEventStream);
        }

        internal void RecordEventAppended()
        {
            ++AppendCallCount;
        }

        internal void CompleteWrite(
            (Type AggregateType, string AggregateId) key,
            IReadOnlyCollection<object> events,
            AggregateVersion? lastVersion,
            bool eventAppended)
        {
            ++CompleteCallCount;
            if (eventAppended && lastVersion is not null)
            {
                _cachedEventStreams[key] = new StoredEventStream(events.ToArray(), lastVersion.Value);
            }
        }

        internal sealed record StoredEventStream(IReadOnlyList<object> Events, AggregateVersion Version);
    }

    private sealed class FailingReadEventStreamCacheSessionFactory : IEventStreamCacheSessionFactory
    {
        public Task<IEventStreamCacheReadSession?> TryCreateReadSessionAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEventStreamCacheReadSession?>(new ReadSession());
        }

        public Task<IEventStreamCacheWriteSession?> TryCreateWriteSessionAsync<TAggregate>(
            string aggregateId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEventStreamCacheWriteSession?>(new WriteSession());
        }

        private sealed class ReadSession : IEventStreamCacheReadSession
        {
            public AggregateVersion Version => AggregateVersion.FromValue(1);

            public async IAsyncEnumerable<object> GetEventsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new NumberBeerBrewedEvent(999);
                throw new InvalidOperationException("Cache stream failed.");
            }
        }

        private sealed class WriteSession : IEventStreamCacheWriteSession
        {
            public Task AppendAsync(object @event, AggregateVersion version, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task CompleteAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }
    }

    private sealed class FailingEventStreamCacheSessionFactory : IEventStreamCacheSessionFactory
    {
        public Task<IEventStreamCacheReadSession?> TryCreateReadSessionAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Cache is unavailable.");
        }

        public Task<IEventStreamCacheWriteSession?> TryCreateWriteSessionAsync<TAggregate>(
            string aggregateId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEventStreamCacheWriteSession?>(null);
        }

    }

    private sealed class NoOpEventStreamCacheInvalidator : IEventStreamCacheInvalidator
    {
        public Task InvalidateAsync<TAggregate>(string aggregateId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEventDatabase : IEventDatabase
    {
        private readonly Dictionary<string, List<object>> _events = new();
        private readonly HashSet<string> _deletedStreams = new();
        private readonly List<Read> _reads = new();

        public Action<int>? AfterEventYielded { get; set; }

        public void Add(string aggregateId, params object[] events)
        {
            if (!_events.TryGetValue(aggregateId, out var stream))
            {
                stream = new List<object>();
                _events.Add(aggregateId, stream);
            }

            stream.AddRange(events);
            _deletedStreams.Remove(aggregateId);
        }

        public void Delete(string aggregateId)
        {
            _events.Remove(aggregateId);
            _deletedStreams.Add(aggregateId);
        }

        public AggregateVersion?[] ReadsFor(string aggregateId)
        {
            return _reads.Where(r => r.AggregateId == aggregateId).Select(r => r.AfterVersion).ToArray();
        }

        public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var e in ReadAsync<TAggregate>(aggregateId, EventStreamReadPosition.Beginning, cancellationToken))
            {
                yield return e;
            }
        }

        public async IAsyncEnumerable<object> ReadAsync<TAggregate>(string aggregateId, EventStreamReadPosition readPosition, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            AggregateVersion? afterVersion = readPosition.TryGetAfterVersion(out var version) ? version : null;
            _reads.Add(new Read(aggregateId, afterVersion));
            await foreach (var e in ReadEventsAsync(aggregateId, readPosition, cancellationToken))
            {
                yield return e;
            }
        }

        public async IAsyncEnumerable<EventDatabaseRecord> ReadRecordsAsync<TAggregate>(string aggregateId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var eventNumber = 0L;
            await foreach (var e in ReadAsync<TAggregate>(aggregateId, cancellationToken))
            {
                yield return CreateRecord(eventNumber, e);
                ++eventNumber;
            }
        }

        public Task WriteAsync<TAggregate>(string aggregateId, IReadOnlyList<object> events, AggregateVersion retrievedVersion, ExpectedVersion expectedVersion, Guid conversationId, Guid initiatorId, IDictionary<string, string> customProperties, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        private async IAsyncEnumerable<object> ReadEventsAsync(string aggregateId, EventStreamReadPosition readPosition, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var readsAfterVersion = readPosition.TryGetAfterVersion(out var afterVersion);
            if (readsAfterVersion && _deletedStreams.Contains(aggregateId))
            {
                throw new AggregateNotFoundEventForgingException(typeof(BreweryAggregate), aggregateId);
            }

            if (_events.TryGetValue(aggregateId, out var stream))
            {
                var firstEventNumber = readsAfterVersion ? afterVersion.Next().Value : 0L;
                for (var eventNumber = firstEventNumber; eventNumber < stream.Count; ++eventNumber)
                {
                    yield return stream[(int)eventNumber];
                    AfterEventYielded?.Invoke((int)(eventNumber - firstEventNumber + 1L));
                }
            }

            await Task.CompletedTask;
        }

        private static EventDatabaseRecord CreateRecord(long eventNumber, object e)
        {
            return new EventDatabaseRecord(Guid.NewGuid(), eventNumber, e.GetType().FullName!, DateTime.UtcNow, e, Guid.Empty, Guid.Empty, new Dictionary<string, string>());
        }

        private sealed record Read(string AggregateId, AggregateVersion? AfterVersion);
    }

    private sealed record MetricMeasurement(string InstrumentName, long Value, IReadOnlyDictionary<string, object?> Tags);
}
