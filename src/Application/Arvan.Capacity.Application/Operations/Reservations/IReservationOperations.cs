using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Application.Operations.Reservations;

/// <summary>
/// Owns validation and write-orchestration for <see cref="Reservation"/>: each transition is the
/// entity's guarded method followed by the write that stores it and its event. Never begins or
/// commits a transaction, never calls a peer Operation.
/// </summary>
public interface IReservationOperations
{
    /// <summary>
    /// Retrieves a customer's reservation and locks its row — a consistency-critical read, because
    /// a transition is about to follow in the same transaction.
    /// </summary>
    /// <param name="id">The reservation's id.</param>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="NotFoundException">Thrown when no such reservation exists for the customer.</exception>
    Task<Reservation> GetForUpdateAsync(Guid id, string customerId, CancellationToken ct);

    /// <summary>
    /// Refuses a client reference a live reservation of the same customer already uses.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="clientReference">The reference, if the request carries one.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="ReservationClientReferenceTakenException">Thrown when the reference is taken.</exception>
    Task EnsureClientReferenceAvailableAsync(string customerId, string? clientReference, CancellationToken ct);

    /// <summary>
    /// Inserts a reservation in <see cref="ReservationStatus.Held"/> for one plan, before its
    /// capacity is taken. The caller runs this under a savepoint and discards it if the capacity
    /// turns out to be short.
    /// </summary>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="product">The product reserved.</param>
    /// <param name="region">The region.</param>
    /// <param name="onShortage">What the client asked for under shortage.</param>
    /// <param name="clientReference">The client's reference, if any.</param>
    /// <param name="plan">The zone and pools to hold in.</param>
    /// <param name="actor">Who submitted.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<Reservation> HoldAsync(
        string customerId,
        Product product,
        string region,
        ShortagePolicy onShortage,
        string? clientReference,
        HoldPlan plan,
        Actor actor,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>
    /// Inserts a reservation in <see cref="ReservationStatus.Rejected"/>, so the refusal shows in
    /// history and its idempotency key replays it.
    /// </summary>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="productCode">The product reserved.</param>
    /// <param name="region">The region.</param>
    /// <param name="onShortage">What the client asked for under shortage.</param>
    /// <param name="clientReference">The client's reference, if any.</param>
    /// <param name="lines">The lines as requested.</param>
    /// <param name="reason">Why it was refused.</param>
    /// <param name="actor">Who submitted.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<Reservation> RejectAsync(
        string customerId,
        string productCode,
        string region,
        ShortagePolicy onShortage,
        string? clientReference,
        IReadOnlyList<ReservationLine> lines,
        ReasonCode reason,
        Actor actor,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>
    /// Confirms a held reservation and stores the transition.
    /// </summary>
    /// <param name="reservation">The locked reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who confirmed.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <returns><c>true</c> when the state changed, so the capacity must move; <c>false</c> when it was already confirmed.</returns>
    Task<bool> ConfirmAsync(Reservation reservation, DateTimeOffset now, Actor actor, CancellationToken ct);

    /// <summary>
    /// Cancels a held reservation and stores the transition.
    /// </summary>
    /// <param name="reservation">The locked reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who cancelled.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <returns><c>true</c> when the state changed, so the capacity must return; <c>false</c> when it was already cancelled.</returns>
    Task<bool> CancelAsync(Reservation reservation, DateTimeOffset now, Actor actor, CancellationToken ct);

    /// <summary>
    /// Extends a held reservation's lease once and stores the transition.
    /// </summary>
    /// <param name="reservation">The locked reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="holdTtl">The product's lease duration.</param>
    /// <param name="actor">Who extended.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task ExtendAsync(Reservation reservation, DateTimeOffset now, TimeSpan holdTtl, Actor actor, CancellationToken ct);

    /// <summary>
    /// Stores the end of a lease that ran out.
    /// </summary>
    /// <param name="reservation">The locked reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task ExpireAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Records that confirmed capacity was released and stores the transition.
    /// </summary>
    /// <param name="reservation">The locked reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who released.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <returns><c>true</c> when the state changed, so the capacity must move; <c>false</c> when it was already released.</returns>
    Task<bool> ReleaseAsync(Reservation reservation, DateTimeOffset now, Actor actor, CancellationToken ct);

    /// <summary>
    /// Locks up to <paramref name="limit"/> holds whose lease has ended.
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
    /// Refuses a submit from a customer who has too many open holds, too many waiting requests (when
    /// the submit asks to wait), or whose holds on the product keep expiring unused.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="product">The product, whose limits apply.</param>
    /// <param name="onShortage">What the submit asks for under shortage.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    /// <exception cref="TooManyRequestsException">Thrown when a limit or the cooldown applies.</exception>
    Task EnsureWithinLimitsAsync(
        string customerId, Product product, ShortagePolicy onShortage, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Inserts a reservation in <see cref="ReservationStatus.Waiting"/>: it holds nothing and waits,
    /// at most the product's wait, for capacity.
    /// </summary>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="product">The product reserved.</param>
    /// <param name="region">The region.</param>
    /// <param name="zone">The zone the client asked for, if any.</param>
    /// <param name="clientReference">The client's reference, if any.</param>
    /// <param name="lines">The lines as requested.</param>
    /// <param name="actor">Who submitted.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<Reservation> WaitAsync(
        string customerId,
        Product product,
        string region,
        string? zone,
        string? clientReference,
        IReadOnlyList<ReservationLine> lines,
        Actor actor,
        DateTimeOffset now,
        CancellationToken ct);

    /// <summary>
    /// Stores that a waiting reservation got its capacity from <paramref name="plan"/>.
    /// </summary>
    /// <param name="reservation">The locked reservation.</param>
    /// <param name="plan">The pools the capacity was taken from.</param>
    /// <param name="product">The product, whose lease length applies.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task PromoteAsync(Reservation reservation, HoldPlan plan, Product product, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Stores the end of a wait that ran out.
    /// </summary>
    /// <param name="reservation">The locked reservation.</param>
    /// <param name="now">The database time.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task TimeOutAsync(Reservation reservation, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Locks up to <paramref name="limit"/> waiting requests whose wait is still running, oldest first.
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
    /// Locks up to <paramref name="limit"/> of a customer's live holds and waiting requests.
    /// </summary>
    /// <param name="customerId">The customer.</param>
    /// <param name="limit">The largest number of reservations to lock.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<IReadOnlyList<Reservation>> LockOpenAsync(string customerId, int limit, CancellationToken ct);
}
