namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Signals a request that is well-formed but can never succeed as written. Maps to HTTP 422.
/// </summary>
/// <param name="code">A stable, machine-readable identifier for the failure.</param>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="localizedMessage">A message translated for end-user display, if one is available.</param>
public class UnprocessableException(string code, string message, string? localizedMessage = null)
    : BusinessException(code, message, localizedMessage);
