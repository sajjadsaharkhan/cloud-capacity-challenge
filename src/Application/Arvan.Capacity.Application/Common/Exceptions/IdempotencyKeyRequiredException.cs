using Arvan.Capacity.Domain.Constants;

namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when a state-changing call has no usable <c>Idempotency-Key</c>.
/// </summary>
public sealed class IdempotencyKeyRequiredException()
    : BusinessException(
        "idempotency.key_required",
        $"Header 'Idempotency-Key' is required: 1 to {ReservationConstant.IdempotencyKeyMaxLength} characters from [A-Za-z0-9_-].");
