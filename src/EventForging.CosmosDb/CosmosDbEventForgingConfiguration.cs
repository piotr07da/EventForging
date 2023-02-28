using System.Reflection;
using EventForging.CosmosDb.EventHandling;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbEventForgingConfiguration : ICosmosDbEventForgingConfiguration
{
    private readonly Dictionary<Type, AggregateLocationConfiguration> _aggregateLocations = new();
    private readonly List<SubscriptionConfiguration> _subscriptions = new();

    public string? ConnectionString { get; set; }
    public IReadOnlyDictionary<Type, AggregateLocationConfiguration> AggregateLocations => _aggregateLocations;
    public bool IgnoreServerCertificateValidation { get; set; }
    public IReadOnlyList<SubscriptionConfiguration> Subscriptions => _subscriptions;

    public void AddAggregateLocations(string databaseName, string eventsContainerName, params Type[] aggregateTypes)
    {
        var locationConfiguration = new AggregateLocationConfiguration(databaseName, eventsContainerName);

        var eventForgedType = typeof(IEventForged);

        foreach (var aggregateType in aggregateTypes)
        {
            if (!eventForgedType.IsAssignableFrom(aggregateType))
            {
                throw new EventForgingConfigurationException($"Given aggregate type {aggregateType.FullName} does not implement {eventForgedType.FullName} interface.");
            }

            if (_aggregateLocations.TryGetValue(aggregateType, out var location))
            {
                throw new EventForgingConfigurationException($"Cannot add location for aggregate of type {aggregateType.FullName}. Following location has already been registered: [{location.DatabaseName}, {location.EventsContainerName}].");
            }

            _aggregateLocations.Add(aggregateType, locationConfiguration);
        }
    }

    public void AddAggregateLocations(string databaseName, string eventsContainerName, Assembly aggregatesAssembly, Func<Type, bool>? aggregateTypeFilter = default)
    {
        var locationConfiguration = new AggregateLocationConfiguration(databaseName, eventsContainerName);

        var eventForgedType = typeof(IEventForged);
        aggregateTypeFilter ??= t => true;
        var aggregateTypes = aggregatesAssembly.GetTypes().Where(t => eventForgedType.IsAssignableFrom(t) && aggregateTypeFilter(t)).ToArray();
        foreach (var aggregateType in aggregateTypes)
        {
            if (!_aggregateLocations.ContainsKey(aggregateType))
            {
                _aggregateLocations.Add(aggregateType, locationConfiguration);
            }
        }
    }

    public void AddEventsSubscription(string subscriptionName, string databaseName, string eventsContainerName, string changeFeedName, DateTime? startTime)
    {
        _subscriptions.Add(new SubscriptionConfiguration(subscriptionName, databaseName, eventsContainerName, changeFeedName, startTime));
    }
}
