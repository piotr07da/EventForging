using EventStore.Client;

namespace EventForging.EventStore.EventHandling;

public sealed record SubscriptionConfiguration(string SubscriptionName, string StreamId, string GroupName, PersistentSubscriptionNakEventAction EventHandlingExceptionNakAction = PersistentSubscriptionNakEventAction.Retry, ulong? StartFrom = 0);
