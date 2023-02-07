using System.Text;

namespace EventForging;

public class EventForgingUnexpectedVersionException : EventForgingConcurrencyException
{
    public EventForgingUnexpectedVersionException(string aggregateId, string? streamId, ExpectedVersion expectedVersion, AggregateVersion lastReadVersion, AggregateVersion? actualVersion)
        : this(FormatMessage(aggregateId, streamId, expectedVersion, lastReadVersion, actualVersion), aggregateId, streamId, expectedVersion, lastReadVersion, actualVersion)
    {
    }

    public EventForgingUnexpectedVersionException(string message, string aggregateId, string? streamId, ExpectedVersion expectedVersion, AggregateVersion lastReadVersion, AggregateVersion? actualVersion)
        : base(message)
    {
        AggregateId = aggregateId;
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        LastReadVersion = lastReadVersion;
        ActualVersion = actualVersion;
    }

    public string AggregateId { get; }
    public string? StreamId { get; }
    public ExpectedVersion ExpectedVersion { get; }
    public AggregateVersion LastReadVersion { get; }
    public AggregateVersion? ActualVersion { get; }

    private static string FormatMessage(string aggregateId, string? streamId, ExpectedVersion expectedVersion, AggregateVersion readVersion, AggregateVersion? actualVersion)
    {
        var messageBuilder = new StringBuilder();
        messageBuilder.Append($"Unexpected version while writing an aggregate with id {aggregateId}.");
        if (!string.IsNullOrEmpty(streamId))
        {
            messageBuilder.Append($" Stream id is {streamId}.");
        }

        messageBuilder.Append($" Expected version is {expectedVersion}.");
        messageBuilder.Append($" Aggregate version while it was read from the repository was {readVersion}.");
        if (actualVersion.HasValue)
        {
            messageBuilder.Append($" Actual version is {actualVersion.Value}.");
        }

        return messageBuilder.ToString();
    }
}
