using System.Text;

namespace EventForging;

public class EventForgingUnexpectedVersionException : EventForgingConcurrencyException
{
    public EventForgingUnexpectedVersionException(string aggregateId, string? streamId, ExpectedVersion expectedVersion, AggregateVersion lastReadVersion, AggregateVersion? actualVersion, Exception innerException)
        : this(FormatMessage(aggregateId, streamId, expectedVersion, lastReadVersion, actualVersion), aggregateId, streamId, expectedVersion, lastReadVersion, actualVersion, innerException)
    {
    }

    public EventForgingUnexpectedVersionException(string message, string aggregateId, string? streamId, ExpectedVersion expectedVersion, AggregateVersion lastReadVersion, AggregateVersion? actualVersion, Exception innerException)
        : base(message, innerException)
    {
        AggregateId = aggregateId;
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        LastReadVersion = lastReadVersion;
        ActualVersion = actualVersion;
    }

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
        messageBuilder.AppendLine($"Unexpected version while writing an aggregate with id {aggregateId}.");
        if (!string.IsNullOrEmpty(streamId))
        {
            messageBuilder.AppendLine($"Stream id is {streamId}.");
        }

        messageBuilder.AppendLine($"Aggregate version while it was read from the repository (or while it was newly created) was {readVersion}.");
        if (actualVersion.HasValue)
        {
            messageBuilder.AppendLine($"Actual version is {actualVersion.Value}.");
        }

        messageBuilder.AppendLine($"Expected version is {expectedVersion}.");

        return messageBuilder.ToString();
    }
}
