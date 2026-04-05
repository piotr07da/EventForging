namespace EventForging.CosmosDb;

internal sealed class CosmosDbEventsContainerConfiguration : ICosmosDbEventsContainerConfiguration
{
    private readonly CosmosDbEventForgingConfiguration _configuration;
    private readonly string _databaseName;
    private readonly string _eventsContainerName;

    public CosmosDbEventsContainerConfiguration(CosmosDbEventForgingConfiguration configuration, string databaseName, string eventsContainerName)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _databaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        _eventsContainerName = eventsContainerName ?? throw new ArgumentNullException(nameof(eventsContainerName));
    }

    public void AddEventsSubscription(string subscriptionName, string changeFeedName, DateTime? startTime)
    {
        _configuration.AddEventsSubscription(subscriptionName, _databaseName, _eventsContainerName, changeFeedName, startTime);
    }

    public void AddEventsSubscription(string subscriptionName, string changeFeedName, DateTime? startTime, TimeSpan? pollInterval)
    {
        _configuration.AddEventsSubscription(subscriptionName, _databaseName, _eventsContainerName, changeFeedName, startTime, pollInterval);
    }
}
