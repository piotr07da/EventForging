// ReSharper disable InconsistentNaming

using Xunit;

namespace EventForging.Tests;

public class EventStreamReadPosition_tests
{
    [Fact]
    public void beginning_does_not_contain_an_after_version()
    {
        var position = EventStreamReadPosition.Beginning;

        var hasAfterVersion = position.TryGetAfterVersion(out _);

        Assert.True(position.IsBeginning);
        Assert.False(hasAfterVersion);
    }

    [Fact]
    public void after_contains_the_specified_version()
    {
        var position = EventStreamReadPosition.After(42);

        var hasAfterVersion = position.TryGetAfterVersion(out var version);

        Assert.False(position.IsBeginning);
        Assert.True(hasAfterVersion);
        Assert.Equal(42, version.Value);
    }

    [Fact]
    public void after_rejects_not_existing_aggregate_version()
    {
        Assert.Throws<ArgumentException>(() => EventStreamReadPosition.After(AggregateVersion.NotExistingAggregate));
    }
}
