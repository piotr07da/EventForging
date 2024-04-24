namespace EventForging.CosmosDb.Diagnostics.Tracing;

public class CosmosDbTracingAttributeNames
{
    public const string EventDatabaseStreamId = "eventdb.stream_id";
    public const string EventDatabaseTryCount = "eventdb.write.try_count";
    public const string DatabaseSystem = "db.system";
    public const string CosmosDbContainer = "db.cosmosdb.container";
    public const string CosmosDbRequestCharge = "db.cosmosdb.request_charge";
}
