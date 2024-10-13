using System.Diagnostics;
using EventForging.Diagnostics.Tracing;
using EventForging.EventsHandling;
using Xunit;

namespace EventForging.Tests.Diagnostics.Tracing;

// ReSharper disable once InconsistentNaming
public class ReceivedEventsBatchExtensions_tests
{
    private const string StreamId = "streamId1";
    private const string EventType = "EventType1";
    private const string NameSuffixForIterationActivities = "Test Iteration";

    private static readonly ActivitySource _testActivitySource = new("EventForging.Tests", "1.0.0");
    private readonly ICollection<Activity> _tracing;

    public ReceivedEventsBatchExtensions_tests()
    {
        _tracing = new List<Activity>();
        ActivitySource.AddActivityListener(new ActivityListener
        {
            ActivityStopped = a => { _tracing.Add(a); },
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        });
    }

    [Fact]
    public async Task given_batch_with_same_activity_id_for_every_event_when_IterateWithTracingRestoreAsync_then_activity_is_started_once()
    {
        // Arrange

        var receivedEvents = new List<ReceivedEvent>();

        var activity = _testActivitySource.StartActivity(ActivityKind.Internal);
        AddNewEventWithStoringOfCurrentActivityId(receivedEvents);
        activity?.Complete();

        var batch = new ReceivedEventsBatch(receivedEvents);

        // Act
        await batch.IterateWithTracingRestoreAsync(NameSuffixForIterationActivities, e => Task.CompletedTask);

        // Assert
        Assert.Single(_tracing.Where(a => a.DisplayName == NameSuffixForIterationActivities));
    }

    [Fact]
    public async Task given_batch_with_different_activity_id_for_every_event_when_IterateWithTracingRestoreAsync_then_activity_is_started_for_every_event()
    {
        // Arrange

        var expectedNumberOfRestoredActivities = 5;
        var receivedEvents = new List<ReceivedEvent>();

        for (var i = 0; i < expectedNumberOfRestoredActivities; ++i)
        {
            var activity = _testActivitySource.StartActivity(ActivityKind.Internal);
            AddNewEventWithStoringOfCurrentActivityId(receivedEvents);
            activity?.Complete();
        }

        var batch = new ReceivedEventsBatch(receivedEvents);

        // Act
        await batch.IterateWithTracingRestoreAsync(NameSuffixForIterationActivities, e => Task.CompletedTask);

        // Assert
        Assert.Equal(expectedNumberOfRestoredActivities, _tracing.Count(a => a.DisplayName == NameSuffixForIterationActivities));
    }

    [Fact]
    public async Task given_batch_with_event_with_activity_id_when_IterateWithTracingRestoreAsync_and_exception_is_thrown_during_iteration_then_activity_is_started_and_has_exception_recorded()
    {
        // Arrange

        var receivedEvents = new List<ReceivedEvent>();

        var activity = _testActivitySource.StartActivity(ActivityKind.Internal);
        AddNewEventWithStoringOfCurrentActivityId(receivedEvents);
        activity?.Complete();

        var batch = new ReceivedEventsBatch(receivedEvents);

        // Act
        try
        {
            await batch.IterateWithTracingRestoreAsync(NameSuffixForIterationActivities, e => throw new InvalidOperationException("test exception 123"));
        }
        catch
        {
            // ignored
        }

        // Assert
        var startedActivity = Assert.Single(_tracing.Where(a => a.DisplayName == NameSuffixForIterationActivities));
        Assert.Equal(ActivityStatusCode.Error, startedActivity.Status);
        Assert.Equal("test exception 123", startedActivity.StatusDescription);
    }

    [Fact]
    public async Task given_batch_with_event_added_without_activity_context_when_IterateWithTracingRestoreAsync_then_activity_is_not_started()
    {
        // Arrange

        var receivedEvents = new List<ReceivedEvent>();

        receivedEvents.Add(new ReceivedEvent(new { }, new EventInfo(StreamId, Guid.NewGuid(), 1, EventType, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, new Dictionary<string, string>())));

        var batch = new ReceivedEventsBatch(receivedEvents);

        // Act
        await batch.IterateWithTracingRestoreAsync(NameSuffixForIterationActivities, e => Task.CompletedTask);

        // Assert
        Assert.Empty(_tracing);
    }

    private static void AddNewEventWithStoringOfCurrentActivityId(ICollection<ReceivedEvent> events)
    {
        var eventNumber = events.Count + 1;
        var customProperties = new Dictionary<string, string>();
        customProperties.StoreCurrentActivityId();
        events.Add(new ReceivedEvent(new { }, new EventInfo(StreamId, Guid.NewGuid(), eventNumber, EventType, Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, customProperties)));
    }
}
