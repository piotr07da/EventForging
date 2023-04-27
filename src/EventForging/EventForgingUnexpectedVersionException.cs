using System.Text;

namespace EventForging;

public class EventForgingUnexpectedVersionException : EventForgingConcurrencyException
{
    public EventForgingUnexpectedVersionException(
        string aggregateId,
        string? streamId,
        ExpectedVersion expectedVersion,
        AggregateVersion retrievedVersion,
        AggregateVersion? actualVersion,
        Exception innerException)
        : this(FormatMessage(aggregateId, streamId, expectedVersion, retrievedVersion, actualVersion), aggregateId, streamId, expectedVersion, retrievedVersion, actualVersion, innerException)
    {
    }

    public EventForgingUnexpectedVersionException(
        string message,
        string aggregateId,
        string? streamId,
        ExpectedVersion expectedVersion,
        AggregateVersion retrievedVersion,
        AggregateVersion? actualVersion,
        Exception innerException)
        : base(message, innerException)
    {
        AggregateId = aggregateId;
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        RetrievedVersion = retrievedVersion;
        ActualVersion = actualVersion;
    }

    public EventForgingUnexpectedVersionException(
        string aggregateId,
        string? streamId,
        ExpectedVersion expectedVersion,
        AggregateVersion retrievedVersion,
        AggregateVersion? actualVersion)
        : this(FormatMessage(aggregateId, streamId, expectedVersion, retrievedVersion, actualVersion), aggregateId, streamId, expectedVersion, retrievedVersion, actualVersion)
    {
    }

    public EventForgingUnexpectedVersionException(
        string message,
        string aggregateId,
        string? streamId,
        ExpectedVersion expectedVersion,
        AggregateVersion retrievedVersion,
        AggregateVersion? actualVersion)
        : base(message)
    {
        AggregateId = aggregateId;
        StreamId = streamId;
        ExpectedVersion = expectedVersion;
        RetrievedVersion = retrievedVersion;
        ActualVersion = actualVersion;
    }

    public string AggregateId { get; }
    public string? StreamId { get; }
    public ExpectedVersion ExpectedVersion { get; }
    public AggregateVersion RetrievedVersion { get; }
    public AggregateVersion? ActualVersion { get; }

    private static string FormatMessage(
        string aggregateId,
        string? streamId,
        ExpectedVersion expectedVersion,
        AggregateVersion retrievedVersion,
        AggregateVersion? actualVersion)
    {
        var messageBuilder = new StringBuilder();
        messageBuilder.AppendLine($"Unexpected version while saving an aggregate with id '{aggregateId}' to the database.");
        if (!string.IsNullOrEmpty(streamId))
        {
            messageBuilder.AppendLine($"The event stream id is '{streamId}'.");
        }

        messageBuilder.AppendLine($"Expected version was '{expectedVersion}'.");
        messageBuilder.AppendLine($"Retrieved version was '{retrievedVersion}'.");
        if (actualVersion.HasValue)
        {
            messageBuilder.AppendLine($"Actual version stored in the database is '{actualVersion.Value}'.");
        }

        return messageBuilder.ToString();
    }
}
