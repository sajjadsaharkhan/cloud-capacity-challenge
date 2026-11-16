namespace Arvan.Capacity.Domain.Exceptions;

/// <summary>
/// The base for a Tier-0 guard that rejects the input it was given — a missing, oversized or
/// out-of-range value — regardless of the aggregate's state. Maps to HTTP 400.
/// </summary>
/// <param name="code">A stable, machine-readable identifier for the violated rule.</param>
/// <param name="message">A human-readable description of the violation.</param>
/// <param name="localizedMessage">A message translated for end-user display, if one is available.</param>
public abstract class DomainValidationException(string code, string message, string? localizedMessage = null)
    : DomainException(code, message, localizedMessage);
