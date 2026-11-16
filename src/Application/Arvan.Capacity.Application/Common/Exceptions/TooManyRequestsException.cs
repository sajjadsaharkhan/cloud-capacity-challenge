namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Signals a limit the caller reached: retrying later can succeed. Maps to HTTP 429 with a
/// <c>Retry-After</c> header.
/// </summary>
/// <param name="code">A stable, machine-readable identifier for the failure.</param>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="retryAfterSeconds">How long the caller should wait before retrying.</param>
/// <param name="localizedMessage">A message translated for end-user display, if one is available.</param>
public class TooManyRequestsException(string code, string message, int retryAfterSeconds, string? localizedMessage = null)
    : BusinessException(code, message, localizedMessage)
{
    /// <summary>
    /// How long the caller should wait before retrying.
    /// </summary>
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
