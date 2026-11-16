using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Entities.Reservations;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions.Capacity;
using Arvan.Capacity.Domain.Exceptions.Products;
using Arvan.Capacity.Domain.Exceptions.Reservations;

namespace Arvan.Capacity.Invariants.Tests;

public sealed class DomainTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(15);
    private static readonly Actor Who = new(ActorKind.Customer, "u");

    private static InventorySnapshot Inv(
        int pool, string? zone, ResourceClass resourceClass, long total, long held = 0, long used = 0,
        string? host = null, PoolAdminState state = PoolAdminState.Available, decimal ratio = 1) =>
        new(pool, zone, host, state, resourceClass, total, 0, ratio, held, used, 1, 1, total);

    [Theory]
    [InlineData(100, 0, 1, 100)]
    [InlineData(100, 10, 2, 180)]
    [InlineData(101, 0, 1.5, 151)]
    [InlineData(10, 10, 4, 0)]
    public void Sellable_is_floor_of_total_minus_reserved_times_ratio(long total, long reserved, double ratio, long expected) =>
        Assert.Equal(expected, InventorySnapshot.ComputeSellable(total, reserved, (decimal)ratio));

    [Fact]
    public void Free_is_zero_unless_the_pool_is_available()
    {
        Assert.Equal(70, Inv(1, "z", ResourceClass.Vcpu, 100, held: 20, used: 10).Free);
        Assert.Equal(0, Inv(1, "z", ResourceClass.Vcpu, 100, state: PoolAdminState.Offline).Free);
    }

    [Theory]
    [InlineData(1000, ReasonCode.ExceedsRegisteredCapacity)]
    [InlineData(70, ReasonCode.CapacitySoldOut)]
    [InlineData(30, ReasonCode.CapacityTemporarilyUnavailable)]
    public void Shortage_is_classified_against_registered_capacity_then_live_allocations(long quantity, ReasonCode expected) =>
        Assert.Equal(expected, ShortageClassifier.Classify(quantity, [Inv(1, "z", ResourceClass.Vcpu, 100, held: 40, used: 40)]));

    [Fact]
    public void An_offline_pool_counts_as_registered_but_never_as_free() =>
        Assert.Equal(ReasonCode.CapacityTemporarilyUnavailable,
            ShortageClassifier.Classify(50, [Inv(1, "z", ResourceClass.Vcpu, 100, state: PoolAdminState.Offline)]));

    [Fact]
    public void The_most_severe_line_code_is_the_reservations_code() =>
        Assert.Equal(ReasonCode.ExceedsRegisteredCapacity,
            ShortageClassifier.MostSevere([ReasonCode.CapacityTemporarilyUnavailable, ReasonCode.ExceedsRegisteredCapacity, ReasonCode.CapacitySoldOut]));

    [Theory]
    [InlineData(500, true)]
    [InlineData(1500, true)]
    [InlineData(250, false)]   // below min
    [InlineData(1100, false)]  // not a multiple of the step
    [InlineData(5000, false)]  // above max
    public void Quantities_must_satisfy_min_step_and_max(long quantity, bool allowed)
    {
        var inv = new InventorySnapshot(1, "z", null, PoolAdminState.Available, ResourceClass.Vcpu, 100000, 0, 1, 0, 0, 500, 500, 2000);

        if (allowed) inv.EnsureQuantityAllowed(quantity);
        else Assert.Throws<QuantityNotAllowedException>(() => inv.EnsureQuantityAllowed(quantity));
    }

    [Fact]
    public void Zones_are_ranked_by_how_comfortably_the_request_fits()
    {
        var lines = new[] { new ReservationLine(ResourceClass.Vcpu, 1000) };
        var plans = HoldPlanner.Plan(null, lines,
            [Inv(1, "a", ResourceClass.Vcpu, 2000), Inv(2, "b", ResourceClass.Vcpu, 9000), Inv(3, "c", ResourceClass.Vcpu, 5000)]);

        Assert.Equal(["b", "c"], plans.Select(p => p.Zone));   // at most two zones
    }

    [Fact]
    public void A_named_zone_is_the_only_zone_tried() =>
        Assert.Equal(["a"], HoldPlanner.Plan("a", [new ReservationLine(ResourceClass.Vcpu, 1000)],
            [Inv(1, "a", ResourceClass.Vcpu, 2000), Inv(2, "b", ResourceClass.Vcpu, 9000)]).Select(p => p.Zone));

    [Fact]
    public void Host_scoped_lines_take_hosts_by_best_fit_and_stay_on_one_host()
    {
        var lines = new[] { new ReservationLine(ResourceClass.Gpu, 3), new ReservationLine(ResourceClass.Vcpu, 1000) };
        var plans = HoldPlanner.Plan("z", lines,
        [
            Inv(1, "z", ResourceClass.Vcpu, 9000),
            Inv(10, "z", ResourceClass.Gpu, 8, host: "h1"),
            Inv(11, "z", ResourceClass.Gpu, 8, held: 4, host: "h2"),   // free 4: the tightest fit for 3
            Inv(12, "z", ResourceClass.Gpu, 8, held: 7, host: "h3"),   // free 1: does not fit
        ]);

        Assert.Equal([11, 10, 12], plans.Select(p => p.Lines.Single(l => l.ResourceClass == ResourceClass.Gpu).PoolId));
        Assert.True(plans.Count <= HoldPlanner.MaxAttempts);
    }

    [Fact]
    public void Region_scoped_lines_use_the_region_pool_whatever_the_zone()
    {
        var plans = HoldPlanner.Plan(null, [new ReservationLine(ResourceClass.Ipv4, 1)], [Inv(7, null, ResourceClass.Ipv4, 100)]);
        Assert.Equal(7, Assert.Single(plans).Lines.Single().PoolId);
    }

    [Fact]
    public void No_plan_when_a_class_has_no_pool_in_the_zone() =>
        Assert.Empty(HoldPlanner.Plan("a",
            [new ReservationLine(ResourceClass.Vcpu, 1), new ReservationLine(ResourceClass.RamMb, 1)],
            [Inv(1, "a", ResourceClass.Vcpu, 10)]));

    [Theory]
    [InlineData(0, 100, CapacityLevel.Unavailable)]
    [InlineData(9, 100, CapacityLevel.Limited)]
    [InlineData(10, 100, CapacityLevel.Available)]
    public void Capacity_level_is_unavailable_at_zero_and_limited_below_a_tenth(long free, long sellable, CapacityLevel expected) =>
        Assert.Equal(expected, CapacityLevels.Of(free, sellable));

    [Fact]
    public void A_product_is_as_available_as_its_worst_class() =>
        Assert.Equal(CapacityLevel.Limited, CapacityLevels.Worst([CapacityLevel.Available, CapacityLevel.Limited]));

    // ---- the pool state machine, every (state, event) pair ----

    private static Pool In(PoolAdminState state)
    {
        var pool = Pool.Register(1, "r", "z", null);
        if (state is PoolAdminState.Available or PoolAdminState.Offline) pool.Activate();
        if (state is PoolAdminState.Offline) pool.TakeOffline();
        if (state is PoolAdminState.Retired) pool.Retire(false);
        return pool;
    }

    public static TheoryData<PoolAdminState, string, PoolAdminState?> PoolTable => new()
    {
        { PoolAdminState.Registered, "activate", PoolAdminState.Available },
        { PoolAdminState.Registered, "take_offline", null },
        { PoolAdminState.Registered, "restore", null },
        { PoolAdminState.Registered, "retire", PoolAdminState.Retired },
        { PoolAdminState.Available, "activate", null },
        { PoolAdminState.Available, "take_offline", PoolAdminState.Offline },
        { PoolAdminState.Available, "restore", null },
        { PoolAdminState.Available, "retire", null },
        { PoolAdminState.Offline, "activate", null },
        { PoolAdminState.Offline, "take_offline", null },
        { PoolAdminState.Offline, "restore", PoolAdminState.Available },
        { PoolAdminState.Offline, "retire", PoolAdminState.Retired },
        { PoolAdminState.Retired, "activate", null },
        { PoolAdminState.Retired, "take_offline", null },
        { PoolAdminState.Retired, "restore", null },
        { PoolAdminState.Retired, "retire", null },
    };

    [Theory]
    [MemberData(nameof(PoolTable))]
    public void Every_pool_state_and_event_pair_is_either_the_listed_move_or_refused(PoolAdminState from, string ev, PoolAdminState? to)
    {
        var pool = In(from);

        void Apply()
        {
            switch (ev)
            {
                case "activate": pool.Activate(); break;
                case "take_offline": pool.TakeOffline(); break;
                case "restore": pool.Restore(); break;
                case "retire": pool.Retire(false); break;
            }
        }

        if (to is null) Assert.Throws<PoolInvalidTransitionException>(Apply);
        else { Apply(); Assert.Equal(to, pool.AdminState); }
    }

    [Fact]
    public void An_offline_pool_with_units_in_use_cannot_be_retired() =>
        Assert.Throws<PoolInvalidTransitionException>(() => In(PoolAdminState.Offline).Retire(hasHeldOrUsedUnits: true));

    // ---- the request state machine, every (state, event) pair ----

    private static Reservation In(ReservationStatus state)
    {
        var lines = new[] { new ReservationLine(ResourceClass.Vcpu, 1000, 1) };
        var plan = new HoldPlan("z", [new LineAllocation(1, ResourceClass.Vcpu, 1000)]);

        switch (state)
        {
            case ReservationStatus.Waiting:
                return Reservation.Wait("c", "p", "r", null, null, [new ReservationLine(ResourceClass.Vcpu, 1000)], Ttl, Who, T0);
            case ReservationStatus.Rejected:
                return Reservation.Reject("c", "p", "r", ShortagePolicy.Reject, null, lines, ReasonCode.CapacitySoldOut, Who, T0);
        }

        var held = Reservation.Hold("c", "p", "r", "z", ShortagePolicy.Reject, null, lines, Ttl, Who, T0);
        switch (state)
        {
            case ReservationStatus.Confirmed: held.Confirm(T0.AddMinutes(1), Who); break;
            case ReservationStatus.Cancelled: held.Cancel(T0.AddMinutes(1), Who); break;
            case ReservationStatus.Expired: held.Expire(T0 + Ttl, Actor.Sweeper); break;
        }

        _ = plan;
        return held;
    }

    public static TheoryData<ReservationStatus, string, ReservationStatus?> RequestTable()
    {
        var data = new TheoryData<ReservationStatus, string, ReservationStatus?>();
        foreach (var state in Enum.GetValues<ReservationStatus>())
        foreach (var ev in new[] { "confirm", "cancel", "extend", "expire", "promote", "timeout", "release" })
        {
            ReservationStatus? to = (state, ev) switch
            {
                (ReservationStatus.Held, "confirm") => ReservationStatus.Confirmed,
                (ReservationStatus.Held, "cancel") => ReservationStatus.Cancelled,
                (ReservationStatus.Held, "extend") => ReservationStatus.Held,
                (ReservationStatus.Held, "expire") => ReservationStatus.Expired,
                (ReservationStatus.Waiting, "cancel") => ReservationStatus.Cancelled,
                (ReservationStatus.Waiting, "promote") => ReservationStatus.Held,
                (ReservationStatus.Waiting, "timeout") => ReservationStatus.Rejected,
                (ReservationStatus.Confirmed, "release") => ReservationStatus.Confirmed,
                // Repeats that already produced the current state are legal no-ops.
                (ReservationStatus.Confirmed, "confirm") => ReservationStatus.Confirmed,
                (ReservationStatus.Cancelled, "cancel") => ReservationStatus.Cancelled,
                _ => null,
            };
            data.Add(state, ev, to);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(RequestTable))]
    public void Every_request_state_and_event_pair_is_the_listed_move_or_refused(ReservationStatus from, string ev, ReservationStatus? to)
    {
        var r = In(from);
        var plan = new HoldPlan("z", [new LineAllocation(1, ResourceClass.Vcpu, 1000)]);

        // Held and waiting are judged just before their deadline; the timer events just after.
        var now = ev is "expire" ? T0 + Ttl : ev is "timeout" ? T0 + Ttl : T0.AddMinutes(2);

        void Apply()
        {
            switch (ev)
            {
                case "confirm": r.Confirm(now, Who); break;
                case "cancel": r.Cancel(now, Who); break;
                case "extend": r.Extend(now, Ttl, Who); break;
                case "expire": r.Expire(now, Actor.Sweeper); break;
                case "promote": r.Promote(plan, Ttl, now, Actor.Sweeper); break;
                case "timeout": r.TimeOut(now, Actor.Sweeper); break;
                case "release": r.Release(now, Who); break;
            }
        }

        if (to is null) Assert.ThrowsAny<Domain.Exceptions.DomainException>(Apply);
        else { Apply(); Assert.Equal(to, r.StoredStatus); }
    }

    [Fact]
    public void A_wait_is_promoted_with_pools_and_a_fresh_lease_and_never_after_its_deadline()
    {
        var plan = new HoldPlan("z", [new LineAllocation(9, ResourceClass.Vcpu, 1000)]);
        var r = Reservation.Wait("c", "p", "r", null, null, [new ReservationLine(ResourceClass.Vcpu, 1000)], Ttl, Who, T0);

        Assert.Throws<ReservationInvalidTransitionException>(() => r.Promote(plan, Ttl, T0 + Ttl, Actor.Sweeper));

        r.Promote(plan, Ttl, T0.AddMinutes(5), Actor.Sweeper);
        Assert.Equal(T0.AddMinutes(20), r.ExpiresAt);
        Assert.Equal(9, r.Lines.Single().PoolId);
        Assert.Equal("z", r.Zone);
        Assert.Null(r.WaitDeadline);
    }

    [Theory]
    [InlineData(ReservationEventType.Submit, ReservationStatus.Held, "reservation.held")]
    [InlineData(ReservationEventType.Submit, ReservationStatus.Waiting, "reservation.waiting")]
    [InlineData(ReservationEventType.Promote, ReservationStatus.Held, "reservation.held")]
    [InlineData(ReservationEventType.Timeout, ReservationStatus.Rejected, "reservation.rejected")]
    [InlineData(ReservationEventType.Release, ReservationStatus.Confirmed, "reservation.released")]
    public void Events_are_published_under_the_routing_keys_of_the_spec(ReservationEventType ev, ReservationStatus to, string key) =>
        Assert.Equal(key, new ReservationEvent(1, ev, null, to, Who, null, T0).RoutingKey);

    [Fact]
    public void Product_and_inventory_definitions_refuse_values_outside_their_ranges()
    {
        var ok = new ProductDefinition(ProductStatus.Active, [ResourceClass.Vcpu], 900, 900, 20, 4, 100);
        ok.EnsureValid();

        Assert.Throws<ProductDefinitionInvalidException>(() => (ok with { HoldTtlSeconds = 60 }).EnsureValid());
        Assert.Throws<ProductDefinitionInvalidException>(() => (ok with { MaxPoolSharePct = 0 }).EnsureValid());
        Assert.Throws<ProductDefinitionInvalidException>(() => (ok with { ResourceClasses = [] }).EnsureValid());

        new InventoryConfiguration(100, 10, 2, 1, 1, 50).EnsureValid();
        Assert.Throws<InventoryConfigurationInvalidException>(() => new InventoryConfiguration(100, 200, 1, 1, 1, 50).EnsureValid());
        Assert.Throws<InventoryConfigurationInvalidException>(() => new InventoryConfiguration(100, 0, 0.5m, 1, 1, 50).EnsureValid());
    }
}
