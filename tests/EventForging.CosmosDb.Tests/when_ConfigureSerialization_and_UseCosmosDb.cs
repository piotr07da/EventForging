// ReSharper disable InconsistentNaming

using System.Reflection;
using EventForging.CosmosDb.DependencyInjection;
using EventForging.DependencyInjection;
using EventForging.Serialization;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventForging.CosmosDb.Tests;

public class when_ConfigureSerialization_and_UseCosmosDb : IAsyncLifetime
{
    private const string ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
    private const string DatabaseName = "Sales";
    private const string ContainerName = "Sales-Events";

    private readonly IServiceProvider _serviceProvider;
    private readonly CosmosClient _cosmosClient;

    public when_ConfigureSerialization_and_UseCosmosDb()
    {
        var services = new ServiceCollection();
        services.AddEventForging(r =>
        {
            r.ConfigureSerialization(sc =>
            {
                sc.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(Assembly.GetExecutingAssembly()));
            });
            r.UseCosmosDb(cc =>
            {
                cc.IgnoreServerCertificateValidation = true;
                cc.ConnectionString = ConnectionString;
                cc.AddAggregatesLocations(DatabaseName, ContainerName, Assembly.GetExecutingAssembly());
            });
        });
        _serviceProvider = services.BuildServiceProvider();

        _cosmosClient = CreateCosmosClient();
    }

    public async Task InitializeAsync()
    {
        var hs = _serviceProvider.GetRequiredService<IHostedService>();
        await hs.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        var hs = _serviceProvider.GetRequiredService<IHostedService>();
        await hs.StopAsync(CancellationToken.None);

        var db = _cosmosClient.GetDatabase(DatabaseName);
        await db.DeleteAsync();
    }

    [Fact]
    public async Task then_database_created()
    {
        var database = GetDatabase();
        await database.ReadAsync();
    }

    [Fact]
    public async Task and_new_aggregate_saved_then_read_aggregate_is_rehydrated()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();

        var orderToSave = Order.Raise(orderId);
        await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, Guid.Empty);
        var orderAfterSave = await repository.GetAsync(orderId);

        Assert.Equal(orderId, orderAfterSave.Id);
    }

    [Fact]
    public async Task and_existing_aggregate_saved_then_read_aggregate_is_rehydrated()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();

        var existingOrder = await prepare_existing_aggregate(orderId);
        existingOrder.Complete();
        await repository.SaveAsync(orderId, existingOrder, ExpectedVersion.Any, Guid.Empty, Guid.Empty);
        var orderAfterSave = await repository.GetAsync(orderId);

        Assert.Equal(orderId, orderAfterSave.Id);
        Assert.True(orderAfterSave.Completed);
    }

    [Fact]
    public async Task and_new_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_are_written_to_the_database_only_for_the_first_saving()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();

        var orderToSave = Order.Raise(orderId);
        var initiatorId = Guid.NewGuid();
        await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, initiatorId, new Dictionary<string, string> { { "save", "first" }, });
        await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, initiatorId, new Dictionary<string, string> { { "save", "second" }, });

        var documents = await GetEventDocumentsAsync();
        Assert.Single(documents);
        var document = documents[0];
        Assert.Equal("first", document.Metadata!.CustomProperties["save"]);
    }

    [Fact]
    public async Task and_existing_aggregate_saved_twice_with_the_same_initiator_id_then_its_events_are_written_to_the_database_only_for_the_first_saving()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();

        var existingOrder = await prepare_existing_aggregate(orderId);
        existingOrder.Complete();
        var initiatorId = Guid.NewGuid();
        await repository.SaveAsync(orderId, existingOrder, ExpectedVersion.Any, Guid.Empty, initiatorId, new Dictionary<string, string> { { "save", "first" }, });
        await repository.SaveAsync(orderId, existingOrder, ExpectedVersion.Any, Guid.Empty, initiatorId, new Dictionary<string, string> { { "save", "second" }, });

        var documents = await GetEventDocumentsAsync();
        Assert.Equal(2, documents.Length);
        var document = documents[1];
        Assert.Equal("first", document.Metadata!.CustomProperties["save"]);
    }

    [Fact]
    public async Task and_new_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();

        var orderToSave = Order.Raise(orderId);
        var firstSaveInitiatorId = Guid.NewGuid();
        var secondSaveInitiatorId = Guid.NewGuid();
        await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, firstSaveInitiatorId);
        await Assert.ThrowsAsync<EventForgingUnexpectedVersionException>(async () =>
        {
            await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, secondSaveInitiatorId);
        });
    }

    [Fact]
    public async Task and_existing_aggregate_saved_twice_with_different_initiator_ids_then_exception_thrown_during_second_saving()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();

        var existingOrder = await prepare_existing_aggregate(orderId);
        existingOrder.Complete();
        var firstSaveInitiatorId = Guid.NewGuid();
        var secondSaveInitiatorId = Guid.NewGuid();
        await repository.SaveAsync(orderId, existingOrder, ExpectedVersion.Any, Guid.Empty, firstSaveInitiatorId);
        await Assert.ThrowsAsync<EventForgingUnexpectedVersionException>(async () =>
        {
            await repository.SaveAsync(orderId, existingOrder, ExpectedVersion.Any, Guid.Empty, secondSaveInitiatorId);
        });
    }

    private async Task<EventDocument[]> GetEventDocumentsAsync()
    {
        var container = GetContainer();
        var iterator = container.GetItemQueryIterator<EventDocument>("SELECT * FROM e");
        var page = await iterator.ReadNextAsync();
        var documents = page.Where(d => d.DocumentType == "Event").ToArray();
        foreach (var document in documents)
        {
            var data = _serviceProvider.GetRequiredService<IEventSerializer>().DeserializeFromString(document.EventType!, document.Data!.ToString()!);
            document.Data = data;
        }

        return documents;
    }

    private async Task<Order> prepare_existing_aggregate(Guid orderId)
    {
        var orderToSave = Order.Raise(orderId);
        var repository = ResolveRepository();
        await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, Guid.Empty);
        var existingOrder = await repository.GetAsync(orderId);
        return existingOrder;
    }

    private IRepository<Order> ResolveRepository()
    {
        return _serviceProvider.GetRequiredService<IRepository<Order>>();
    }

    private Container GetContainer()
    {
        return GetDatabase().GetContainer(ContainerName);
    }

    private Database GetDatabase()
    {
        return _cosmosClient.GetDatabase(DatabaseName);
    }

    private CosmosClient CreateCosmosClient()
    {
        return new CosmosClient(ConnectionString, new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true, }),
            ConnectionMode = ConnectionMode.Gateway,
        });
    }

    internal sealed class EventDocument
    {
        public string? Id { get; set; }

        public string? StreamId { get; set; }

        public string? DocumentType { get; set; }

        public int EventNumber { get; set; }

        public string? EventType { get; set; }

        public object? Data { get; set; }

        public EventMetadata? Metadata { get; set; }
    }
}
