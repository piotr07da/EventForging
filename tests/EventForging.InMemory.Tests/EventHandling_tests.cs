// ReSharper disable InconsistentNaming

using System.Diagnostics;
using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Diagnostics;
using EventForging.Diagnostics.Tracing;
using EventForging.InMemory.Diagnostics;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace EventForging.InMemory.Tests;

public sealed class EventHandling_tests : IAsyncLifetime
{
    private readonly IHost _host;
    private readonly EventHandlingTestFixture _fixture;

    private readonly ICollection<Activity> _tracing = new List<Activity>();
    private readonly TracerProvider _tracerProvider;

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
                    r.UseInMemory(c =>
                    {
                        c.SerializationEnabled = true;
                        c.AddEventSubscription("TestSubscription");
                        c.AddEventSubscription("FailingTestSubscription");
                    });
                    r.AddEventHandlers(assembly);
                });
                services.AddSingleton<EventHandlingTestFixture>();
            });

        _host = hostBuilder.Build();
        _fixture = _host.Services.GetRequiredService<EventHandlingTestFixture>();

        _tracerProvider = Sdk
            .CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(nameof(EventHandling_tests)))
            .AddSource(EventForgingDiagnosticsInfo.TracingSourceName)
            .AddSource(EventForgingInMemoryDiagnosticsInfo.TracingSourceName)
            .AddInMemoryExporter(_tracing)
            .Build();
    }

    public async Task InitializeAsync()
    {
        _tracing.Clear();
        await _host.StartAsync();
    }

    public async Task DisposeAsync()
    {
        _tracerProvider.Dispose();
        await _host.StopAsync();
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1000, false)]
    public async Task when_aggregate_saved_then_events_handled(int amountOfCounterEvents, bool checkTracingContinuity)
    {
        await _fixture.when_aggregate_saved_then_events_handled(TimeSpan.FromSeconds(5), amountOfCounterEvents);

        if (checkTracingContinuity)
        {
            Assert.All(_tracing.Where(a => a.OperationName == TracingActivityNames.EventDispatcherDispatch), a => Assert.NotNull(a.ParentId));
            Assert.All(_tracing.Where(a => a.OperationName == Diagnostics.Tracing.TracingActivityNames.SubscriptionReceiveEvent), a => Assert.NotNull(a.ParentId));
        }
    }

    [Fact]
    public async Task when_aggregate_saved_then_events_handled_by_failing_handler_and_keeps_retrying_until_success()
    {
        await _fixture.when_aggregate_saved_then_events_handled_by_failing_handler_and_keeps_retrying_until_success(3, 3, TimeSpan.FromSeconds(5), TimeSpan.Zero);
    }
}
