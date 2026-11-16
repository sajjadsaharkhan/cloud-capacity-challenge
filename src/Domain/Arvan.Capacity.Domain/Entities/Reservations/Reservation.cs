using Arvan.Capacity.Domain.Constants;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Reservations;
using Arvan.Capacity.Framework.Domain.Abstractions;

namespace Arvan.Capacity.Domain.Entities.Reservations;

/// <summary>
/// A request for capacity and its lifecycle: the request state machine. <see cref="StoredStatus"/>
/// is what the row says; <see cref="EffectiveStatusAt"/> is what a reader sees, because an
/// intermediate state past its deadline already reads as its final state before anything has
/// stored it.
/// </summary>
/// <remarks>
/// Every method takes <c>now</c> — the database transaction's clock — and never reads the host's.
/// Each transition appends a <see cref="ReservationEvent"/> to <see cref="TakePendingEvents"/>, so
/// no caller can change the state without leaving the history row behind.
/// </remarks>
public sealed class Reservation : BaseEntity<Guid>
{
    private readonly List<ReservationLine> _lines = [];
    private readonly List<ReservationEvent> _pendingEvents = [];

    /// <summary>
    /// The customer the reservation belongs to. Every query is scoped by it.
    /// </summary>
    public string CustomerId { get; private set; } = default!;

    /// <summary>
    /// The product code reserved.
    /// </summary>
    public string ProductCode { get; private set; } = default!;

    /// <summary>
    /// The region the whole reservation lives in.
    /// </summary>
    public string Region { get; private set; } = default!;

    /// <summary>
    /// The zone the zone-scoped lines were held in, when one was chosen.
    /// </summary>
    public string? Zone { get; private set; }

    /// <summary>
    /// The state stored on the row.
    /// </summary>
    public ReservationStatus StoredStatus { get; private set; }

    /// <summary>
    /// Why the reservation reached its state, when it has a reason.
    /// </summary>
    public ReasonCode? ReasonCode { get; private set; }

    /// <summary>
    /// What the client asked for when capacity is short.
    /// </summary>
    public ShortagePolicy OnShortage { get; private set; }

    /// <summary>
    /// The lease deadline. Set only while <see cref="StoredStatus"/> is <see cref="ReservationStatus.Held"/>.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// The wait deadline. Set only while <see cref="StoredStatus"/> is <see cref="ReservationStatus.Waiting"/>.
    /// </summary>
    public DateTimeOffset? WaitDeadline { get; private set; }

    /// <summary>
    /// Whether the lease has already been extended once.
    /// </summary>
    public bool Extended { get; private set; }

    /// <summary>
    /// Whether confirmed capacity is still allocated. Set only while <see cref="StoredStatus"/> is
    /// <see cref="ReservationStatus.Confirmed"/>.
    /// </summary>
    public AllocationState? AllocationState { get; private set; }

    /// <summary>
    /// The client's own reference for this reservation, unique per customer among live ones.
    /// </summary>
    public string? ClientReference { get; private set; }

    /// <summary>
    /// The optimistic-concurrency version, moved by every transition.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// The database time the reservation was created. Set once.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The database time of the last transition.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// The resource lines, with the pool each is held in once the reservation is held.
    /// </summary>
    public IReadOnlyList<ReservationLine> Lines => _lines;

    /// <summary>
    /// Reserved for materialization by the persistence layer.
    /// </summary>
    private Reservation()
    {
    }

    /// <summary>
    /// Creates a reservation that was granted: lines held, lease running. <c>NEW → HELD</c>.
    /// </summary>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="productCode">The product reserved.</param>
    /// <param name="region">The region.</param>
    /// <param name="zone">The zone the capacity was held in, if any.</param>
    /// <param name="onShortage">What the client asked for under shortage.</param>
    /// <param name="clientReference">The client's reference, if any.</param>
    /// <param name="lines">The lines, each already assigned the pool it is held in.</param>
    /// <param name="holdTtl">The product's lease duration.</param>
    /// <param name="actor">Who submitted.</param>
    /// <param name="now">The database time.</param>
    /// <exception cref="DomainValidationException">Thrown when the lines or the reference are invalid.</exception>
    public static Reservation Hold(
        string customerId,
        string productCode,
        string region,
        string? zone,
        ShortagePolicy onShortage,
        string? clientReference,
        IEnumerable<ReservationLine> lines,
        TimeSpan holdTtl,
        Actor actor,
        DateTimeOffset now)
    {
        var reservation = Create(customerId, productCode, region, zone, onShortage, clientReference, lines, now);
        reservation.StoredStatus = ReservationStatus.Held;
        reservation.ExpiresAt = now + holdTtl;
        reservation.Record(ReservationEventType.Submit, null, actor, null, now);
        return reservation;
    }

