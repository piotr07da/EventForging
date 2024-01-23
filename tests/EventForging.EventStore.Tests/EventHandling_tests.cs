// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Serialization;
using EventStore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventForging.EventStore.Tests;

[Trait("Category", "Integration")]
[Trait("Category", "Flaky")]
public sealed class EventHandling_tests : IAsyncLifetime
{
    private const string ConnectionString = "esdb://localhost:2113?tls=false";
    private const string EventsStreamIdPrefix = "tests";
    private const string SubscriptionStreamId = "from-category-tests";
    private const string SubscriptionName = "TestSubscription";
    private const string SubscriptionGroupName = "test-group";
    private const string FailingSubscriptionName = "FailingTestSubscription";
    private const string FailingSubscriptionGroupName = "failing-test-group";
    private const string ProjectionName = "from-category-tests-projection";

    private const string ProjectionQuery = $@"
options({{
    resultStreamName: ""{SubscriptionStreamId}"",
        $includeLinks: false,
    reorderEvents: false,
    processingLag: 0
}})
fromCategory('{EventsStreamIdPrefix}')
    .when({{
    $any: function(state, event) {{
        linkTo(""{SubscriptionStreamId}"", event);
    }}
}})
";

    private readonly IHost _host;
    private readonly EventHandlingTestFixture _fixture;

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
                    r.UseEventStore(cc =>
                    {
                        cc.Address = ConnectionString;

                        cc.SetStreamIdFactory((aggregateType, aggregateId) => $"{EventsStreamIdPrefix}-{aggregateType.Name}-{aggregateId}");

                        cc.AddEventsSubscription(SubscriptionName, SubscriptionStreamId, SubscriptionGroupName);
                        cc.AddEventsSubscription(FailingSubscriptionName, SubscriptionStreamId, FailingSubscriptionGroupName);
                    });
                    r.AddEventHandlers(assembly);
                });
                services.AddEventStoreProjectionManagementClient(css =>
                {
                    css.ConnectivitySettings.Address = new Uri(ConnectionString);
                });
                services.AddSingleton<EventHandlingTestFixture>();
            });

        _host = hostBuilder.Build();
        _fixture = _host.Services.GetRequiredService<EventHandlingTestFixture>();
        ReadModel.Initialize();
    }

    public async Task InitializeAsync()
    {
        await _host.StartAsync();
        var projectionManagementClient = _host.Services.GetRequiredService<EventStoreProjectionManagementClient>();
        try
        {
            await projectionManagementClient.CreateContinuousAsync(ProjectionName, string.Empty);
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch
        {
            // This throws when collection already exists. In this test it doesn't matter.
        }
        finally
        {
            await projectionManagementClient.UpdateAsync(ProjectionName, ProjectionQuery, true);
        }
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
    }

    [Fact]
    public async Task when_aggregate_saved_then_events_handled()
    {
        await _fixture.when_aggregate_saved_then_events_handled(TimeSpan.FromSeconds(5), 200);
    }

    [Fact]
    public async Task when_aggregate_saved_then_events_handled_by_failing_handler_and_keeps_retrying_until_success()
    {
        await _fixture.when_aggregate_saved_then_events_handled_by_failing_handler_and_keeps_retrying_until_success(3, 3, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
    }
}
