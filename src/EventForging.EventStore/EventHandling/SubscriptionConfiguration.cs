namespace EventForging.EventStore.EventHandling;

public sealed record SubscriptionConfiguration(string SubscriptionName, string StreamName, string GroupName, ulong? StartFrom = 0);
