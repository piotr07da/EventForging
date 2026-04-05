// ReSharper disable InconsistentNaming

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.CosmosDb.Tests;

public sealed class EventHandling_configuration_tests
{
    [Fact]
    public void adding_two_subscriptions_with_the_same_changeFeed_name_shall_throw_exception()
    {
        var ex = Assert.Throws<EventForgingConfigurationException>(() =>
        {
            var services = new ServiceCollection();
            services.AddEventForging(r =>
            {
                r.UseCosmosDb(cc =>
                {
                    cc.ConnectionString = ConnectionInfo.ConnectionString;
                    cc.AddAggregateLocations("Db1", "Events1", typeof(SubscriptionConfigurationTestAggregate1));
                    cc.AddAggregateLocations("Db2", "Events2", typeof(SubscriptionConfigurationTestAggregate2));
                    cc.AddEventsSubscription("Sub1", "Db1", "Events1", "ChFeed", null);
                    cc.AddEventsSubscription("Sub2", "Db2", "Events2", "ChFeed", null);
                });
            });
        });
        Assert.True(ex.Message == "Cannot add two event subscriptions with the same change feed name. Duplicated name is 'ChFeed'.");
    }

    [Fact]
    public void adding_subscription_with_poll_interval_shall_store_it()
    {
        var services = new ServiceCollection();
        services.AddEventForging(r =>
        {
            r.UseCosmosDb(cc =>
            {
                cc.ConnectionString = ConnectionInfo.ConnectionString;
                cc.AddAggregateLocations("Db1", "Events1", typeof(SubscriptionConfigurationTestAggregate1));
                cc.AddEventsSubscription("Sub1", "Db1", "Events1", "ChFeed", null, TimeSpan.FromSeconds(2));
            });
        });

        using var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetRequiredService<ICosmosDbEventForgingConfiguration>();

        var subscription = Assert.Single(configuration.Subscriptions);
        Assert.Equal(TimeSpan.FromSeconds(2), subscription.PollInterval);
    }

    [Fact]
    public void adding_events_container_for_aggregate_shall_register_location_and_subscription()
    {
        var services = new ServiceCollection();
        services.AddEventForging(r =>
        {
            r.UseCosmosDb(cc =>
            {
                cc.ConnectionString = ConnectionInfo.ConnectionString;
                cc.AddEventsContainerForAggregate("Db1", "Events1", typeof(SubscriptionConfigurationTestAggregate1), s =>
                {
                    s.AddEventsSubscription("Sub1", "ChFeed1", null, TimeSpan.FromSeconds(2));
                });
            });
        });

        using var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetRequiredService<ICosmosDbEventForgingConfiguration>();

        Assert.True(configuration.AggregateLocations.TryGetValue(typeof(SubscriptionConfigurationTestAggregate1), out var location));
        Assert.True(location == new AggregateLocationConfiguration("Db1", "Events1"));

        var subscription = Assert.Single(configuration.Subscriptions);
        Assert.True(subscription == new EventForging.CosmosDb.EventHandling.SubscriptionConfiguration("Sub1", "Db1", "Events1", "ChFeed1", null, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void adding_events_container_for_aggregates_shall_register_filtered_locations_and_subscription()
    {
        var services = new ServiceCollection();
        services.AddEventForging(r =>
        {
            r.UseCosmosDb(cc =>
            {
                cc.ConnectionString = ConnectionInfo.ConnectionString;
                cc.AddEventsContainerForAggregates(
                    "Db1",
                    "Events1",
                    typeof(SubscriptionConfigurationTestAggregate1).Assembly,
                    t => t == typeof(SubscriptionConfigurationTestAggregate1) || t == typeof(SubscriptionConfigurationTestAggregate2),
                    s => s.AddEventsSubscription("Sub1", "ChFeed1", DateTime.UtcNow));
            });
        });

        using var serviceProvider = services.BuildServiceProvider();
        var configuration = serviceProvider.GetRequiredService<ICosmosDbEventForgingConfiguration>();

        Assert.True(configuration.AggregateLocations.TryGetValue(typeof(SubscriptionConfigurationTestAggregate1), out var location1));
        Assert.True(location1 == new AggregateLocationConfiguration("Db1", "Events1"));
        Assert.True(configuration.AggregateLocations.TryGetValue(typeof(SubscriptionConfigurationTestAggregate2), out var location2));
        Assert.True(location2 == new AggregateLocationConfiguration("Db1", "Events1"));
        Assert.False(configuration.AggregateLocations.ContainsKey(typeof(SubscriptionConfigurationTestAggregate3)));

        var subscription = Assert.Single(configuration.Subscriptions);
        Assert.True(subscription.DatabaseName == "Db1");
        Assert.True(subscription.EventsContainerName == "Events1");
        Assert.True(subscription.SubscriptionName == "Sub1");
        Assert.True(subscription.ChangeFeedName == "ChFeed1");
    }

    [Fact]
    public void adding_subscription_without_matching_aggregate_location_shall_throw_exception()
    {
        var ex = Assert.Throws<EventForgingConfigurationException>(() =>
        {
            var services = new ServiceCollection();
            services.AddEventForging(r =>
            {
                r.UseCosmosDb(cc =>
                {
                    cc.ConnectionString = ConnectionInfo.ConnectionString;
                    cc.AddAggregateLocations("Db1", "Events1", typeof(SubscriptionConfigurationTestAggregate1));
                    cc.AddEventsSubscription("Sub1", "Db1", "Events2", "ChFeed", null);
                });
            });
        });

        Assert.True(ex.Message == "Cannot add event subscription 'Sub1' for [Db1, Events2]. Register aggregate location for this database and events container first using ICosmosDbEventForgingConfiguration.AddAggregateLocations.");
    }

    private sealed class SubscriptionConfigurationTestAggregate1 : IEventForged
    {
        public SubscriptionConfigurationTestAggregate1()
        {
            Events = Events.CreateFor(this);
        }

        public Events Events { get; }
    }

    private sealed class SubscriptionConfigurationTestAggregate2 : IEventForged
    {
        public SubscriptionConfigurationTestAggregate2()
        {
            Events = Events.CreateFor(this);
        }

        public Events Events { get; }
    }

    private sealed class SubscriptionConfigurationTestAggregate3 : IEventForged
    {
        public SubscriptionConfigurationTestAggregate3()
        {
            Events = Events.CreateFor(this);
        }

        public Events Events { get; }
    }
}
