namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when a caller exceeds the request rate of an endpoint group.
/// </summary>
/// <param name="retryAfterSeconds">Seconds until a token is available.</param>
public sealed class RateLimitedException(int retryAfterSeconds)
    : TooManyRequestsException("rate.limited", "Too many requests; slow down.", retryAfterSeconds);