    /// <summary>
    /// Creates a reservation that was refused, so that it shows in history and its idempotency key
    /// replays the same rejection. <c>NEW → REJECTED</c>.
    /// </summary>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="productCode">The product reserved.</param>
    /// <param name="region">The region.</param>
    /// <param name="onShortage">What the client asked for under shortage.</param>
    /// <param name="clientReference">The client's reference, if any.</param>
    /// <param name="lines">The lines as requested; none of them is held.</param>
    /// <param name="reason">Why the request was refused.</param>
    /// <param name="actor">Who submitted.</param>
    /// <param name="now">The database time.</param>
    /// <exception cref="DomainValidationException">Thrown when the lines or the reference are invalid.</exception>
    public static Reservation Reject(
        string customerId,
        string productCode,
        string region,
        ShortagePolicy onShortage,
        string? clientReference,
        IEnumerable<ReservationLine> lines,
        ReasonCode reason,
        Actor actor,
        DateTimeOffset now)
    {
        var reservation = Create(customerId, productCode, region, null, onShortage, clientReference, lines, now);
        reservation.StoredStatus = ReservationStatus.Rejected;
        reservation.ReasonCode = reason;
        reservation.Record(ReservationEventType.Submit, null, actor, reason, now);
        return reservation;
    }

    /// <summary>
    /// Creates a reservation that waits, by the client's choice, for capacity that is short only
    /// temporarily. It holds nothing while it waits. <c>NEW → WAITING</c>.
    /// </summary>
    /// <param name="customerId">The owning customer.</param>
    /// <param name="productCode">The product reserved.</param>
    /// <param name="region">The region.</param>
    /// <param name="zone">The zone the client asked for, kept so a promotion honours it; <c>null</c> lets the service pick.</param>
    /// <param name="clientReference">The client's reference, if any.</param>
    /// <param name="lines">The lines as requested; none of them is held.</param>
    /// <param name="waitMax">The product's longest wait.</param>
    /// <param name="actor">Who submitted.</param>
    /// <param name="now">The database time.</param>
    /// <exception cref="DomainValidationException">Thrown when the lines or the reference are invalid.</exception>
    public static Reservation Wait(
        string customerId,
        string productCode,
        string region,
        string? zone,
        string? clientReference,
        IEnumerable<ReservationLine> lines,
        TimeSpan waitMax,
        Actor actor,
        DateTimeOffset now)
    {
        var reservation = Create(customerId, productCode, region, zone, ShortagePolicy.Wait, clientReference, lines, now);
        reservation.StoredStatus = ReservationStatus.Waiting;
        reservation.WaitDeadline = now + waitMax;
        reservation.Record(ReservationEventType.Submit, null, actor, null, now);
        return reservation;
    }

    /// <summary>
    /// A waiting reservation got its capacity: <c>WAITING → HELD</c>, with the pools it was taken
    /// from and a fresh lease.
    /// </summary>
    /// <param name="plan">The zone and pools the capacity was taken from.</param>
    /// <param name="holdTtl">The product's lease duration.</param>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who promoted it.</param>
    /// <exception cref="ReservationInvalidTransitionException">Thrown when the reservation is not waiting, or its wait has ended.</exception>
    public void Promote(HoldPlan plan, TimeSpan holdTtl, DateTimeOffset now, Actor actor)
    {
        if (StoredStatus is not ReservationStatus.Waiting || now >= WaitDeadline)
            throw new ReservationInvalidTransitionException(Id, EffectiveStatusAt(now), "be promoted");

        var pools = plan.Lines.ToDictionary(l => l.ResourceClass, l => l.PoolId);
        var promoted = _lines.Select(l => new ReservationLine(l.ResourceClass, l.Quantity, pools[l.ResourceClass])).ToList();
        _lines.Clear();
        _lines.AddRange(promoted);

        Zone = plan.Zone;
        StoredStatus = ReservationStatus.Held;
        ExpiresAt = now + holdTtl;
        WaitDeadline = null;
        Record(ReservationEventType.Promote, ReservationStatus.Waiting, actor, null, now);
    }

