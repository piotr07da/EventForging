using Xunit;

namespace EventForging.Tests;

public class Aggregate_tests
{
    [Fact]
    public void given_an_aggregate_when_execute_operations_then_aggregate_state_changed()
    {
        var number = 26;
        var text = "EventForging";
        var timestamp = DateTime.UtcNow;

        var a = new TestAggregate();
        a.ChangeNumber(number);
        a.ChangeText(text);
        a.ChangeTimestamp(timestamp);

        Assert.Equal(number, a.Number);
        Assert.Equal(text, a.Text);
        Assert.Equal(timestamp, a.Timestamp);
    }
}
