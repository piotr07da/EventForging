namespace EventForging.CosmosDb.EventHandling;

public sealed record SubscriptionConfiguration(string SubscriptionName, string DatabaseName, string EventsContainerName, string ChangeFeedName, DateTime? StartTime);