    /// <summary>
    /// Stores the end of a wait that ran out: <c>WAITING → REJECTED</c>.
    /// </summary>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who stored it.</param>
    /// <exception cref="ReservationInvalidTransitionException">Thrown when the reservation is not a wait past its deadline.</exception>
    public void TimeOut(DateTimeOffset now, Actor actor)
    {
        if (StoredStatus is not ReservationStatus.Waiting || now < WaitDeadline)
            throw new ReservationInvalidTransitionException(Id, EffectiveStatusAt(now), "be timed out");

        StoredStatus = ReservationStatus.Rejected;
        ReasonCode = Enums.ReasonCode.WaitTimeout;
        WaitDeadline = null;
        Record(ReservationEventType.Timeout, ReservationStatus.Waiting, actor, Enums.ReasonCode.WaitTimeout, now);
    }

    /// <summary>
    /// Confirms a hold: <c>HELD → CONFIRMED</c>, and the held capacity becomes allocated. Idempotent:
    /// confirming one that is already confirmed does nothing and is not an error.
    /// </summary>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who confirmed.</param>
    /// <returns><c>true</c> when the state changed, so the caller must move the capacity.</returns>
    /// <exception cref="ReservationExpiredException">Thrown when the lease has ended.</exception>
    /// <exception cref="ReservationInvalidTransitionException">Thrown when the reservation is in any other state.</exception>
    public bool Confirm(DateTimeOffset now, Actor actor)
    {
        if (StoredStatus is ReservationStatus.Confirmed)
            return false;

        EnsureLeaseRunning(now, "be confirmed");

        StoredStatus = ReservationStatus.Confirmed;
        AllocationState = Enums.AllocationState.Active;
        ExpiresAt = null;
        Record(ReservationEventType.Confirm, ReservationStatus.Held, actor, null, now);
        return true;
    }

    /// <summary>
    /// Cancels a hold or a wait: <c>HELD → CANCELLED</c> returns the held capacity at once; <c>WAITING → CANCELLED</c> held nothing. Idempotent:
    /// cancelling one that is already cancelled does nothing and is not an error.
    /// </summary>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who cancelled.</param>
    /// <returns><c>true</c> when the state changed; the capacity must be returned only if the reservation was held.</returns>
    /// <exception cref="ReservationInvalidTransitionException">Thrown when the reservation is in any other state, including one whose deadline has passed.</exception>
    public bool Cancel(DateTimeOffset now, Actor actor)
    {
        if (StoredStatus is ReservationStatus.Cancelled)
            return false;

        var from = StoredStatus;
        var live = (from is ReservationStatus.Held && now < ExpiresAt) || (from is ReservationStatus.Waiting && now < WaitDeadline);
        if (!live)
            throw new ReservationInvalidTransitionException(Id, EffectiveStatusAt(now), "be cancelled");

        StoredStatus = ReservationStatus.Cancelled;
        ReasonCode = Enums.ReasonCode.CancelledByClient;
        ExpiresAt = null;
        WaitDeadline = null;
        Record(ReservationEventType.Cancel, from, actor, Enums.ReasonCode.CancelledByClient, now);
        return true;
    }

    /// <summary>
    /// Extends a hold's lease once by the product's TTL, never past twice the TTL after creation.
    /// </summary>
    /// <param name="now">The database time.</param>
    /// <param name="holdTtl">The product's lease duration.</param>
    /// <param name="actor">Who extended.</param>
    /// <exception cref="ReservationExpiredException">Thrown when the lease has ended.</exception>
    /// <exception cref="ReservationAlreadyExtendedException">Thrown when the lease was already extended.</exception>
    /// <exception cref="ReservationInvalidTransitionException">Thrown when the reservation is not held.</exception>
    public void Extend(DateTimeOffset now, TimeSpan holdTtl, Actor actor)
    {
        EnsureLeaseRunning(now, "be extended");

        if (Extended)
            throw new ReservationAlreadyExtendedException(Id);

        ExpiresAt = Min(ExpiresAt!.Value + holdTtl, CreatedAt + holdTtl * 2);
        Extended = true;
        Record(ReservationEventType.Extend, ReservationStatus.Held, actor, null, now);
    }

    /// <summary>
    /// Stores the end of a lease that ran out: <c>HELD → EXPIRED</c>. Called by the sweeper or the
    /// inline finalizer, after the effective status already told readers so.
    /// </summary>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who stored it.</param>
    /// <exception cref="ReservationInvalidTransitionException">Thrown when the reservation is not a hold past its deadline.</exception>
    public void Expire(DateTimeOffset now, Actor actor)
    {
        if (StoredStatus is not ReservationStatus.Held || now < ExpiresAt)
            throw new ReservationInvalidTransitionException(Id, EffectiveStatusAt(now), "be expired");

        StoredStatus = ReservationStatus.Expired;
        ReasonCode = Enums.ReasonCode.HoldExpired;
        Record(ReservationEventType.Expire, ReservationStatus.Held, actor, Enums.ReasonCode.HoldExpired, now);
    }

