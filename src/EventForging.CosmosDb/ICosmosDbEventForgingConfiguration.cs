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

    IStreamNameFactory StreamNameFactory { get; }

    void AddAggregateLocations(string databaseName, string eventsContainerName, params Type[] aggregateTypes);
    void AddAggregateLocations(string databaseName, string eventsContainerName, Assembly aggregatesAssembly, Func<Type, bool>? aggregateTypeFilter = default);

    void AddEventsSubscription(string subscriptionName, string databaseName, string eventsContainerName, string changeFeedName, DateTime? startTime);

    /// <summary>
    ///     Allows to set custom stream name factory.
    /// </summary>
    /// <param name="streamNameFactory">The custom stream name factory.</param>
    void SetStreamNameFactory(IStreamNameFactory streamNameFactory);

    /// <summary>
    ///     Allows to set custom stream name factory.
    /// </summary>
    /// <param name="streamNameFactory">
    ///     The custom stream name factory.<br />
    ///     The first argument is an aggregate type.<br />
    ///     The second argument is an aggregate identifier.
    /// </param>
    void SetStreamNameFactory(Func<Type, string, string> streamNameFactory);
}
