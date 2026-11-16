using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Reservations;

namespace Arvan.Capacity.Invariants.Tests;

public sealed class ReservationStateMachineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private static readonly Actor Customer = new(ActorKind.Customer, "u1");

    private static Reservation Held() => Reservation.Hold(
        "c1", "vm-standard", "ir-thr", "ir-thr-1", ShortagePolicy.Reject, null,
        [new ReservationLine(ResourceClass.Vcpu, 1000, 1)], Ttl, Customer, T0);

    [Fact]
    public void Confirm_before_deadline_allocates_and_is_idempotent()
    {
        var r = Held();

        Assert.True(r.Confirm(T0.AddMinutes(1), Customer));
        Assert.Equal(ReservationStatus.Confirmed, r.StoredStatus);
        Assert.Equal(AllocationState.Active, r.AllocationState);
        Assert.False(r.Confirm(T0.AddMinutes(2), Customer));
    }

    [Fact]
    public void Confirm_at_or_after_deadline_is_refused_as_expired()
    {
        Assert.Throws<ReservationExpiredException>(() => Held().Confirm(T0 + Ttl, Customer));
    }

    [Fact]
    public void Client_event_and_timer_are_never_both_legal()
    {
        var atDeadline = T0 + Ttl;

        var r = Held();
        Assert.Throws<ReservationInvalidTransitionException>(() => r.Cancel(atDeadline, Customer));
        r.Expire(atDeadline, Actor.Sweeper);
        Assert.Equal(ReservationStatus.Expired, r.StoredStatus);

        Assert.Throws<ReservationInvalidTransitionException>(() => Held().Expire(atDeadline.AddTicks(-1), Actor.Sweeper));
    }

    [Fact]
    public void Extend_once_by_ttl_and_never_twice()
    {
        var r = Held();

        r.Extend(T0.AddMinutes(1), Ttl, Customer);
        Assert.Equal(T0 + Ttl * 2, r.ExpiresAt);
        Assert.Throws<ReservationAlreadyExtendedException>(() => r.Extend(T0.AddMinutes(2), Ttl, Customer));
    }

    [Fact]
    public void Held_past_deadline_reads_expired_before_it_is_stored()
    {
        var r = Held();

        Assert.Equal(ReservationStatus.Held, r.EffectiveStatusAt(T0.AddMinutes(14)));
        Assert.Equal(ReservationStatus.Expired, r.EffectiveStatusAt(T0 + Ttl));
        Assert.Equal(ReservationStatus.Held, r.StoredStatus);
    }

    [Fact]
    public void Every_transition_leaves_one_event_with_the_next_version()
    {
        var r = Held();
        r.Confirm(T0.AddMinutes(1), Customer);
        r.Release(T0.AddMinutes(2), new Actor(ActorKind.Service, "vm"));

        var events = r.TakePendingEvents();

        Assert.Equal([1, 2, 3], events.Select(e => e.Version));
        Assert.Equal(
            [ReservationEventType.Submit, ReservationEventType.Confirm, ReservationEventType.Release],
            events.Select(e => e.Event));
        Assert.Empty(r.TakePendingEvents());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_quantity_is_refused(long quantity) =>
        Assert.ThrowsAny<Exception>(() => Reservation.Hold(
            "c1", "p", "r", null, ShortagePolicy.Reject, null,
            [new ReservationLine(ResourceClass.Vcpu, quantity, 1)], Ttl, Customer, T0));
}
