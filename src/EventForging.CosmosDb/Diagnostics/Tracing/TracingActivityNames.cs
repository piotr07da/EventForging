namespace EventForging.CosmosDb.Diagnostics.Tracing;

public static class TracingActivityNames
{
    public const string EventDatabaseRead = "EF CDB CosmosDbEventDatabase Read";
    public const string EventDatabaseWrite = "EF CDB CosmosDbEventDatabase Write";
    public const string EventDatabaseWriteAttempt = "EF CDB CosmosDbEventDatabase Write Attempt";
    public const string EventsSubscriberHandleChanges = "EF CDB EventsSubscriber Handle Changes";
}
