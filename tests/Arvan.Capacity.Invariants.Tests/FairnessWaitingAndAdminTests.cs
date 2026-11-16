using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.UseCases.Admin.AdjustLedger;
using Arvan.Capacity.Application.UseCases.Admin.CancelOpenReservations;
using Arvan.Capacity.Application.UseCases.Admin.ChangePoolState;
using Arvan.Capacity.Application.UseCases.Admin.GetReconciliation;
using Arvan.Capacity.Application.UseCases.Admin.PutInventory;
using Arvan.Capacity.Application.UseCases.Admin.PutProduct;
using Arvan.Capacity.Application.UseCases.Admin.PutQuota;
using Arvan.Capacity.Application.UseCases.Capacity.FitCheck;
using Arvan.Capacity.Application.UseCases.Capacity.GetCapacityLevels;
using Arvan.Capacity.Application.UseCases.Reservations.PromoteWaiting;
using Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;
using Arvan.Capacity.Application.UseCases.Reservations.TimeOutWaiting;
using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Entities.Products;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// Quotas, fairness controls, waiting, the capacity reads and the admin API, end to end.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class FairnessWaitingAndAdminTests(PostgresFixture fixture) : IAsyncLifetime
{
    private static readonly Actor Admin = new(ActorKind.Admin, "op");
    private CapacityTestHost _host = default!;

    public async Task InitializeAsync() => _host = await CapacityTestHost.CreateAsync(fixture);

    public async Task DisposeAsync()
    {
        await _host.AssertInvariantsAsync();
        await _host.DisposeAsync();
    }

    private Task<T> UseCase<T>(Func<IServiceProvider, Task<T>> call) => _host.Run(call);

    [Fact]
    public async Task Quota_counts_open_holds_and_never_means_unlimited()
    {
        await _host.Execute("INSERT INTO quotas (customer_id, resource_class, region, quota_limit) VALUES ('q', 'VCPU', 'ir-thr', 3000)");

        Assert.Equal(ReservationStatus.Held, (await _host.SubmitAsync(CapacityTestHost.Submit("q", "q1", 2000))).Reservation.Status);
        var refused = await _host.SubmitAsync(CapacityTestHost.Submit("q", "q2", 2000));
        Assert.Equal(ReasonCode.QuotaExceeded, refused.Reservation.ReasonCode);
        Assert.Equal(ReservationStatus.Rejected, refused.Reservation.Status);

        // A quota shortage never waits.
        var wait = await _host.SubmitAsync(CapacityTestHost.Submit("q", "q3", 2000, onShortage: ShortagePolicy.Wait));
        Assert.Equal(ReservationStatus.Rejected, wait.Reservation.Status);

        // No contract row: the class default applies (GPU defaults to 0).
        var gpu = await _host.SubmitAsync(new SubmitReservationInput(
            "nobody", CapacityTestHost.Customer("nobody"), "g", "vm-gpu", "ir-thr", "ir-thr-1",
            [new SubmitReservationLineInput(ResourceClass.Gpu, 1)], ShortagePolicy.Reject, null));
        Assert.Equal(ReasonCode.QuotaExceeded, gpu.Reservation.ReasonCode);
    }

    [Fact]
    public async Task Lowering_a_quota_below_usage_keeps_existing_holds_and_blocks_new_ones()
    {
        await _host.SubmitAsync(CapacityTestHost.Submit("shrink", "s1", 4000));

        await UseCase(sp => sp.GetRequiredService<PutQuotaUseCase>().ExecuteAsync(
            new PutQuotaInput(Admin, "pq", "shrink", ResourceClass.Vcpu, "ir-thr", 1000), default));

        Assert.Equal(1, await _host.Scalar("SELECT count(*) FROM reservations WHERE customer_id = 'shrink' AND stored_status = 'HELD'"));
        Assert.Equal(ReasonCode.QuotaExceeded, (await _host.SubmitAsync(CapacityTestHost.Submit("shrink", "s2", 500))).Reservation.ReasonCode);
    }

    [Fact]
    public async Task Hold_limit_share_cap_and_cooldown_apply_to_a_customer_hoarding_a_scarce_product()
    {
        await _host.Execute("INSERT INTO quotas (customer_id, resource_class, region, quota_limit) VALUES ('hog', 'GPU', 'ir-thr', 100), ('hog', 'VCPU', 'ir-thr', 1000000)");

        SubmitReservationInput Gpu(string key, long n) => new(
            "hog", CapacityTestHost.Customer("hog"), key, "vm-gpu", "ir-thr", "ir-thr-1",
            [new SubmitReservationLineInput(ResourceClass.Gpu, n)], ShortagePolicy.Reject, null);

        // Share cap: 25% of an 8-GPU host is 2, so a third GPU on one host is refused, but another host serves it.
        var first = await _host.SubmitAsync(Gpu("h1", 2));
        var second = await _host.SubmitAsync(Gpu("h2", 2));
        Assert.Equal(ReservationStatus.Held, first.Reservation.Status);
        Assert.Equal(ReservationStatus.Held, second.Reservation.Status);
        Assert.Equal(2, await _host.Scalar("SELECT count(DISTINCT pool_id) FROM reservation_lines WHERE resource_class = 'GPU'"));

        // Three hosts to try; three more holds of 2 exhaust the cap on all of them.
        await _host.SubmitAsync(Gpu("h3", 2));
        var capped = await _host.SubmitAsync(Gpu("h4", 2));
        Assert.Equal(ReasonCode.PoolShareExceeded, capped.Reservation.ReasonCode);

        // Cooldown: five expiries in the last hour, the latest within ten minutes.
        await _host.Execute("""
            INSERT INTO reservations (id, customer_id, product_code, region, stored_status, reason_code, on_shortage, version, created_at, updated_at)
            SELECT gen_random_uuid(), 'cool', 'vm-standard', 'ir-thr', 'EXPIRED', 'HOLD_EXPIRED', 'REJECT', 2, now() - interval '30 minutes', now() - interval '2 minutes'
            FROM generate_series(1, 5)
            """);
        await Assert.ThrowsAsync<CooldownActiveException>(() => _host.SubmitAsync(CapacityTestHost.Submit("cool", "cool1")));
    }

    [Fact]
    public async Task The_hold_limit_and_the_waiting_limit_apply_per_customer_and_product()
    {
        await _host.Execute("UPDATE products SET max_active_holds = 2, max_waiting = 1 WHERE code = 'vm-standard'");

        await _host.SubmitAsync(CapacityTestHost.Submit("lim", "l1"));
        await _host.SubmitAsync(CapacityTestHost.Submit("lim", "l2"));
        var limited = await Assert.ThrowsAsync<HoldLimitReachedException>(() => _host.SubmitAsync(CapacityTestHost.Submit("lim", "l3")));
        Assert.True(limited.RetryAfterSeconds > 0);

        // Someone else is not affected.
        Assert.Equal(ReservationStatus.Held, (await _host.SubmitAsync(CapacityTestHost.Submit("other", "o1"))).Reservation.Status);

        // The waiting limit: one wait allowed.
        await _host.ShrinkVcpu("ir-thr-2", 1);
        await _host.SubmitAsync(CapacityTestHost.Submit("w", "w1", 1000, "ir-thr-2"));
        Assert.Equal(ReservationStatus.Waiting, (await _host.SubmitAsync(CapacityTestHost.Submit("w", "w2", 1000, "ir-thr-2", onShortage: ShortagePolicy.Wait))).Reservation.Status);
        await Assert.ThrowsAsync<HoldLimitReachedException>(() => _host.SubmitAsync(CapacityTestHost.Submit("w", "w3", 1000, "ir-thr-2", onShortage: ShortagePolicy.Wait)));
    }

    [Fact]
    public async Task A_wait_is_opt_in_promoted_in_order_and_rejected_at_its_deadline()
    {
        await _host.ShrinkVcpu("ir-thr-1", 2);
        var a = await _host.SubmitAsync(CapacityTestHost.Submit("w", "a", 2000));
        Assert.Equal(ReservationStatus.Held, a.Reservation.Status);

        // Temporary shortage + WAIT = WAITING; without WAIT it is rejected with a retry hint.
        var waiting = await _host.SubmitAsync(CapacityTestHost.Submit("w2", "w2", 1000, onShortage: ShortagePolicy.Wait));
        Assert.Equal(ReservationStatus.Waiting, waiting.Reservation.Status);
        var plain = await _host.SubmitAsync(CapacityTestHost.Submit("w3", "w3", 1000));
        Assert.Equal(ReasonCode.CapacityTemporarilyUnavailable, plain.Reservation.ReasonCode);
        Assert.InRange(plain.RetryAfterSeconds ?? 0, 1, 60);

        // Nothing free yet: promotion leaves it waiting.
        Assert.Equal(0, (await UseCase(sp => sp.GetRequiredService<PromoteWaitingUseCase>().ExecuteAsync(new PromoteWaitingInput(50), default))).Promoted);

        await _host.CancelAsync(a.Reservation.Id, "w", "ca");
        Assert.Equal(1, (await UseCase(sp => sp.GetRequiredService<PromoteWaitingUseCase>().ExecuteAsync(new PromoteWaitingInput(50), default))).Promoted);
        Assert.Equal("HELD", await Text($"SELECT stored_status FROM reservations WHERE id = '{waiting.Reservation.Id}'"));

        // A wait that runs out is rejected, and holds nothing.
        await _host.SubmitAsync(CapacityTestHost.Submit("w4", "w4", 2000, onShortage: ShortagePolicy.Wait));
        await _host.Execute("UPDATE reservations SET wait_deadline = now() - interval '1 second' WHERE stored_status = 'WAITING'");
        Assert.Equal(1, (await UseCase(sp => sp.GetRequiredService<TimeOutWaitingUseCase>().ExecuteAsync(new TimeOutWaitingInput(50), default))).TimedOut);
        Assert.Equal("WAIT_TIMEOUT", await Text("SELECT reason_code FROM reservations WHERE customer_id = 'w4'"));
    }

    [Fact]
    public async Task Capacity_levels_count_expired_holds_as_free_and_hide_numbers_from_customers()
    {
        await _host.Execute("UPDATE inventories SET total = 1000, reserved = 0, allocation_ratio = 1, min_unit = 1000, step_size = 1000, max_unit = 1000 WHERE resource_class = 'VCPU'");

        LevelOf(await Levels(ActorKind.Customer), "vm-standard", "ir-thr", CapacityLevel.Available);

        var holds = new List<Guid>();
        foreach (var zone in new[] { "ir-thr-1", "ir-thr-2" })
            holds.Add((await _host.SubmitAsync(CapacityTestHost.Submit($"lv{zone}", $"lv{zone}", 1000, zone))).Reservation.Id);

        LevelOf(await Levels(ActorKind.Customer), "vm-standard", "ir-thr", CapacityLevel.Unavailable);

        foreach (var id in holds)
            await _host.Age(id);

        LevelOf(await Levels(ActorKind.Customer), "vm-standard", "ir-thr", CapacityLevel.Available);

        await Assert.ThrowsAsync<ActorNotAllowedException>(() => Levels(ActorKind.Customer, detail: true));
        Assert.NotNull((await Levels(ActorKind.Admin, detail: true)).Pools);
    }

    [Fact]
    public async Task Fit_check_is_advisory_and_includes_the_callers_quota()
    {
        await _host.Execute("INSERT INTO quotas (customer_id, resource_class, region, quota_limit) VALUES ('fc', 'VCPU', 'ir-thr', 1000)");

        FitCheckInput Check(long vcpu) => new("fc", "vm-standard", "ir-thr", null, [new FitCheckLineInput(ResourceClass.Vcpu, vcpu)]);

        Assert.True((await UseCase(sp => sp.GetRequiredService<FitCheckUseCase>().ExecuteAsync(Check(500), default))).Fits);

        var overQuota = await UseCase(sp => sp.GetRequiredService<FitCheckUseCase>().ExecuteAsync(Check(1500), default));
        Assert.False(overQuota.Fits);
        Assert.Equal(ReasonCode.QuotaExceeded, overQuota.Code);

        Assert.Equal(0, await _host.Scalar("SELECT count(*) FROM reservations WHERE customer_id = 'fc'"));
    }

    [Fact]
    public async Task Pool_states_follow_the_resource_state_machine_and_offline_pools_take_no_holds()
    {
        var pool = (int)await _host.ZonePool("ir-thr-1");

        await Assert.ThrowsAsync<Domain.Exceptions.Capacity.PoolInvalidTransitionException>(() => PoolEvent(pool, "restore"));

        var held = await _host.SubmitAsync(CapacityTestHost.Submit("off", "o1", 1000, "ir-thr-1"));
        await PoolEvent(pool, "take_offline");

        // Existing holds finish normally; new holds land elsewhere.
        await _host.ConfirmAsync(held.Reservation.Id, "off", "o2");
        var next = await _host.SubmitAsync(CapacityTestHost.Submit("off", "o3", 1000, "ir-thr-1"));
        Assert.Equal(ReservationStatus.Rejected, next.Reservation.Status);

        // Not retireable while units are in use.
        await Assert.ThrowsAsync<Domain.Exceptions.Capacity.PoolInvalidTransitionException>(() => PoolEvent(pool, "retire"));

        await PoolEvent(pool, "restore");
        Assert.Equal(ReservationStatus.Held, (await _host.SubmitAsync(CapacityTestHost.Submit("off", "o4", 1000, "ir-thr-1"))).Reservation.Status);
    }

    [Fact]
    public async Task An_inventory_below_usage_is_refused_or_forced_offline_and_products_can_be_retired()
    {
        var pool = (int)await _host.ZonePool("ir-thr-1");
        await _host.SubmitAsync(CapacityTestHost.Submit("inv", "i1", 4000, "ir-thr-1"));
        var small = new InventoryConfiguration(1000, 0, 1, 500, 500, 1000);

        await Assert.ThrowsAsync<Domain.Exceptions.Capacity.InventoryBelowUsageException>(() => UseCase(sp =>
            sp.GetRequiredService<PutInventoryUseCase>().ExecuteAsync(new PutInventoryInput(Admin, "ik1", pool, ResourceClass.Vcpu, small, false), default)));

        var forced = await UseCase(sp => sp.GetRequiredService<PutInventoryUseCase>().ExecuteAsync(new PutInventoryInput(Admin, "ik2", pool, ResourceClass.Vcpu, small, true), default));
        Assert.False(forced.Applied);
        Assert.True(forced.PoolTakenOffline);
        Assert.Equal(4000, forced.Held);
        Assert.Equal("OFFLINE", await Text($"SELECT admin_state FROM pools WHERE id = {pool}"));

        // A retired product takes no new submits, but its holds are untouched.
        await UseCase(sp => sp.GetRequiredService<PutProductUseCase>().ExecuteAsync(new PutProductInput(
            Admin, "pp", "vm-standard",
            new ProductDefinition(ProductStatus.Retired, [ResourceClass.Vcpu, ResourceClass.RamMb, ResourceClass.DiskGb, ResourceClass.Ipv4], 900, 900, 20, 4, 100)), default));
        await Task.Delay(2100); // the catalog is cached for two seconds
        await Assert.ThrowsAsync<ProductNotAvailableException>(() => _host.SubmitAsync(CapacityTestHost.Submit("inv", "i3", 500, "ir-thr-2")));
    }

    [Fact]
    public async Task Reconciliation_reports_a_counter_that_disagrees_and_adjustments_move_counter_and_ledger_together()
    {
        await _host.SubmitAsync(CapacityTestHost.Submit("rec", "r1", 2000));
        Assert.Equal(0, (await Reconcile()).DriftRows);

        // A counter that someone changed outside the ledger: reported against both the ledger and the reservations.
        await _host.Execute("UPDATE inventories SET held = held + 500 WHERE resource_class = 'VCPU' AND held > 0");
        var drifted = await Reconcile();
        Assert.Contains(drifted.Drift, d => d.Check is "inventory_vs_ledger" && d.Expected == 2000 && d.Actual == 2500);
        Assert.Contains(drifted.Drift, d => d.Check is "inventory_vs_reservations");
        await _host.Execute("UPDATE inventories SET held = held - 500 WHERE resource_class = 'VCPU' AND held > 0");
        Assert.Equal(0, (await Reconcile()).DriftRows);

        // An operator's correction is one ADJUST row and moves the counter by the same delta, so the
        // ledger still explains it; the reservations no longer do, which is what the report says.
        var pool = (int)await _host.Scalar("SELECT pool_id FROM inventories WHERE resource_class = 'VCPU' AND held > 0");
        await UseCase(sp => sp.GetRequiredService<AdjustLedgerUseCase>().ExecuteAsync(new AdjustLedgerInput(
            Admin, "adj", pool, null, null, ResourceClass.Vcpu, 500, 0, "capacity found offline"), default));

        Assert.Equal(1, await _host.Scalar("SELECT count(*) FROM ledger_entries WHERE entry_type = 'ADJUST' AND delta_held = 500"));
        var report = await Reconcile();
        Assert.DoesNotContain(report.Drift, d => d.Check is "inventory_vs_ledger");
        Assert.Contains(report.Drift, d => d.Check is "inventory_vs_reservations" && d.Expected == 2000 && d.Actual == 2500);

        // A customer may not adjust the ledger, and a reason is required.
        await Assert.ThrowsAsync<ActorNotAllowedException>(() => UseCase(sp => sp.GetRequiredService<AdjustLedgerUseCase>().ExecuteAsync(
            new AdjustLedgerInput(CapacityTestHost.Customer("x"), "adj3", pool, null, null, ResourceClass.Vcpu, 1, 0, "r"), default)));
        await Assert.ThrowsAsync<UnprocessableException>(() => UseCase(sp => sp.GetRequiredService<AdjustLedgerUseCase>().ExecuteAsync(
            new AdjustLedgerInput(Admin, "adj4", pool, null, null, ResourceClass.Vcpu, 1, 0, " "), default)));

        // Leave the database as the invariants expect.
        await _host.Execute($"UPDATE inventories SET held = held - 500 WHERE pool_id = {pool} AND resource_class = 'VCPU'");
        await _host.Execute("DELETE FROM ledger_entries WHERE entry_type = 'ADJUST'");
    }

    [Fact]
    public async Task Cancel_open_cancels_holds_and_waits_but_not_confirmed_allocations()
    {
        await _host.ShrinkVcpu("ir-thr-1", 3);
        var confirmed = (await _host.SubmitAsync(CapacityTestHost.Submit("sus", "s1"))).Reservation.Id;
        await _host.ConfirmAsync(confirmed, "sus", "sc");
        await _host.SubmitAsync(CapacityTestHost.Submit("sus", "s2"));
        await _host.SubmitAsync(CapacityTestHost.Submit("sus", "s3"));
        await _host.SubmitAsync(CapacityTestHost.Submit("sus", "s4", onShortage: ShortagePolicy.Wait));

        var result = await UseCase(sp => sp.GetRequiredService<CancelOpenReservationsUseCase>()
            .ExecuteAsync(new CancelOpenReservationsInput(Admin, "sus"), default));

        Assert.Equal(3, result.Cancelled);
        Assert.Equal("CONFIRMED", await Text($"SELECT stored_status FROM reservations WHERE id = '{confirmed}'"));
        Assert.Equal(1000, await _host.Scalar("SELECT used FROM inventories WHERE resource_class = 'VCPU' AND total = 3000"));
        Assert.Equal(0, await _host.Scalar("SELECT held FROM inventories WHERE resource_class = 'VCPU' AND total = 3000"));
    }

    // ---- helpers ----

    private Task<GetCapacityLevelsOutput> Levels(ActorKind kind, bool detail = false) =>
        UseCase(sp => sp.GetRequiredService<GetCapacityLevelsUseCase>().ExecuteAsync(new GetCapacityLevelsInput(kind, null, null, detail), default));

    private static void LevelOf(GetCapacityLevelsOutput output, string product, string region, CapacityLevel expected) =>
        Assert.Equal(expected, output.Items.Single(i => i.Product == product && i.Region == region).Level);

    private Task<ChangePoolStateOutput> PoolEvent(int pool, string stateEvent) =>
        UseCase(sp => sp.GetRequiredService<ChangePoolStateUseCase>().ExecuteAsync(
            new ChangePoolStateInput(Admin, Guid.NewGuid().ToString("N"), pool, stateEvent), default));

    private Task<Application.Repositories.Readonly.ReconciliationReport> Reconcile() =>
        UseCase(sp => sp.GetRequiredService<GetReconciliationUseCase>().ExecuteAsync(new GetReconciliationInput(ActorKind.Admin, true), default));

    private async Task<string> Text(string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(_host.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
