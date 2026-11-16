namespace Arvan.Capacity.Application.Common.Exceptions;

/// <summary>
/// Thrown when a customer's holds on a product keep expiring unused, so new ones are paused for a
/// while.
/// </summary>
/// <param name="productCode">The product.</param>
/// <param name="retryAfterSeconds">Seconds until the cooldown ends.</param>
public sealed class CooldownActiveException(string productCode, int retryAfterSeconds)
    : TooManyRequestsException(
        "reservation.cooldown_active",
        $"Too many holds on product '{productCode}' expired unused recently; try again later.",
        retryAfterSeconds);
