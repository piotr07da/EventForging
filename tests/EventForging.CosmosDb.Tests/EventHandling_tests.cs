// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using User = EventForging.DatabaseIntegrationTests.Common.User;

namespace EventForging.CosmosDb.Tests;

public sealed class EventHandling_tests : IAsyncLifetime
{
    private const string ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "TestModule";
    private const string EventsContainerName = "TestModule-Events";
    private const string SubscriptionName = "TestSubscription";
    private const string ChangeFeedName = "testSubscriptionFeed";

    private readonly IHost _host;
    private readonly EventHandlingTestFixture _fixture;
    private readonly CosmosClient _cosmosClient;

    public EventHandling_tests()
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

                        cc.AddAggregatesLocations(DatabaseName, EventsContainerName, assembly);

                        cc.AddEventsSubscription(SubscriptionName, DatabaseName, EventsContainerName, ChangeFeedName, null);
                    });
                    r.AddEventHandlers(assembly);
                });
                services.AddSingleton<EventHandlingTestFixture>();
            });

        _host = hostBuilder.Build();
        _fixture = _host.Services.GetRequiredService<EventHandlingTestFixture>();
        _cosmosClient = CreateCosmosClient();
        ReadModel.Initialize();
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
    public async Task when_aggregate_saved_then_events_handled()
    {
        await _fixture.when_aggregate_saved_then_events_handled();
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
