using Arvan.Capacity.Domain.Entities.Reservations;

namespace Arvan.Capacity.Domain.Constants;

/// <summary>
/// Length and count limits for <see cref="Reservation"/>.
/// </summary>
public static class ReservationConstant
{
    /// <summary>
    /// The largest number of resource lines one reservation may carry.
    /// </summary>
    public const int MaxLines = 16;

    /// <summary>
    /// The maximum length of <see cref="Reservation.ClientReference"/>.
    /// </summary>
    public const int ClientReferenceMaxLength = 128;

    /// <summary>
    /// The maximum length of an idempotency key.
    /// </summary>
    public const int IdempotencyKeyMaxLength = 64;

    /// <summary>
    /// How long a stored idempotency response stays replayable, counted from first use. Longer
    /// than any reservation's lifetime.
    /// </summary>
    public static readonly TimeSpan IdempotencyRetention = TimeSpan.FromHours(24);
}
