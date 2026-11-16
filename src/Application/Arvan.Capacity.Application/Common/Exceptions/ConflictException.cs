namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Signals that a request conflicts with the current state of an aggregate. Maps to HTTP 409.
/// </summary>
/// <param name="code">A stable, machine-readable identifier for the failure.</param>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="localizedMessage">A message translated for end-user display, if one is available.</param>
public class ConflictException(string code, string message, string? localizedMessage = null)
    : BusinessException(code, message, localizedMessage);