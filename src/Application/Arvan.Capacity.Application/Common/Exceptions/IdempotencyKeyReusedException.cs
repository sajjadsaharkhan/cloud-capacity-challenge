namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when an idempotency key already identifies a different operation or a different body.
/// </summary>
public sealed class IdempotencyKeyReusedException()
    : UnprocessableException(
        "idempotency.key_reused",
        "This idempotency key was already used for a different operation or request body.");
