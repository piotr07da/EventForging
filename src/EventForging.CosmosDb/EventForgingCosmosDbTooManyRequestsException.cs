using System.Net;
using Microsoft.Azure.Cosmos;

namespace EventForging.CosmosDb;

public class EventForgingCosmosDbTooManyRequestsException : EventForgingException
{
    public EventForgingCosmosDbTooManyRequestsException(string message, TimeSpan? retryAfter, double? requestCharge, Exception innerException)
        : base(message, innerException)
    {
        RetryAfter = retryAfter;
        RequestCharge = requestCharge;
    }

    public EventForgingCosmosDbTooManyRequestsException(string message, TimeSpan? retryAfter, double? requestCharge)
        : base(message)
    {
        RetryAfter = retryAfter;
        RequestCharge = requestCharge;
    }

    public HttpStatusCode StatusCode => (HttpStatusCode)429;
    public TimeSpan? RetryAfter { get; }
    public double? RequestCharge { get; }

    internal static void ThrowIfTooManyRequests(CosmosException exception)
    {
        if (exception.StatusCode == (HttpStatusCode)429)
        {
            throw Create(exception);
        }
    }

    internal static void ThrowIfTooManyRequests(ResponseMessage response)
    {
        if (response.StatusCode == (HttpStatusCode)429)
        {
            throw Create(response.ErrorMessage, null, response.Headers.RequestCharge);
        }
    }

    internal static void ThrowIfTooManyRequests(TransactionalBatchResponse response)
    {
        if (response.StatusCode == (HttpStatusCode)429)
        {
            throw Create(response.ErrorMessage, response.RetryAfter, response.RequestCharge);
        }
    }

    private static EventForgingCosmosDbTooManyRequestsException Create(CosmosException exception)
    {
        return new EventForgingCosmosDbTooManyRequestsException(
            CreateMessage(exception.Message),
            exception.RetryAfter,
            exception.RequestCharge,
            exception);
    }

    private static EventForgingCosmosDbTooManyRequestsException Create(string? errorMessage, TimeSpan? retryAfter, double? requestCharge)
    {
        return new EventForgingCosmosDbTooManyRequestsException(CreateMessage(errorMessage), retryAfter, requestCharge);
    }

    private static string CreateMessage(string? errorMessage)
    {
        return string.IsNullOrWhiteSpace(errorMessage)
            ? "Cosmos DB request was throttled. Status code is 429 TooManyRequests."
            : $"Cosmos DB request was throttled. Status code is 429 TooManyRequests. Message: {errorMessage}";
    }
}
