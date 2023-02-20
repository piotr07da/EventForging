﻿// ReSharper disable InconsistentNaming

using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventForging.InMemory.Tests;

public sealed class EventHandling_tests : IAsyncLifetime
{
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
                    r.UseInMemory(c =>
                    {
                        c.SerializationEnabled = true;
                        c.AddEventSubscription("TestSubscription");
                    });
                    r.AddEventHandlers(assembly);
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
    }

    public async Task DisposeAsync()
    {
        await _host.StopAsync();
    }

    [Fact]
    public async Task when_aggregate_saved_then_events_handled()
    {
        await _fixture.when_aggregate_saved_then_events_handled();
    }
}
