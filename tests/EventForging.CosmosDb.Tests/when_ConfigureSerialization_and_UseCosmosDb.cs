// ReSharper disable InconsistentNaming

using EventForging.CosmosDb.DependencyInjection;
using EventForging.DatabaseIntegrationTests.Common;
using EventForging.DependencyInjection;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using User = EventForging.DatabaseIntegrationTests.Common.User;

namespace EventForging.CosmosDb.Tests;

[Trait("Category", "Integration")]
public class when_ConfigureSerialization_and_UseCosmosDb : IAsyncLifetime
{
    private const string ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "TestModule";
    private const string ContainerName = "TestModule-Events";

    private readonly IServiceProvider _serviceProvider;
    private readonly CosmosClient _cosmosClient;

    private readonly EventDatabaseTestFixture _fixture;

    public when_ConfigureSerialization_and_UseCosmosDb()
    {
        var services = new ServiceCollection();
        var assembly = typeof(User).Assembly;
        services.AddEventForging(r =>
        {
            r.Configuration.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(assembly));
            r.UseCosmosDb(cc =>
            {
                cc.IgnoreServerCertificateValidation = true;
                cc.ConnectionString = ConnectionString;
                cc.AddAggregatesLocations(DatabaseName, ContainerName, assembly);
            });
        });
        services.AddSingleton<EventDatabaseTestFixture>();
        _serviceProvider = services.BuildServiceProvider();

        _cosmosClient = CreateCosmosClient();

        _fixture = _serviceProvider.GetRequiredService<EventDatabaseTestFixture>();
    }

    public async Task InitializeAsync()
    {
        var hs = _serviceProvider.GetRequiredService<IHostedService>();
        await hs.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        var hs = _serviceProvider.GetRequiredService<IHostedService>();
        await hs.StopAsync(CancellationToken.None);

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
