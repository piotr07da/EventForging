using System.Diagnostics.Metrics;
using EventForging.CosmosDb.Diagnostics.Tracing;

namespace EventForging.CosmosDb.Diagnostics.Metrics;

internal static class EventDatabaseOperationRequestChargeMetric
{
    public const string Name = "eventforging.cosmosdb.event_database_operation.request_charge";
    public const string Unit = "RU";

    private static readonly Counter<double> RequestChargeCounter = MeterProvider.Meter.CreateCounter<double>(
        Name,
        Unit,
        "Cosmos DB request charge consumed by EventForging Cosmos DB event database operations.");

    public static void Record(double requestCharge, string operationResult, EventDatabaseOperationRequestChargeMetricContext context, IReadOnlyCollection<string> customPropertyTagNames)
    {
        var tags = new List<KeyValuePair<string, object?>>
        {
            new(EventDatabaseOperationRequestChargeMetricTagNames.OperationType, context.OperationType),
            new(EventDatabaseOperationRequestChargeMetricTagNames.OperationName, context.OperationName),
            new(EventDatabaseOperationRequestChargeMetricTagNames.OperationResult, operationResult),
            new(EventDatabaseOperationRequestChargeMetricTagNames.AggregateType, context.AggregateType.Name),
            new(EventDatabaseOperationRequestChargeMetricTagNames.DatabaseSystem, CosmosDbTracingAttributeNames.DatabaseSystemValue),
            new(EventDatabaseOperationRequestChargeMetricTagNames.CosmosDbContainer, context.ContainerName),
        };

        foreach (var customPropertyTagName in customPropertyTagNames)
        {
            if (context.CustomProperties?.TryGetValue(customPropertyTagName, out var value) == true)
            {
                tags.Add(new KeyValuePair<string, object?>($"{EventDatabaseOperationRequestChargeMetricTagNames.CustomPropertyPrefix}{customPropertyTagName}", value));
            }
        }

        RequestChargeCounter.Add(requestCharge, tags.ToArray());
    }
}