    /// <summary>
    /// Records that the product service gave confirmed capacity back. Idempotent: releasing what is
    /// already released does nothing and is not an error.
    /// </summary>
    /// <param name="now">The database time.</param>
    /// <param name="actor">Who released.</param>
    /// <returns><c>true</c> when the state changed, so the caller must move the capacity.</returns>
    /// <exception cref="ReservationInvalidTransitionException">Thrown when the reservation is not confirmed.</exception>
    public bool Release(DateTimeOffset now, Actor actor)
    {
        if (StoredStatus is not ReservationStatus.Confirmed)
            throw new ReservationInvalidTransitionException(Id, EffectiveStatusAt(now), "be released");

        if (AllocationState is Enums.AllocationState.Released)
            return false;

        AllocationState = Enums.AllocationState.Released;
        Record(ReservationEventType.Release, ReservationStatus.Confirmed, actor, null, now);
        return true;
    }

    /// <summary>
    /// The status a reader sees at <paramref name="now"/>: a hold past its lease reads
    /// <see cref="ReservationStatus.Expired"/> before the sweeper has stored it. Must agree with the
    /// <c>reservation_effective</c> view.
    /// </summary>
    /// <param name="now">The database time.</param>
    public ReservationStatus EffectiveStatusAt(DateTimeOffset now) =>
        StoredStatus switch
        {
            ReservationStatus.Held when now >= ExpiresAt => ReservationStatus.Expired,
            ReservationStatus.Waiting when now >= WaitDeadline => ReservationStatus.Rejected,
            _ => StoredStatus,
        };

    /// <summary>
    /// Attaches the lines a persistence layer loaded for this reservation. Only for materialization:
    /// a reservation's lines are fixed when it is created.
    /// </summary>
    /// <param name="lines">The loaded lines.</param>
    /// <exception cref="InvalidOperationException">Thrown when the reservation already has lines.</exception>
    public void RestoreLines(IEnumerable<ReservationLine> lines)
    {
        if (_lines.Count > 0)
            throw new InvalidOperationException("The reservation's lines are fixed once it has any.");

        _lines.AddRange(lines);
    }

    /// <summary>
    /// Hands over the events the transitions since the last call produced, and forgets them. The
    /// repository appends them in the same transaction as the row.
    /// </summary>
    public IReadOnlyList<ReservationEvent> TakePendingEvents()
    {
        var events = _pendingEvents.ToArray();
        _pendingEvents.Clear();
        return events;
    }

    private static Reservation Create(
        string customerId,
        string productCode,
        string region,
        string? zone,
        ShortagePolicy onShortage,
        string? clientReference,
        IEnumerable<ReservationLine> lines,
        DateTimeOffset now)
    {
        var lineList = lines.ToList();

        if (lineList.Count is 0 or > ReservationConstant.MaxLines
            || lineList.Select(l => l.ResourceClass).Distinct().Count() != lineList.Count
            || lineList.Any(l => l.Quantity <= 0))
            throw new ReservationLinesInvalidException();

        if (clientReference is { Length: > ReservationConstant.ClientReferenceMaxLength })
            throw new ReservationClientReferenceInvalidException();

        var reservation = new Reservation
        {
            CustomerId = customerId,
            ProductCode = productCode,
            Region = region,
            Zone = zone,
            OnShortage = onShortage,
            ClientReference = clientReference,
            CreatedAt = now,
            UpdatedAt = now,
        };
        reservation.Id = Guid.CreateVersion7();
        reservation._lines.AddRange(lineList);
        return reservation;
    }

    /// <summary>
    /// Refuses a client event unless the reservation is a hold whose lease is still running.
    /// </summary>
    private void EnsureLeaseRunning(DateTimeOffset now, string attempted)
    {
        if (StoredStatus is ReservationStatus.Expired || (StoredStatus is ReservationStatus.Held && now >= ExpiresAt))
            throw new ReservationExpiredException(Id);

        if (StoredStatus is not ReservationStatus.Held)
            throw new ReservationInvalidTransitionException(Id, EffectiveStatusAt(now), attempted);
    }

    /// <summary>
    /// Moves the version and the update time and queues the event. Called by every transition after
    /// it has changed the state, so <see cref="StoredStatus"/> already holds the target.
    /// </summary>
    private void Record(ReservationEventType type, ReservationStatus? from, Actor actor, ReasonCode? reason, DateTimeOffset now)
    {
        Version++;
        UpdatedAt = now;
        _pendingEvents.Add(new ReservationEvent(Version, type, from, StoredStatus, actor, reason, now));
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
