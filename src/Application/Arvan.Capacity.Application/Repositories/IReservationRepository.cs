using Arvan.Capacity.Domain.Entities.Reservations;

namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The write-side port for <see cref="Reservation"/>. Adding and updating also append the
/// reservation's pending events, in the same statement batch, so history can never lag a state.
/// </summary>
public interface IReservationRepository : IRepository<Reservation>
{
    /// <summary>
    /// Finds a customer's reservation and locks its row until the transaction ends, or returns
    /// <c>null</c> if it does not exist or belongs to someone else.
    /// </summary>
    /// <param name="id">The reservation's id.</param>
    /// <param name="customerId">The owning customer; another customer's reservation reads as absent.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<Reservation?> FindForUpdateAsync(Guid id, string customerId, CancellationToken ct);

    /// <summary>
    /// Writes a reservation's new state and appends its pending events, guarded by the version the
    /// row had before the transitions.
    /// </summary>
    /// <param name="reservation">The reservation, after its transitions.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the row's version moved underneath the caller, which the row lock makes impossible unless a lock was skipped.</exception>
    Task UpdateAsync(Reservation reservation, CancellationToken ct);

    /// <summary>
    /// Whether a customer already has a waiting, held or confirmed reservation with this client
    /// reference.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="clientReference">The reference to look for.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<bool> ClientReferenceInUseAsync(string customerId, string clientReference, CancellationToken ct);

    /// <summary>
    /// Locks up to <paramref name="limit"/> holds whose lease has ended, oldest deadline first,
    /// skipping rows another sweeper already holds.
    /// </summary>
    /// <param name="limit">The largest number of reservations to lock.</param>
    /// <param name="poolIds">When set, only holds with a line in one of these pools.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<Reservation>> LockExpiredHeldAsync(int limit, IReadOnlyCollection<int>? poolIds, CancellationToken ct);

    /// <summary>
    /// Whether any hold with a line in one of these pools has run past its lease but is still
    /// stored as held.
    /// </summary>
    /// <param name="poolIds">The pools to look in.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<bool> ExpiredHeldExistsAsync(IReadOnlyCollection<int> poolIds, CancellationToken ct);

    /// <summary>
    /// How many open holds and how many waiting requests a customer has on a product.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="productCode">The product.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<OpenReservationCounts> CountOpenAsync(string customerId, string productCode, CancellationToken ct);

    /// <summary>
    /// How many of a customer's holds on a product expired in the last hour, and when the latest did.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="productCode">The product.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<RecentExpiries> GetRecentExpiriesAsync(string customerId, string productCode, CancellationToken ct);

    /// <summary>
    /// Locks up to <paramref name="limit"/> waiting requests whose wait is still running, oldest
    /// first, skipping rows another sweeper already holds.
    /// </summary>
    /// <param name="limit">The largest number of reservations to lock.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<Reservation>> LockWaitingAsync(int limit, CancellationToken ct);

    /// <summary>
    /// Locks up to <paramref name="limit"/> waiting requests whose wait has ended.
    /// </summary>
    /// <param name="limit">The largest number of reservations to lock.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<Reservation>> LockTimedOutWaitingAsync(int limit, CancellationToken ct);

    /// <summary>
    /// Locks up to <paramref name="limit"/> of a customer's holds and waiting requests that are
    /// still live.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="limit">The largest number of reservations to lock.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<Reservation>> LockOpenAsync(string customerId, int limit, CancellationToken ct);
}

/// <summary>
/// A customer's open reservations on one product.
/// </summary>
/// <param name="ActiveHolds">Holds whose lease is still running.</param>
/// <param name="Waiting">Waiting requests whose wait is still running.</param>
public sealed record OpenReservationCounts(int ActiveHolds, int Waiting);

/// <summary>
/// A customer's recent expiries on one product.
/// </summary>
/// <param name="CountLastHour">How many holds expired in the last hour.</param>
/// <param name="LatestAt">When the latest one did.</param>
public sealed record RecentExpiries(int CountLastHour, DateTimeOffset? LatestAt);
