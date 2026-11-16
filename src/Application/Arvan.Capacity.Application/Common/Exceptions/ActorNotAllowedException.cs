namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when an actor kind is not allowed to perform an operation.
/// </summary>
/// <param name="operation">What was attempted.</param>
public sealed class ActorNotAllowedException(string operation)
    : ForbiddenException(
        "actor.not_allowed",
        $"This actor kind is not allowed to {operation}.");
