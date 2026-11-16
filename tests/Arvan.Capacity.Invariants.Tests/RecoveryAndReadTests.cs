using Arvan.Capacity.Application.Common.Exceptions;
using Arvan.Capacity.Application.UseCases.Reservations.GetReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ListReservations;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// Outages, stopped sweepers, other customers and repeated or late calls.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RecoveryAndReadTests(PostgresFixture fixture) : IAsyncLifetime
{
    private CapacityTestHost _host = default!;

    public async Task InitializeAsync() => _host = await CapacityTestHost.CreateAsync(fixture);

    public async Task DisposeAsync()
    {
        await _host.AssertInvariantsAsync();
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task Calls_aborted_mid_flight_and_retried_with_their_keys_leave_no_duplicate_or_partial_reservation()
    {
        await _host.ShrinkVcpu("ir-thr-1", 40);
        var random = new Random(11);

        // Each call is aborted at a random moment, as a killed process would be; some commit before
        // the abort, some roll back. The client does not know which.
        await Task.WhenAll(Enumerable.Range(0, 200).Select(async i =>
        {
            using var abort = new CancellationTokenSource(TimeSpan.FromMilliseconds(random.Next(1, 40)));
            try { await _host.SubmitAsync(CapacityTestHost.Submit($"k{i}", $"key{i}"), abort.Token); }
            catch (Exception) { /* cancelled, or shed */ }
        }));

        // The client retries every call with the same key.
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 200).Select(i =>
            ConcurrencyTests.WithRetry(() => _host.SubmitAsync(CapacityTestHost.Submit($"k{i}", $"key{i}")))));

        Assert.Equal(200, await _host.Scalar("SELECT count(*) FROM reservations"));
        Assert.Equal(200, await _host.Scalar("SELECT count(DISTINCT customer_id) FROM reservations"));
        Assert.Equal(40, outcomes.Count(o => o.Reservation.Status is ReservationStatus.Held));
        Assert.Equal(0, await _host.Scalar("SELECT count(*) FROM reservations r WHERE NOT EXISTS (SELECT 1 FROM reservation_lines l WHERE l.reservation_id = r.id)"));
    }

    [Fact]
    public async Task With_the_sweeper_stopped_reads_show_expired_and_new_holds_use_the_expired_capacity()
    {
        await _host.ShrinkVcpu("ir-thr-1", 5);
        var holds = new List<Guid>();
        for (var i = 0; i < 5; i++)
            holds.Add((await _host.SubmitAsync(CapacityTestHost.Submit($"s{i}", $"s{i}"))).Reservation.Id);

        Assert.Equal(ReservationStatus.Rejected, (await _host.SubmitAsync(CapacityTestHost.Submit("late", "full"))).Reservation.Status);

        foreach (var id in holds)
            await _host.Age(id);

        // No sweeper has run: stored state is still HELD, but a reader is told the truth.
        var read = await _host.Run(sp => sp.GetRequiredService<GetReservationUseCase>()
            .ExecuteAsync(new GetReservationInput(holds[0], "s0"), default));
        Assert.Equal(ReservationStatus.Expired, read.Status);
        Assert.False(read.Settled);
        Assert.Equal(ReasonCode.HoldExpired, read.ReasonCode);
        Assert.Equal(5, await _host.Scalar("SELECT count(*) FROM reservations WHERE stored_status = 'HELD'"));

        // And a new hold succeeds, the hold path finalizing what it needs.
        var fresh = await _host.SubmitAsync(CapacityTestHost.Submit("fresh", "fresh-key", vcpu: 3000));
        Assert.Equal(ReservationStatus.Held, fresh.Reservation.Status);
    }

    [Fact]
    public async Task Another_customer_gets_not_found_for_every_operation()
    {
        var id = (await _host.SubmitAsync(CapacityTestHost.Submit("alice", "a1"))).Reservation.Id;

        await Assert.ThrowsAsync<EntityNotFoundException<Domain.Entities.Reservations.Reservation>>(() =>
            _host.Run(sp => sp.GetRequiredService<GetReservationUseCase>().ExecuteAsync(new GetReservationInput(id, "bob"), default)));
        await Assert.ThrowsAsync<EntityNotFoundException<Domain.Entities.Reservations.Reservation>>(() => _host.ConfirmAsync(id, "bob", "x1"));
        await Assert.ThrowsAsync<EntityNotFoundException<Domain.Entities.Reservations.Reservation>>(() => _host.CancelAsync(id, "bob", "x2"));
        await Assert.ThrowsAsync<EntityNotFoundException<Domain.Entities.Reservations.Reservation>>(() => _host.ExtendAsync(id, "bob", "x3"));

        var theirs = await _host.Run(sp => sp.GetRequiredService<ListReservationsUseCase>()
            .ExecuteAsync(new ListReservationsInput("bob", [], null, null, 1, 20), default));
        Assert.Empty(theirs.Items);
    }

    [Fact]
    public async Task Release_is_once_only_and_a_late_confirm_is_refused()
    {
        var id = (await _host.SubmitAsync(CapacityTestHost.Submit("rel", "r1"))).Reservation.Id;

        await Assert.ThrowsAnyAsync<DomainException>(() => _host.ReleaseAsync(id, "rel", "early"));   // not confirmed
        await _host.ConfirmAsync(id, "rel", "c1");
        await _host.ReleaseAsync(id, "rel", "rel1");
        await _host.ReleaseAsync(id, "rel", "rel2");                                                  // new key, already released: 200

        Assert.Equal(1, await _host.Scalar($"SELECT count(*) FROM ledger_entries WHERE reservation_id = '{id}' AND entry_type = 'RELEASE'"));
        Assert.Equal(0, await _host.Scalar("SELECT sum(used) FROM inventories"));

        var late = (await _host.SubmitAsync(CapacityTestHost.Submit("rel", "r2"))).Reservation.Id;
        await _host.Age(late);
        await Assert.ThrowsAsync<Domain.Exceptions.Reservations.ReservationExpiredException>(() => _host.ConfirmAsync(late, "rel", "late"));
    }
}
