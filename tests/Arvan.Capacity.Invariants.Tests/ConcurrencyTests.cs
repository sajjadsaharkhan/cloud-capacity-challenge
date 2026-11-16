using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;
using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// The invariant behind the whole service, run against a real PostgreSQL: under any concurrency,
/// held plus used never exceeds sellable, every counter equals the sum of its ledger rows, and each
/// reservation ends in exactly one final state.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConcurrencyTests(PostgresFixture fixture) : IAsyncLifetime
{
    private CapacityTestHost _host = default!;

    public async Task InitializeAsync() => _host = await CapacityTestHost.CreateAsync(fixture);

    public async Task DisposeAsync()
    {
        await _host.AssertInvariantsAsync();
        await _host.DisposeAsync();
    }

    /// <summary>
    /// A client retries a call the service shed with 503, with the same key, as the contract says.
    /// </summary>
    internal static async Task<T> WithRetry<T>(Func<Task<T>> call)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await call();
            }
            catch (ServiceUnavailableException) when (attempt < 400)
            {
                await Task.Delay(20);
            }
        }
    }

    [Fact]
    public async Task Thousand_parallel_submits_on_a_pool_of_hundred_hold_exactly_hundred()
    {
        await _host.ShrinkVcpu("ir-thr-1", 100);
        using var stop = new CancellationTokenSource();

        // Sampled while the load runs: the bound holds at every moment, not just at the end.
        var violations = 0L;
        var sampler = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                violations += await _host.Scalar(
                    "SELECT count(*) FROM inventories WHERE held + used > floor((total - reserved) * allocation_ratio)");
                await Task.Delay(25);
            }
        });

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 1000).Select(i =>
            WithRetry(() => _host.SubmitAsync(CapacityTestHost.Submit($"c{i}", $"k{i}")))));

        await stop.CancelAsync();
        await sampler;

        Assert.Equal(100, outcomes.Count(o => o.Reservation.Status is ReservationStatus.Held));
        Assert.Equal(900, outcomes.Count(o => o.Reservation.Status is ReservationStatus.Rejected));
        Assert.Equal(0, violations);
        Assert.Equal(100000, await _host.Scalar("SELECT held FROM inventories WHERE resource_class = 'VCPU' AND total = 100000"));
    }

    [Fact]
    public async Task Multi_line_requests_in_random_line_order_never_deadlock()
    {
        var random = new Random(7);
        var classes = new[]
        {
            new SubmitReservationLineInput(ResourceClass.Vcpu, 500),
            new SubmitReservationLineInput(ResourceClass.RamMb, 512),
            new SubmitReservationLineInput(ResourceClass.DiskGb, 10),
            new SubmitReservationLineInput(ResourceClass.Ipv4, 1),
        };

        var calls = Enumerable.Range(0, 300).Select(i =>
        {
            var lines = classes.OrderBy(_ => random.Next()).Take(2 + i % 3).ToList();
            var input = new SubmitReservationInput(
                $"m{i}", CapacityTestHost.Customer($"m{i}"), $"mk{i}", "vm-standard", "ir-thr", i % 2 == 0 ? "ir-thr-1" : null,
                lines, ShortagePolicy.Reject, null);

            return WithRetry(() => _host.SubmitAsync(input));
        });

        // A deadlock would surface as a PostgresException the retry does not cover.
        var outcomes = await Task.WhenAll(calls);
        Assert.All(outcomes, o => Assert.Equal(ReservationStatus.Held, o.Reservation.Status));
    }

    [Fact]
    public async Task Confirm_cancel_and_expiry_racing_on_one_hold_apply_exactly_one_outcome()
    {
        await _host.ShrinkVcpu("ir-thr-1", 10);
        var submitted = await _host.SubmitAsync(CapacityTestHost.Submit("racer", "r1"));
        var id = submitted.Reservation.Id;
        await _host.Execute($"UPDATE reservations SET expires_at = now() + interval '1 second' WHERE id = '{id}'");

        var calls = new List<Task>();
        for (var i = 0; i < 10; i++)
        {
            var n = i;
            calls.Add(Swallow(() => WithRetry(() => _host.ConfirmAsync(id, "racer", $"cf{n}"))));
            calls.Add(Swallow(() => WithRetry(() => _host.CancelAsync(id, "racer", $"cn{n}"))));
        }

        await Task.WhenAll(calls);
        await Task.Delay(1500);
        await _host.SweepAsync();

        var status = await _host.Scalar(
            $"SELECT CASE stored_status WHEN 'CONFIRMED' THEN 1 WHEN 'CANCELLED' THEN 2 WHEN 'EXPIRED' THEN 3 ELSE 0 END FROM reservations WHERE id = '{id}'");
        Assert.NotEqual(0, status);
        Assert.Equal(1, await _host.Scalar($"SELECT count(*) FROM reservation_events WHERE reservation_id = '{id}' AND event IN ('CONFIRM', 'CANCEL', 'EXPIRE')"));

        var held = await _host.Scalar("SELECT held FROM inventories WHERE resource_class = 'VCPU' AND total = 10000");
        var used = await _host.Scalar("SELECT used FROM inventories WHERE resource_class = 'VCPU' AND total = 10000");
        Assert.Equal(status is 1 ? (0L, 1000L) : (0L, 0L), (held, used));
    }

    [Fact]
    public async Task Same_key_in_parallel_creates_one_reservation()
    {
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            WithRetry(() => _host.SubmitAsync(CapacityTestHost.Submit("same", "one-key")))));

        Assert.Single(outcomes.Select(o => o.Reservation.Id).Distinct());
        Assert.Equal(1, await _host.Scalar("SELECT count(*) FROM reservations WHERE customer_id = 'same'"));
        Assert.Equal(19, outcomes.Count(o => o.Replayed));
    }

    [Fact]
    public async Task Confirm_cancel_and_extend_repeated_with_one_key_apply_once()
    {
        var a = (await _host.SubmitAsync(CapacityTestHost.Submit("idem", "a"))).Reservation.Id;
        var b = (await _host.SubmitAsync(CapacityTestHost.Submit("idem", "b"))).Reservation.Id;
        var c = (await _host.SubmitAsync(CapacityTestHost.Submit("idem", "c"))).Reservation.Id;

        await Task.WhenAll(Enumerable.Range(0, 10).SelectMany(_ => new Task[]
        {
            WithRetry(() => _host.ConfirmAsync(a, "idem", "conf")),
            WithRetry(() => _host.CancelAsync(b, "idem", "canc")),
            WithRetry(() => _host.ExtendAsync(c, "idem", "ext")),
        }));

        Assert.Equal(1, await _host.Scalar($"SELECT count(*) FROM reservation_events WHERE reservation_id = '{a}' AND event = 'CONFIRM'"));
        Assert.Equal(1, await _host.Scalar($"SELECT count(*) FROM reservation_events WHERE reservation_id = '{b}' AND event = 'CANCEL'"));
        Assert.Equal(1, await _host.Scalar($"SELECT count(*) FROM reservation_events WHERE reservation_id = '{c}' AND event = 'EXTEND'"));
    }

    private static async Task Swallow(Func<Task> call)
    {
        try { await call(); } catch (Exception) { /* a losing racer is refused; only the end state matters */ }
    }
}
