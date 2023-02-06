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
        var database = _cosmosClient.GetDatabase(DatabaseName);
        await database.ReadAsync();
    }

    [Fact]
    public async Task and_aggregate_saved_then_read_aggregate_is_rehydrated()
    {
        var orderId = Guid.NewGuid();
        var orderToSave = Order.Raise(orderId);
        var repository = _serviceProvider.GetRequiredService<IRepository<Order>>();
        await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, Guid.Empty);
        var orderFromRepository = await repository.GetAsync(orderId);
        Assert.Equal(orderId, orderFromRepository.Id);
    }

    private CosmosClient CreateCosmosClient()
    {
        return new CosmosClient(ConnectionString, new CosmosClientOptions
        {
            HttpClientFactory = () => new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true, }),
            ConnectionMode = ConnectionMode.Gateway,
        });
    }
}
