// ReSharper disable InconsistentNaming
// ReSharper disable ConvertToConstant.Local
// ReSharper disable ObjectCreationAsStatement

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventForging.Tests;

public class aggregate_tests
{
    [Fact]
    public void given_an_aggregate_when_execute_operations_then_aggregate_state_changed()
    {
        var number = 26;
        var text = "EventForging";
        var timestamp = DateTime.UtcNow;

        var a = new BreweryAggregate();
        a.BrewNumberBeer(number);
        a.BrewTextBeer(text);
        a.BrewTimestampBeer(timestamp);

        Assert.Equal(number, a.NumberBeerBrewed);
        Assert.Equal(text, a.TextBeerBrewed);
        Assert.Equal(timestamp, a.TimestampBeerBrewed);
    }

    [Fact]
    public void given_an_aggregate_without_apply_methods_when_execute_an_operation_then_exception_thrown()
    {
        var number = 26;

        var a = new BreweryAggregateWithoutApplyMethods();

        Assert.ThrowsAny<Exception>(() =>
        {
            a.BrewNumberBeer(number);
        });
    }

    [Fact]
    public void aggregate_cannot_be_sealed_to_be_able_to_be_created()
    {
        var ex = Assert.Throws<EventForgingException>(() =>
        {
            new SealedClassAggregate();
        });
        Assert.Equal("An aggregate class cannot be sealed.", ex.Message);
    }

    [Fact]
    public async Task aggregate_cannot_be_sealed_to_be_able_to_be_rehydrated()
    {
        var repository = ServiceProviderFactory.Create().GetRequiredService<IRepository<SealedClassAggregate>>();

        var ex = await Assert.ThrowsAsync<EventForgingException>(async () =>
        {
            await repository.GetAsync(Guid.NewGuid());
        });
        Assert.Equal("An aggregate class cannot be sealed.", ex.Message);
    }

    [Fact]
    public void aggregate_has_to_have_private_Apply_method_for_the_event_emitted_by_itself()
    {
        Assert.Throws<EventForgingException>(() =>
        {
            NoApplyMethodAggregate.CreateEmittingAnEvent();
        });
    }
}
