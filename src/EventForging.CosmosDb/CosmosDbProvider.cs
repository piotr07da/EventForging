using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using EventForging.CosmosDb.Serialization;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbProvider : ICosmosDbProvider
{
    private static CosmosClient? _client;
    private static readonly IDictionary<string, Database> _databases = new Dictionary<string, Database>();
    private static readonly IDictionary<string, Container> _containers = new Dictionary<string, Container>();
    private static readonly IDictionary<Type, Container> _aggregateContainers = new Dictionary<Type, Container>();
    private readonly IEventForgingCosmosDbConfiguration _configuration;
    private readonly IEventSerializer _eventSerializer;
    private readonly ISerializerOptionsProvider _serializerOptionsProvider;

    public CosmosDbProvider(IEventForgingCosmosDbConfiguration configuration, IEventSerializer eventSerializer, ISerializerOptionsProvider serializerOptionsProvider)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
    }

    public async Task InitializeAsync()
    {
        var clientOptions = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Direct,
            Serializer = new EventForgingCosmosSerializer(_eventSerializer, _serializerOptionsProvider),
        };

        if (_configuration.IgnoreServerCertificateValidation)
        {
            clientOptions.HttpClientFactory = () => new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true, });
            clientOptions.ConnectionMode = ConnectionMode.Gateway;
        }

        _client = new CosmosClient(_configuration.ConnectionString, clientOptions);

        foreach (var kvp in _configuration.AggregateLocations)
        {
            var aggregateType = kvp.Key;
            var locationConfiguration = kvp.Value;

            if (!_databases.TryGetValue(locationConfiguration.DatabaseName, out var database))
            {
                database = await _client.CreateDatabaseIfNotExistsAsync(locationConfiguration.DatabaseName);
                _databases.Add(locationConfiguration.DatabaseName, database);
            }

            if (!_containers.TryGetValue(locationConfiguration.ContainerName, out var container))
            {
                container = await database.CreateContainerIfNotExistsAsync(locationConfiguration.ContainerName, "/streamId");
                _containers.Add(locationConfiguration.ContainerName, container);
            }

            _aggregateContainers.Add(aggregateType, container);
        }
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        await Task.CompletedTask;
    }

    public Container GetContainer<TAggregate>()
    {
        var at = typeof(TAggregate);

        if (!_aggregateContainers.TryGetValue(at, out var container))
        {
            throw new EventForgingException($"Cannot find cosmos db container for aggregate of type {at.FullName}. Use {nameof(IEventForgingCosmosDbConfiguration)}.{nameof(IEventForgingCosmosDbConfiguration.AddAggregateLocation)} method to register database and container names for this aggregate type.");
        }

        return container;
    }
}
