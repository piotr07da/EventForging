// ReSharper disable InconsistentNaming

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
}
