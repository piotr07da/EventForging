using System.Text;

namespace EventForging;

public class EventForgingUnexpectedVersionException : EventForgingConcurrencyException
{
    public EventForgingUnexpectedVersionException(ExpectedVersion expectedVersion, AggregateVersion lastReadVersion, AggregateVersion? actualVersion)
        : this(FormatMessage(expectedVersion, lastReadVersion, actualVersion), expectedVersion, lastReadVersion, actualVersion)
    {
    }

    public EventForgingUnexpectedVersionException(string message, ExpectedVersion expectedVersion, AggregateVersion lastReadVersion, AggregateVersion? actualVersion)
        : base(message)
    {
        ExpectedVersion = expectedVersion;
        LastReadVersion = lastReadVersion;
        ActualVersion = actualVersion;
    }

    public ExpectedVersion ExpectedVersion { get; set; }
    public AggregateVersion LastReadVersion { get; set; }
    public AggregateVersion? ActualVersion { get; set; }

    private static string FormatMessage(ExpectedVersion expectedVersion, AggregateVersion readVersion, AggregateVersion? actualVersion)
    {
        var messageBuilder = new StringBuilder();
        messageBuilder.Append($"Unexpected version. Expected version is {expectedVersion}. Aggregate version while it was read from the repository was {readVersion}.");
        if (actualVersion.HasValue)
        {
            messageBuilder.Append($" Actual version is {actualVersion.Value}");
        }

        return messageBuilder.ToString();
    }
}
