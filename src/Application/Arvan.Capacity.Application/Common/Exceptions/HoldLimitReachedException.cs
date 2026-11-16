namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when a customer has too many open holds, or too many waiting requests, on a product.
/// </summary>
/// <param name="productCode">The product.</param>
/// <param name="retryAfterSeconds">How long until a hold may have expired.</param>
public sealed class HoldLimitReachedException(string productCode, int retryAfterSeconds)
    : TooManyRequestsException(
        "reservation.hold_limit_reached",
        $"Too many open or waiting reservations for product '{productCode}'.",
        retryAfterSeconds);
