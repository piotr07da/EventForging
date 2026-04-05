namespace EventForging.CosmosDb;

public interface ICosmosDbEventsContainerConfiguration
{
    void AddEventsSubscription(string subscriptionName, string changeFeedName, DateTime? startTime);
    void AddEventsSubscription(string subscriptionName, string changeFeedName, DateTime? startTime, TimeSpan? pollInterval);
}
