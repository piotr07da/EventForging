namespace EventForging.CosmosDb.Diagnostics.Tracing;

public static class CosmosDbTracingAttributeNames
{
    public const string EventDatabaseStreamId = "eventdb.stream_id";
    public const string EventDatabaseWriteAttemptCount = "eventdb.write.attempt_count";
    public const string EventDatabaseWriteIdempotencyCheckResult = "eventdb.write.idempotency_check_result";
    public const string EventDatabaseReadPageCount = "eventdb.read.page_count";
    public const string DatabaseSystem = "db.system";
    public const string DatabaseSystemValue = "cosmosdb";
    public const string CosmosDbContainer = "db.cosmosdb.container";
    public const string CosmosDbStatusCode = "db.cosmosdb.status_code";
    public const string CosmosDbRequestCharge = "db.cosmosdb.request_charge";
    public const string ChangesCount = "changes_count";

    public static class ResultPageReadEvent
    {
        public const string Name = "Result page has been read.";
    }
}
