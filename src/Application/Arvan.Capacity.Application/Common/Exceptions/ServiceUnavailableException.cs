namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Signals that the service cannot decide right now — the database is unreachable, failing over, or
/// a statement or lock timed out. Nothing was changed, so retrying with the same idempotency key is
/// safe. Maps to HTTP 503 with a <c>Retry-After</c> header.
/// </summary>
/// <param name="innerException">What went wrong underneath.</param>
public sealed class ServiceUnavailableException(Exception innerException)
    : BusinessException(
        "service.unavailable",
        "The service is temporarily unable to decide this request. Retry with the same idempotency key.")
{
    /// <summary>
    /// The underlying failure, for logs.
    /// </summary>
    public Exception Cause { get; } = innerException;

    /// <summary>
    /// How long the caller should wait before retrying.
    /// </summary>
    public int RetryAfterSeconds => 1;
}
