// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.InMemory.Tests;

public class when_ConfigureSerialization_and_UseCosmosDb
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_new_aggregate_saved_then_read_aggregate_rehydrated(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_saved_then_read_aggregate_rehydrated();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_new_aggregate_with_two_events_saved_then_read_aggregate_rehydrated(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_with_two_events_saved_then_read_aggregate_rehydrated();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_existing_aggregate_saved_then_read_aggregate_rehydrated(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_existing_aggregate_saved_then_read_aggregate_rehydrated();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Theory(Skip = "TODO")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Theory(Skip = "TODO")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_existing_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving();
    }

    private static EventDatabaseTestFixture Fixture(bool serializationEnabled)
    {
        var services = new ServiceCollection();
        var assembly = typeof(User).Assembly;
        services.AddEventForging(r =>
        {
            r.ConfigureEventForging(c =>
            {
                c.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(assembly));
            });
            r.UseInMemory(cc =>
            {
                cc.SerializationEnabled = serializationEnabled;
                cc.SetStreamNameFactory((t, aId) => $"tests-{t.Name}-{aId}");
            });
        });
        services.AddSingleton<EventDatabaseTestFixture>();
        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<EventDatabaseTestFixture>();
    }
}
