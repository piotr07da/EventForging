// ReSharper disable InconsistentNaming

using System.Diagnostics;
using EventForging.DatabaseIntegrationTests.Common;
using EventForging.Diagnostics;
using EventForging.Diagnostics.Tracing;
using EventForging.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace EventForging.InMemory.Tests;

public class InMemoryEventDatabase_tests : IAsyncLifetime
{
    private readonly ICollection<Activity> _tracing = new List<Activity>();
    private readonly TracerProvider _tracerProvider;

    public InMemoryEventDatabase_tests()
    {
        _tracerProvider = Sdk
            .CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(nameof(InMemoryEventDatabase_tests)))
            .AddSource(EventForgingDiagnosticsInfo.TracingSourceName)
            .AddInMemoryExporter(_tracing)
            .Build();
    }

    public Task InitializeAsync()
    {
        _tracing.Clear();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _tracerProvider.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_new_aggregate_saved_then_read_aggregate_rehydrated(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_saved_then_read_aggregate_rehydrated();

        var repositorySaveActivity = _tracing.FirstOrDefault(a => a.OperationName == TracingActivityNames.RepositorySave);
        Assert.NotNull(repositorySaveActivity);
        Assert.Contains(repositorySaveActivity.Tags, t => t.Key == TracingAttributeNames.AggregateId);
        Assert.Contains(repositorySaveActivity.Tags, t => t is { Key: TracingAttributeNames.AggregateType, Value: nameof(User), });
        Assert.Contains(repositorySaveActivity.Tags, t => t.Key == TracingAttributeNames.AggregateVersion && t.Value == AggregateVersion.NotExistingAggregate.ToString());
        Assert.Contains(repositorySaveActivity.Tags, t => t.Key == TracingAttributeNames.AggregateEventsCount);
        Assert.Contains(repositorySaveActivity.Tags, t => t.Key == TracingAttributeNames.ExpectedVersion);
        Assert.Contains(repositorySaveActivity.Tags, t => t.Key == TracingAttributeNames.ConversationId);
        Assert.Contains(repositorySaveActivity.Tags, t => t.Key == TracingAttributeNames.InitiatorId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_new_aggregate_with_more_than_one_event_saved_then_read_aggregate_rehydrated(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_with_more_than_one_event_saved_then_read_aggregate_rehydrated(10);
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
    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_new_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_new_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving();

        var repositorySaveActivity = _tracing.FirstOrDefault(a => a.OperationName == TracingActivityNames.RepositorySave && a.Events.Any(e => e.Name == TracingAttributeNames.ExceptionEvent.Name));
        Assert.NotNull(repositorySaveActivity);
        var exceptionEvent = repositorySaveActivity.Events.First(e => e.Name == TracingAttributeNames.ExceptionEvent.Name);
        Assert.Contains(exceptionEvent.Tags, t => t is { Key: TracingAttributeNames.ExceptionEvent.ExceptionEscaped, Value: "true", });
        Assert.Contains(exceptionEvent.Tags, t => t is { Key: TracingAttributeNames.ExceptionEvent.ExceptionType, Value: nameof(EventForgingUnexpectedVersionException), });
        Assert.Contains(exceptionEvent.Tags, t => t.Key == TracingAttributeNames.ExceptionEvent.ExceptionMessage);
        Assert.Contains(exceptionEvent.Tags, t => t.Key == TracingAttributeNames.ExceptionEvent.ExceptionStackTrace);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Any_version_is_expected_then_NO_exception_thrown_during_second_saving();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving(bool serializationEnabled)
    {
        await Fixture(serializationEnabled).when_existing_aggregate_saved_twice_with_different_initiator_ids_and_Retrieved_version_is_expected_then_exception_thrown_during_second_saving();
    }

    private EventDatabaseTestFixture Fixture(bool serializationEnabled)
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
                cc.SetStreamIdFactory((t, aId) => $"tests-{t.Name}-{aId}");
            });
        });
        services.AddSingleton<EventDatabaseTestFixture>();

        var serviceProvider = services.BuildServiceProvider();

        return serviceProvider.GetRequiredService<EventDatabaseTestFixture>();
    }
}
