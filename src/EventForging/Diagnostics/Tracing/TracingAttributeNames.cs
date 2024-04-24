namespace EventForging.Diagnostics.Tracing;

public static class TracingActivityNames
{
    public const string RepositoryGet = "Repository Get";
    public const string RepositorySave = "Repository Save";
}

public static class TracingAttributeNames
{
    public const string AggregateId = "aggregate.id";
    public const string AggregateType = "aggregate.type";
    public const string AggregateVersion = "aggregate.version";
    public const string AggregateEventsCount = "aggregate.events_count";
    public const string ExpectedVersion = "expected_version";
    public const string ConversationId = "conversation_id";
    public const string InitiatorId = "initiator_id";
    public const string NullExpected = "null_expected";
    public const string CustomPropertyPrefix = "custom_property.";

    public static class ExceptionEvent
    {
        public const string Name = "exception";
        public const string ExceptionEscaped = "exception.escaped";
        public const string ExceptionType = "exception.type";
        public const string ExceptionMessage = "exception.message";
        public const string ExceptionStackTrace = "exception.stacktrace";
    }
}
