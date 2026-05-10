namespace EventForging.CosmosDb.Diagnostics.Metrics;

internal static class EventDatabaseOperationRequestChargeMetricTagNames
{
    public const string OperationType = "ef.operation.type";
    public const string OperationName = "ef.operation.name";
    public const string OperationResult = "ef.operation.result";
    public const string AggregateType = "ef.aggregate.type";
    public const string CustomPropertyPrefix = "ef.custom_property.";
    public const string DatabaseSystem = "db.system";
    public const string CosmosDbContainer = "db.cosmosdb.container";
}
