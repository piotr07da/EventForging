namespace EventForging.CosmosDb.Diagnostics.Metrics;

internal sealed record EventDatabaseOperationRequestChargeMetricContext(
    string OperationType,
    string OperationName,
    Type AggregateType,
    string ContainerName,
    IDictionary<string, string>? CustomProperties);
