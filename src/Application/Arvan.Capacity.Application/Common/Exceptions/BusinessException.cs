namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// The base type for a failure an Operation decides to signal — the point where a
/// repository's raw fact (a bool, a count, an entity-or-null) becomes a business decision.
/// </summary>
/// <param name="code">A stable, machine-readable identifier for the failure.</param>
/// <param name="message">A human-readable description of the failure.</param>
/// <param name="localizedMessage">A message translated for end-user display, if one is available.</param>
public abstract class BusinessException(string code, string message, string? localizedMessage = null) : Exception(message)
{
    /// <summary>
    /// A stable, machine-readable identifier for the failure.
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// A message translated for end-user display, if one is available.
    /// </summary>
    public string? LocalizedMessage { get; } = localizedMessage;
}
