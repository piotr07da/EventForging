// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
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
    private const string ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "TestModule_CosmosDbEventDatabase_tests";
    private const string ContainerName = "TestModule-Events";

    private readonly IHost _host;
    private readonly EventDatabaseTestFixture _fixture;
    private readonly CosmosClient _cosmosClient;

    public CosmosDbEventDatabase_tests()
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
                        cc.IgnoreServerCertificateValidation = true;
                        cc.ConnectionString = ConnectionString;
                        cc.AddAggregatesLocations(DatabaseName, ContainerName, assembly);
                    });
                });
                services.AddSingleton<EventDatabaseTestFixture>();
            });

        _host = hostBuilder.Build();
        _fixture = _host.Services.GetRequiredService<EventDatabaseTestFixture>();
        _cosmosClient = CreateCosmosClient();
    }

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();

        var db = _cosmosClient.GetDatabase(DatabaseName);
        await db.DeleteAsync();
    }

    [Fact]
    public async Task then_database_created()
    {
        var database = GetDatabase();
        await database.ReadAsync();
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

    private Database GetDatabase()
    {
        return _cosmosClient.GetDatabase(DatabaseName);
    }

    private static CosmosClient CreateCosmosClient()
    {
        return new CosmosClient(ConnectionString, new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true, }),
            ConnectionMode = ConnectionMode.Gateway,
        });
    }
}
