using System.ComponentModel;
using System.Reflection;
using EventForging.CosmosDb.EventHandling;

namespace EventForging.CosmosDb;

public interface ICosmosDbEventForgingConfiguration
{
    string? ConnectionString { get; set; }
    IReadOnlyDictionary<Type, AggregateLocationConfiguration> AggregateLocations { get; }
    IReadOnlyList<SubscriptionConfiguration> Subscriptions { get; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    bool IgnoreServerCertificateValidation { get; set; }

    void AddAggregateLocations(string databaseName, string eventsContainerName, params Type[] aggregateTypes);
    void AddAggregateLocations(string databaseName, string eventsContainerName, Assembly aggregatesAssembly, Func<Type, bool>? aggregateTypeFilter = default);

    void AddEventsSubscription(string subscriptionName, string databaseName, string eventsContainerName, string changeFeedName, DateTime? startTime);
}
