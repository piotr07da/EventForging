﻿// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
using EventForging.EventStore;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventForging.MongoDb.Tests;

[Trait("Category", "Integration")]
public class MongoDbEventDatabase_tests : IAsyncLifetime
{
    private const string ConnectionString = "mongodb://root:example@event-forging-tests-mongo-db:27017/";

    private readonly IHost _host;

    private readonly EventDatabaseTestFixture _fixture;

    public MongoDbEventDatabase_tests()
    {
        var hostBuilder = new HostBuilder()
            .ConfigureServices(services =>
            {
                var assembly = typeof(User).Assembly;
                services.AddEventForging(r =>
                {
                    r.ConfigureEventForging(c =>
                    {
                        c.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(assembly));
                    });
                    r.UseMongoDb(cc =>
                    {
                        cc.ConnectionString = ConnectionString;
                    });
                });
                services.AddSingleton<EventDatabaseTestFixture>();
            });

        _host = hostBuilder.Build();

        _fixture = _host.Services.GetRequiredService<EventDatabaseTestFixture>();
    }

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
    }

    [Fact]
    public async Task when_new_aggregate_saved_then_read_aggregate_rehydrated()
    {
        await _fixture.when_new_aggregate_saved_then_read_aggregate_rehydrated();
    }

    [Fact]
    public async Task when_new_aggregate_with_more_than_one_event_saved_then_read_aggregate_rehydrated()
    {
        await _fixture.when_new_aggregate_with_more_than_one_event_saved_then_read_aggregate_rehydrated(10);
    }

    [Fact]
    public async Task when_existing_aggregate_saved_then_read_aggregate_rehydrated()
    {
        await _fixture.when_existing_aggregate_saved_then_read_aggregate_rehydrated();
    }

    [Fact]
    public async Task when_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once()
    {
        await _fixture.when_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Fact]
    public async Task when_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once()
    {
        await _fixture.when_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Fact]
    public async Task when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once()
    {
        await _fixture.when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Fact]
    public async Task when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once()
    {
        await _fixture.when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Fact]
    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving()
    {
        await _fixture.when_new_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving();
    }

    [Fact]
    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving()
    {
        await _fixture.when_new_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving();
    }

    [Fact]
    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving()
    {
        await _fixture.when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving();
    }

    [Fact]
    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving()
    {
        await _fixture.when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving();
    }
}
