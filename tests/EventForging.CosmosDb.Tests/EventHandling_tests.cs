// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using User = EventForging.DatabaseIntegrationTests.Common.User;

namespace EventForging.CosmosDb.Tests;

[Trait("Category", "Flaky")]
public sealed class EventHandling_tests : IAsyncLifetime
{
    private const string ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "TestModule_EventHandling_tests";
    private const string EventsContainerName = "TestModule-Events";
    private const string SubscriptionName = "TestSubscription";
    private const string ChangeFeedName = "testSubscriptionFeed";
    private const string FailingSubscriptionName = "FailingTestSubscription";
    private const string FailingChangeFeedName = "testFailingSubscriptionFeed";

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

                        cc.EnableEventPacking = true;

                        cc.AddAggregateLocations(DatabaseName, EventsContainerName, assembly);

                        cc.AddEventsSubscription(SubscriptionName, DatabaseName, EventsContainerName, ChangeFeedName, null);
                        cc.AddEventsSubscription(FailingSubscriptionName, DatabaseName, EventsContainerName, FailingChangeFeedName, null);
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

    [Theory]
    [InlineData(50)]
    [InlineData(250)]
    public async Task when_aggregate_saved_then_events_handled(int amountOfCounterEvents)
    {
        await _fixture.when_aggregate_saved_then_events_handled(TimeSpan.FromSeconds(20), amountOfCounterEvents);
    }

    [Fact]
    public async Task when_aggregate_saved_then_events_handled_by_failing_handler_and_keeps_retrying_until_success()
    {
        // This is specific for cosmos db:
        // From: https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/change-feed-processor?tabs=dotnet#error-handling
        // There is only one scenario where a batch of changes will not be retried.
        // If the failure happens on the first ever delegate execution, the lease store has no previous saved state to be used on the retry.
        // On those cases, the retry would use the initial starting configuration, which might or might not include the last batch.
        // Therefore first event will not be retried. Other events handling will succeed at try number 3.
        await _fixture.when_aggregate_saved_then_events_handled_by_failing_handler_and_keeps_retrying_until_success(1, 3, TimeSpan.FromSeconds(15));
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
