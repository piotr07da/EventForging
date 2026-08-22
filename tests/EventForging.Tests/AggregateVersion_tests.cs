// ReSharper disable InconsistentNaming

using Xunit;

namespace EventForging.Tests;

public class AggregateVersion_tests
{
    [Fact]
    public void next_for_not_existing_aggregate_returns_first_version()
    {
        var nextVersion = AggregateVersion.NotExistingAggregate.Next();

        Assert.Equal(0, nextVersion.Value);
    }

    [Fact]
    public void next_for_existing_aggregate_returns_following_version()
    {
        var nextVersion = AggregateVersion.FromValue(42).Next();

        Assert.Equal(43, nextVersion.Value);
    }
}
