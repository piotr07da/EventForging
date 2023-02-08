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
            r.Configuration.Serialization.SetEventTypeNameMappers(new DefaultEventTypeNameMapper(Assembly.GetExecutingAssembly()));
            r.UseEventStore(cc =>
            {
                cc.Address = ConnectionString;
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
        await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, Guid.NewGuid());
        var orderAfterSave = await repository.GetAsync(orderId);

        Assert.Equal(orderId, orderAfterSave.Id);
    }

    [Fact]
    public async Task and_new_aggregate_saved_many_times_in_sequence_for_the_same_initiatorId_then_only_one_event_saved()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();

        var orderToSave = Order.Raise(orderId);
        for (var i = 0; i < 100; ++i)
        {
            await repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, initiatorId, new Dictionary<string, string> { { "save", i.ToString() }, });
        }

        var mds = await LoadEventsAsync(orderId);

        Assert.Single(mds);
        Assert.Equal("0", mds[0].Metadata.CustomProperties["save"]);
    }

    [Fact]
    public async Task and_new_aggregate_saved_many_times_in_parallel_for_the_same_initiatorId_then_only_one_event_saved()
    {
        var repository = ResolveRepository();
        var orderId = Guid.NewGuid();
        var initiatorId = Guid.NewGuid();

        var orderToSave = Order.Raise(orderId);

        var saveTasks = new List<Task>();
        var saveIds = new List<string>();
        for (var i = 0; i < 2; ++i)
        {
            var saveId = Guid.NewGuid().ToString();
            saveIds.Add(saveId);
            var saveTask = repository.SaveAsync(orderId, orderToSave, ExpectedVersion.Any, Guid.Empty, initiatorId, new Dictionary<string, string> { { "save", saveId }, });
            saveTasks.Add(saveTask);
        }

        await Task.WhenAll(saveTasks);

        var mds = await LoadEventsAsync(orderId);

        Assert.Single(mds);
        Assert.Contains(mds[0].Metadata.CustomProperties["save"], saveIds);
    }

    private async Task<LoadedEvent[]> LoadEventsAsync(Guid orderId)
    {
        var les = new List<LoadedEvent>();
        await foreach (var re in _client.ReadStreamAsync(Direction.Forwards, $"Order-{orderId}", StreamPosition.Start))
        {
            var mdStr = Encoding.UTF8.GetString(re.Event.Metadata.ToArray());
            var md = JsonSerializer.Deserialize<EventMetadata>(mdStr)!;
            les.Add(new LoadedEvent(md));
        }

        return les.ToArray();
    }

    private IRepository<Order> ResolveRepository()
    {
        return _serviceProvider.GetRequiredService<IRepository<Order>>();
    }

    private class LoadedEvent
    {
        public LoadedEvent(EventMetadata metadata)
        {
            Metadata = metadata;
        }

        public EventMetadata Metadata { get; }
    }
}
