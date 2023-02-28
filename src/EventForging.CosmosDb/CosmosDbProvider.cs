using EventForging.CosmosDb.Serialization;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb;

internal sealed class CosmosDbProvider : ICosmosDbProvider
{
    private readonly IDictionary<string, Database> _databases = new Dictionary<string, Database>();
    private readonly IDictionary<string, Container> _containers = new Dictionary<string, Container>();
    private readonly IDictionary<Type, Container> _aggregateContainers = new Dictionary<Type, Container>();
    private readonly IDictionary<string, Container> _leaseContainers = new Dictionary<string, Container>();
    private readonly ICosmosDbEventForgingConfiguration _configuration;
    private readonly IEventSerializer _eventSerializer;
    private readonly IJsonSerializerOptionsProvider _serializerOptionsProvider;
    private CosmosClient? _client;

    public CosmosDbProvider(ICosmosDbEventForgingConfiguration configuration, IEventSerializer eventSerializer, IJsonSerializerOptionsProvider serializerOptionsProvider)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _eventSerializer = eventSerializer ?? throw new ArgumentNullException(nameof(eventSerializer));
        _serializerOptionsProvider = serializerOptionsProvider ?? throw new ArgumentNullException(nameof(serializerOptionsProvider));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
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
            var database = await InitializeDatabaseAsync(locationConfiguration.DatabaseName, cancellationToken);
            var container = await InitializeContainerAsync(database, locationConfiguration.EventsContainerName, "/streamId", cancellationToken);
            _aggregateContainers.Add(aggregateType, container);
        }

        foreach (var subscription in _configuration.Subscriptions)
        {
            if (!_leaseContainers.ContainsKey(subscription.DatabaseName))
            {
                var database = await InitializeDatabaseAsync(subscription.DatabaseName, cancellationToken);
                var container = await InitializeContainerAsync(database, "Lease", "/id", cancellationToken);
                _leaseContainers.Add(subscription.DatabaseName, container);
            }
        }
    }

    public async Task DisposeAsync(CancellationToken cancellationToken = default)
    {
        _client?.Dispose();
        _databases.Clear();
        _containers.Clear();
        _aggregateContainers.Clear();
        await Task.CompletedTask;
    }

    public Container GetAggregateContainer<TAggregate>()
    {
        var at = typeof(TAggregate);

        if (!_aggregateContainers.TryGetValue(at, out var container))
        {
            throw new EventForgingException($"Cannot find cosmos db container for aggregate of type {at.FullName}. Use {nameof(ICosmosDbEventForgingConfiguration)}.{nameof(ICosmosDbEventForgingConfiguration.AddAggregateLocations)} method to register database and container names for this aggregate type.");
        }

        return container;
    }

    public Container GetLeaseContainer(string databaseName)
    {
        if (!_leaseContainers.TryGetValue(databaseName, out var container))
        {
            throw new EventForgingException($"Cannot find cosmos db 'Lease' container in '{databaseName}' database.");
        }

        return container;
    }

    public Container GetContainer(string databaseName, string containerName)
    {
        var containerKey = ContainerCacheKey(databaseName, containerName);
        if (!_containers.TryGetValue(containerKey, out var container))
        {
            throw new EventForgingException($"Cannot find cosmos db '{containerName}' container in '{databaseName}' database.");
        }

        return container;
    }

    private async Task<Database> InitializeDatabaseAsync(string databaseName, CancellationToken cancellationToken)
    {
        if (!_databases.TryGetValue(databaseName, out var database))
        {
            database = await _client!.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken);
            _databases.Add(databaseName, database);
        }

        return database;
    }

    private async Task<Container> InitializeContainerAsync(Database database, string containerName, string partitionKeyPath, CancellationToken cancellationToken)
    {
        var containerKey = ContainerCacheKey(database.Id, containerName);

        if (!_containers.TryGetValue(containerKey, out var container))
        {
            container = await database.CreateContainerIfNotExistsAsync(containerName, partitionKeyPath, cancellationToken: cancellationToken);
            _containers.Add(containerKey, container);
        }

        return container;
    }

    private static string ContainerCacheKey(string databaseName, string containerName) => $"~~{databaseName}~~{containerName}~~";
}
