// ReSharper disable InconsistentNaming

using System.Reflection;
using EventForging.CosmosDb.DependencyInjection;
using EventForging.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EventForging.CosmosDb.Tests;

public class Order : IEventForged
{
    public Order()
    {
        Events = Events.CreateFor(this);
    }

    public Events Events { get; }

    public Guid Id { get; private set; }

    public static Order Raise(Guid id)
    {
        var order = new Order();
        var events = order.Events;
        events.Apply(new OrderRaisedEvent(id));
        return order;
    }

    private void Apply(OrderRaisedEvent e)
    {
        Id = e.OrderId;
    }
}

public sealed record OrderRaisedEvent(Guid OrderId);

public class tests
{
    [Fact]
    public async Task run()
    {
        var services = new ServiceCollection();
        services.AddEventForging(r =>
        {
            r.UseCosmosDb(cc =>
            {
                cc.IgnoreServerCertificateValidation = true;
                cc.ConnectionString = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
                cc.AddAggregatesLocations("Sales", "Sales-Events", Assembly.GetExecutingAssembly());
            });
        });
        var sp = services.BuildServiceProvider();
        var hs = sp.GetRequiredService<IHostedService>();
        await hs.StartAsync(CancellationToken.None);

        var orderId = Guid.NewGuid();
        var order = Order.Raise(orderId);
        var repository = sp.GetRequiredService<IRepository<Order>>();
        await repository.SaveAsync(orderId, order, ExpectedVersion.Any, Guid.Empty, Guid.Empty, null);

        var order2 = await repository.GetAsync(orderId);
    }
}
