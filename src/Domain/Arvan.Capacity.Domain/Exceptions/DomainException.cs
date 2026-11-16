namespace Arvan.Capacity.Domain.Exceptions;

/// <summary>
/// The base for a Tier-0 guard violation — a rule decidable from an entity's own in-memory state —
/// that the aggregate's current state refuses. Maps to HTTP 409. A guard that rejects the input
/// itself derives from <see cref="DomainValidationException"/> instead.
/// </summary>
/// <param name="code">A stable, machine-readable identifier for the violated rule.</param>
/// <param name="message">A human-readable description of the violation.</param>
/// <param name="localizedMessage">A message translated for end-user display, if one is available.</param>
public abstract class DomainException(string code, string message, string? localizedMessage = null)
    : Exception(message)
{
    /// <summary>
    /// A stable, machine-readable identifier for the violated rule.
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// A message translated for end-user display, if one is available.
    /// </summary>
    public string? LocalizedMessage { get; } = localizedMessage;
}
