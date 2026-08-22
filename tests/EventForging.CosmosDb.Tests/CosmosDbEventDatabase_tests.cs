// ReSharper disable InconsistentNaming

using System.Diagnostics.Metrics;
using EventForging.DatabaseIntegrationTests.Common;
using EventForging.CosmosDb.Diagnostics;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using User = EventForging.DatabaseIntegrationTests.Common.User;

namespace EventForging.CosmosDb.Tests;

[Trait("Category", "Integration")]
public class CosmosDbEventDatabase_tests : IAsyncLifetime
{
    private const string DatabaseName = "TestModule_CosmosDbEventDatabase_tests";
    private const string ContainerName = "TestModule-Events";

    private IHost? _host;
    private EventDatabaseTestFixture? _fixture;
    private CosmosClient? _cosmosClient;

    private EventDatabaseTestFixture Fixture => _fixture ?? throw new Exception("Fixture is not initialized.");

    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync();
        }

        if (_cosmosClient is not null)
        {
            var db = _cosmosClient.GetDatabase(DatabaseName);
            await db.DeleteAsync();
        }
    }

    [Fact]
    public async Task then_database_created()
    {
        await InitFixtureAsync(EventPackingMode.Disabled);
        var database = GetDatabase();
        await database.ReadAsync();
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_new_aggregate_saved_then_read_aggregate_rehydrated(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_new_aggregate_saved_then_read_aggregate_rehydrated();
    }

    [Theory]
    [InlineData(EventPackingMode.Disabled, 33)]
    [InlineData(EventPackingMode.UniformDistributionFilling, 33)]
    [InlineData(EventPackingMode.UniformDistributionFilling, 133)]
    [InlineData(EventPackingMode.AllEventsInOnePacket, 33)]
    [InlineData(EventPackingMode.AllEventsInOnePacket, 133)]
    public async Task when_new_aggregate_with_more_than_one_event_saved_then_read_aggregate_rehydrated(EventPackingMode eventPacking, int amountOfCounterEvents)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_new_aggregate_with_more_than_one_event_saved_then_read_aggregate_rehydrated(amountOfCounterEvents);
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_existing_aggregate_saved_then_read_aggregate_rehydrated(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_existing_aggregate_saved_then_read_aggregate_rehydrated();
    }

    [Theory]
    [InlineData(EventPackingMode.Disabled)]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_reading_after_version_then_only_newer_events_are_returned(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_reading_after_version_then_only_newer_events_are_returned();
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    // DO NOT WORK ON EMULATOR
    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    [Trait("Category", "Flaky")]
    public async Task when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_new_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    // DO NOT WORK ON EMULATOR
    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    [Trait("Category", "Flaky")]
    public async Task when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_existing_aggregate_saved_many_times_in_parallel_with_the_same_initiator_id_then_its_events_written_to_the_database_only_once();
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_new_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving();
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_new_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving();
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving();
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving();
    }

    [Theory]
    [InlineData(EventPackingMode.UniformDistributionFilling)]
    [InlineData(EventPackingMode.AllEventsInOnePacket)]
    public async Task do_load_test(EventPackingMode eventPacking)
    {
        await InitFixtureAsync(eventPacking);
        await Fixture.do_load_test(100, 10);
    }

    [Fact]
    public async Task when_aggregate_saved_then_request_charge_metric_contains_configured_custom_property_tag()
    {
        var measurements = new List<(double Value, Dictionary<string, object?> Tags)>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == EventForgingCosmosDbDiagnosticsInfo.MeterName && instrument.Name == "eventforging.cosmosdb.event_database_operation.request_charge")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<double>((_, measurement, tags, _) =>
        {
            var measurementTags = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                measurementTags[tag.Key] = tag.Value;
            }

            measurements.Add((measurement, measurementTags));
        });
        meterListener.Start();

        await InitFixtureAsync(
            EventPackingMode.AllEventsInOnePacket,
            c => c.ConfigureEventDatabaseOperationRequestChargeMetric(m => m.AddTagForCustomProperty("initiatorName")));

        var userId = Guid.NewGuid();
        var user = User.Register(userId);
        var repository = _host!.Services.GetRequiredService<IRepository<User>>();
        await repository.SaveAsync(
            userId,
            user,
            ExpectedVersion.Retrieved,
            Guid.Empty,
            Guid.NewGuid(),
            new Dictionary<string, string> { { "initiatorName", "RegisterUser" }, });

        var writeMeasurement = Assert.Single(measurements.Where(m =>
            m.Tags.TryGetValue("ef.operation.name", out var operationName) && Equals(operationName, "write") &&
            m.Tags.TryGetValue("ef.custom_property.initiatorName", out var initiatorName) && Equals(initiatorName, "RegisterUser")));
        Assert.True(writeMeasurement.Value > 0);
        Assert.True(writeMeasurement.Tags.TryGetValue("ef.operation.type", out var operationType) && Equals(operationType, "write"));
        Assert.True(writeMeasurement.Tags.TryGetValue("ef.operation.result", out var operationResult) && Equals(operationResult, "success"));
        Assert.True(writeMeasurement.Tags.TryGetValue("ef.aggregate.type", out var aggregateType) && Equals(aggregateType, nameof(User)));
        Assert.True(writeMeasurement.Tags.TryGetValue("db.system", out var databaseSystem) && Equals(databaseSystem, "cosmosdb"));
        Assert.True(writeMeasurement.Tags.TryGetValue("db.cosmosdb.container", out var container) && Equals(container, ContainerName));
    }

    private Database GetDatabase()
    {
        if (_cosmosClient is null)
        {
            throw new Exception("CosmosClient is not initialized.");
        }

        return _cosmosClient.GetDatabase(DatabaseName);
    }

    private static CosmosClient CreateCosmosClient()
    {
        return new CosmosClient(ConnectionInfo.ConnectionString, new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true, }),
            ConnectionMode = ConnectionMode.Gateway,
        });
    }

    private async Task InitFixtureAsync(EventPackingMode eventPacking, Action<ICosmosDbEventForgingConfiguration>? configureCosmosDb = null)
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
                    r.UseCosmosDb(cc =>
                    {
                        cc.CreateDatabasesAndContainersIfNotExist = true;
                        cc.IgnoreServerCertificateValidation = true;
                        cc.ConnectionString = ConnectionInfo.ConnectionString;
                        cc.EventPacking = eventPacking;
                        cc.AddAggregateLocations(DatabaseName, ContainerName, assembly);
                        cc.SetStreamIdFactory((t, aId) => $"tests-{t.Name}-{aId}");
                        configureCosmosDb?.Invoke(cc);
                    });
                });
                services.AddSingleton<EventDatabaseTestFixture>();
            });

        _host = hostBuilder.Build();
        _fixture = _host.Services.GetRequiredService<EventDatabaseTestFixture>();
        _cosmosClient = CreateCosmosClient();

        await _host.StartAsync();
    }
}
