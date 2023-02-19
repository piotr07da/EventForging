// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventForging.EventStore.Tests;

[Trait("Category", "Integration")]
public class EventStoreEventDatabase_tests : IAsyncLifetime
{
    private const string ConnectionString = "esdb://localhost:2113?tls=false";

    private readonly IServiceProvider _serviceProvider;

    private readonly EventDatabaseTestFixture _fixture;

    public EventStoreEventDatabase_tests()
    {
        var services = new ServiceCollection();
        var assembly = typeof(User).Assembly;
        services.AddEventForging(r =>
        {
            r.ConfigureEventForging(c =>
            {
                c.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(assembly));
            });
            r.UseEventStore(cc =>
            {
                cc.Address = ConnectionString;
            });
        });
        services.AddSingleton<EventDatabaseTestFixture>();
        _serviceProvider = services.BuildServiceProvider();

        _fixture = _serviceProvider.GetRequiredService<EventDatabaseTestFixture>();
    }

    public async Task InitializeAsync()
    {
        var hs = _serviceProvider.GetRequiredService<IHostedService>();
        await hs.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task when_new_aggregate_saved_then_read_aggregate_rehydrated()
    {
        await _fixture.when_new_aggregate_saved_then_read_aggregate_rehydrated();
    }

    [Fact]
    public async Task when_new_aggregate_with_two_events_saved_then_read_aggregate_rehydrated()
    {
        await _fixture.when_new_aggregate_with_two_events_saved_then_read_aggregate_rehydrated();
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

    [Fact(Skip = "TODO")]
    public async Task when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once()
    {
        await _fixture.when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Fact(Skip = "TODO")]
    public async Task when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once()
    {
        await _fixture.when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Fact]
    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving()
    {
        await _fixture.when_new_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving();
    }

    [Fact]
    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving()
    {
        await _fixture.when_existing_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving();
    }
}
