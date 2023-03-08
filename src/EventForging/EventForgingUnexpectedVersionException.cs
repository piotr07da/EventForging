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
        messageBuilder.AppendLine($"Unexpected version while saving an aggregate with id '{aggregateId}' to the database.");
        if (!string.IsNullOrEmpty(streamId))
        {
            messageBuilder.AppendLine($"The event stream id is '{streamId}'.");
        }

        if (readVersion.AggregateDoesNotExist)
        {
            // TODO
            messageBuilder.AppendLine($"Aggregate was created so its version was '{readVersion}'.");
        }
        else
        {
            // TODO
            messageBuilder.AppendLine($"Aggregate version while it was read from the database was '{readVersion}'.");
        }

        if (actualVersion.HasValue)
        {
            messageBuilder.AppendLine($"Actual version stored in the database is '{actualVersion.Value}'.");
        }

        messageBuilder.AppendLine($"Expected version passed in an argument was '{expectedVersion}'.");
        if (expectedVersion.IsAny)
            messageBuilder.AppendLine($"Because expected version passed in an argument was '{expectedVersion}', the version of an aggregate stored in the database was the same as its read version which was '{readVersion}'.");

        return messageBuilder.ToString();
    }
}
