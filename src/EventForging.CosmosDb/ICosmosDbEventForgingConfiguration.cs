using System.ComponentModel;
using System.Reflection;
using EventForging.CosmosDb.EventHandling;

namespace EventForging.CosmosDb;

public enum EventPackingMode
{
    Disabled,
    UniformDistributionFilling,
    AllEventsInOnePacket,
}

public interface ICosmosDbEventForgingConfiguration
{
    string? ConnectionString { get; set; }
    IReadOnlyDictionary<Type, AggregateLocationConfiguration> AggregateLocations { get; }
    IReadOnlyList<SubscriptionConfiguration> Subscriptions { get; }
    public bool CreateDatabasesAndContainersIfNotExist { get; set; }
    public EventPackingMode EventPacking { get; set; }

    [Obsolete("Use EventPacking instead.")]
    public bool EnableEventPacking { get; set; }

    public int RetryCountForUnexpectedVersionWhenExpectedVersionIsAny { get; set; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    bool IgnoreServerCertificateValidation { get; set; }

    IStreamIdFactory StreamIdFactory { get; }

    void AddAggregateLocations(string databaseName, string eventsContainerName, params Type[] aggregateTypes);
    void AddAggregateLocations(string databaseName, string eventsContainerName, Assembly aggregatesAssembly, Func<Type, bool>? aggregateTypeFilter = default);

    void AddEventsSubscription(string subscriptionName, string databaseName, string eventsContainerName, string changeFeedName, DateTime? startTime);

    /// <summary>Allows to set custom stream id factory.</summary>
    /// <param name="streamIdFactory">The custom stream id factory.</param>
    void SetStreamIdFactory(IStreamIdFactory streamIdFactory);

    /// <summary>Allows to set custom stream id factory.</summary>
    /// <param name="streamIdFactory">The custom stream id factory.<br /> The first argument is an aggregate type.<br /> The second argument is an aggregate identifier.</param>
    void SetStreamIdFactory(Func<Type, string, string> streamIdFactory);
}
