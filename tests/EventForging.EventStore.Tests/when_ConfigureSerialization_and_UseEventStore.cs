// ReSharper disable InconsistentNaming

using System.Reflection;
using System.Text;
using System.Text.Json;
using EventForging.DependencyInjection;
using EventForging.EventStore.DependencyInjection;
using EventForging.Serialization;
using EventStore.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventForging.EventStore.Tests;

public class when_ConfigureSerialization_and_UseEventStore : IAsyncLifetime
{
    private const string ConnectionString = "esdb://localhost:2113?tls=false";
    private static EventStoreClient _client;

    private readonly IServiceProvider _serviceProvider;

    public when_ConfigureSerialization_and_UseEventStore()
    {
        var services = new ServiceCollection();
        services.AddEventForging(r =>
        {
            r.ConfigureSerialization(sc =>
            {
                sc.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(Assembly.GetExecutingAssembly()));
            });
            r.UseEventStore(cc =>
            {
                cc.ConnectionString = ConnectionString;
            });
        });
        _serviceProvider = services.BuildServiceProvider();

        _client = new EventStoreClient(EventStoreClientSettings.Create(ConnectionString));
    }

    public async Task InitializeAsync()
    {
        var hs = _serviceProvider.GetRequiredService<IHostedService>();
        await hs.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await Task.CompletedTask;
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
    public async Task and_new_aggregate_saved_many_times_in_sequence_for_the_same_initiatorId_the_read_only_one_event_saved()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();

        var orderToSave = Order.Raise(orderId);
        for (var i = 0; i < 100; ++i)
        {
            await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, initiatorId, new Dictionary<string, string> { { "save", i.ToString() }, });
        }

        var mds = new List<EventMetadata>();
        await foreach (var re in _client.ReadStreamAsync(Direction.Forwards, $"Order-{orderId}", StreamPosition.Start))
        {
            var mdStr = Encoding.UTF8.GetString(re.Event.Metadata.ToArray());
            var md = JsonSerializer.Deserialize<EventMetadata>(mdStr)!;
            mds.Add(md);
        }

        Assert.Single(mds);
        Assert.Equal("0", mds[0].CustomProperties["save"]);
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
}
