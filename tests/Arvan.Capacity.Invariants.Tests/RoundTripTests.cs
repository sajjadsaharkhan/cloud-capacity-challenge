using System.Diagnostics.Metrics;
using Arvan.Capacity.Application.Telemetry;
using Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;
using Arvan.Capacity.Domain.Enums;
using Xunit.Abstractions;

namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// A hold's latency is mostly made of network round trips to PostgreSQL, so their number is a
/// contract: it must stay small and must not grow with the number of lines.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RoundTripTests(PostgresFixture fixture, ITestOutputHelper output) : IAsyncLifetime
{
    // Before the batching work a three-line hold took 28 round trips.
    private const int HoldBudget = 10;
    private CapacityTestHost _host = default!;

    public async Task InitializeAsync() => _host = await CapacityTestHost.CreateAsync(fixture);

    public async Task DisposeAsync()
    {
        await _host.AssertInvariantsAsync();
        await _host.DisposeAsync();
    }

    /// <summary>
    /// The round trips of every transaction the call ran, as the service itself reports them.
    /// </summary>
    private static async Task<List<int>> Measure(Func<Task> call)
    {
        var seen = new List<int>();
        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == CapacityTelemetry.Name && instrument.Name == "capacity_transaction_round_trips")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((_, value, _, _) => { lock (seen) seen.Add(value); });
        listener.Start();

        await call();
        return seen;
    }

    private static SubmitReservationInput Lines(string customer, string key, params ResourceClass[] classes) =>
        new(customer, CapacityTestHost.Customer(customer), key, "vm-standard", "ir-thr", "ir-thr-1",
            [.. classes.Select(c => new SubmitReservationLineInput(c, c switch
            {
                ResourceClass.Vcpu => 500, ResourceClass.RamMb => 512, ResourceClass.DiskGb => 10, _ => 1,
            }))],
            ShortagePolicy.Reject, null);

    [Fact]
    public async Task A_hold_takes_a_handful_of_round_trips_whatever_the_number_of_lines()
    {
        // The first call fills the catalog cache.
        await _host.SubmitAsync(Lines("warm", "w", ResourceClass.Vcpu));

        var one = (await Measure(() => _host.SubmitAsync(Lines("a", "a", ResourceClass.Vcpu)))).Single();
        var four = (await Measure(() => _host.SubmitAsync(
            Lines("b", "b", ResourceClass.Vcpu, ResourceClass.RamMb, ResourceClass.DiskGb, ResourceClass.Ipv4)))).Single();

        output.WriteLine($"hold, 1 line: {one} round trips; hold, 4 lines: {four} round trips");

        Assert.Equal(one, four);
        Assert.InRange(four, 1, HoldBudget);
    }

    [Fact]
    public async Task A_refusal_on_a_full_pool_is_cheap_and_never_attempts_the_hold()
    {
        await _host.ShrinkVcpu("ir-thr-1", 1);
        await _host.SubmitAsync(CapacityTestHost.Submit("full", "f1", 1000));   // takes the only unit

        var rejected = (await Measure(() => _host.SubmitAsync(CapacityTestHost.Submit("other", "f2", 1000)))).Single();
        output.WriteLine($"refusal on a full pool: {rejected} round trips");

        Assert.InRange(rejected, 1, HoldBudget + 2);

        // The refusal never touched the inventory row: no ledger row, no savepoint dance.
        Assert.Equal(1, await _host.Scalar("SELECT count(*) FROM ledger_entries WHERE entry_type = 'HOLD'"));
    }

    [Fact]
    public async Task Confirm_cancel_and_the_sweeper_write_in_one_batch()
    {
        var id = (await _host.SubmitAsync(CapacityTestHost.Submit("c", "c1"))).Reservation.Id;

        var confirm = (await Measure(() => _host.ConfirmAsync(id, "c", "c2"))).Single();
        output.WriteLine($"confirm: {confirm} round trips");
        Assert.InRange(confirm, 1, 7);

        var held = (await _host.SubmitAsync(CapacityTestHost.Submit("c", "c3"))).Reservation.Id;
        await _host.Age(held);
        var sweep = (await Measure(() => _host.SweepAsync())).Single();
        output.WriteLine($"sweeper pass, 1 hold: {sweep} round trips");
        Assert.InRange(sweep, 1, 7);
    }
}
