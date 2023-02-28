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
                    cc.AddEventsSubscription("Sub1", "Db1", "Events1", "ChFeed", null);
                    cc.AddEventsSubscription("Sub2", "Db2", "Events2", "ChFeed", null);
                });
            });
        });
        Assert.True(ex.Message == "Cannot add two event subscriptions with the same change feed name. Duplicated name is 'ChFeed'.");
    }
}
