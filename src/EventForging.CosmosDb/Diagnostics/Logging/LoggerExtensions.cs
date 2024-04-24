using Microsoft.Extensions.Logging;

namespace EventForging.CosmosDb.Diagnostics.Logging;

public static partial class LoggerExtensions
{
    [LoggerMessage(1001, LogLevel.Information, "Cannot write events to the database for aggregate '{AggregateId}' because other events have already been written for the same initiatorId '{InitiatorId}'.")]
    public static partial void WriteIgnoredDueToIdempotencyCheck(this ILogger logger, string aggregateId, Guid initiatorId);

    [LoggerMessage(1002, LogLevel.Information, "An unexpected version of aggregate '{AggregateId}' has been detected. The retrieved version was '{RetrievedVersion}', but the actual version is '{ActualVersion}'. Retrying attempt {TryIndex} of {RetryCountForUnexpectedVersionWhenExpectedVersionIsAny}. The expected version was 'Any', but in the CosmosDb implementation, a check between the retrieved and actual versions is still required.")]
    public static partial void RetryingDueToUnexpectedVersionOfAggregateWhenExpectedVersionIsAny(this ILogger logger, Exception exception, string aggregateId, AggregateVersion retrievedVersion, AggregateVersion actualVersion, int tryIndex, int retryCountForUnexpectedVersionWhenExpectedVersionIsAny);
}
