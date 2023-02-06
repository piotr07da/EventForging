using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace EventForging.CosmosDb;

internal sealed class EventForgingCosmosDbConfiguration : IEventForgingCosmosDbConfiguration
{
    private readonly Dictionary<Type, AggregateLocationConfiguration> _aggregateLocations = new();

    public string? ConnectionString { get; set; }
    public IReadOnlyDictionary<Type, AggregateLocationConfiguration> AggregateLocations => _aggregateLocations;
    public bool IgnoreServerCertificateValidation { get; set; }

    public void AddAggregateLocation(string databaseName, string containerName, params Type[] aggregateTypes)
    {
        var locationConfiguration = new AggregateLocationConfiguration(databaseName, containerName);

        var eventForgedType = typeof(IEventForged);

        foreach (var aggregateType in aggregateTypes)
        {
            if (!eventForgedType.IsAssignableFrom(aggregateType))
            {
                throw new EventForgingConfigurationException($"Given aggregate type {aggregateType.FullName} does not implement {eventForgedType.FullName} interface.");
            }

            if (_aggregateLocations.TryGetValue(aggregateType, out var location))
            {
                throw new EventForgingConfigurationException($"Cannot add location for aggregate of type {aggregateType.FullName}. Following location has already been registered: [{location.DatabaseName}, {location.ContainerName}].");
            }

            _aggregateLocations.Add(aggregateType, locationConfiguration);
        }
    }

    public void AddAggregatesLocations(string databaseName, string containerName, Assembly aggregatesAssembly, Func<Type, bool>? aggregateTypeFilter = default)
    {
        var locationConfiguration = new AggregateLocationConfiguration(databaseName, containerName);

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
}
