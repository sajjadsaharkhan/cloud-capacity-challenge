using Arvan.Capacity.Domain.Entities.Capacity;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Domain.Exceptions;

namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// SQL agrees with the domain: random sequences of submit, confirm, cancel, extend, expire and
/// release run against the database, and after every step an in-memory model built from the
/// domain's own rules predicts the counters and the statuses.
/// </summary>
/// <remarks>
/// Seeded <see cref="Random"/> rather than FsCheck: a failing seed replays exactly, and the model
/// below is the shrunk reproduction.
/// </remarks>
[Collection(PostgresCollection.Name)]
public sealed class ModelAgreementTests(PostgresFixture fixture)
{
    private sealed record ModelReservation(Guid Id, string Customer, long Quantity, string Status, bool Extended);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task The_database_matches_a_model_of_the_domain_rules_after_every_step(int seed)
    {
        await using var host = await CapacityTestHost.CreateAsync(fixture);
        const int units = 6;
        await host.ShrinkVcpu("ir-thr-1", units);

        var random = new Random(seed);
        var model = new List<ModelReservation>();
        var customers = Enumerable.Range(0, 4).Select(i => $"p{seed}-{i}").ToArray();

        for (var step = 0; step < 80; step++)
        {
            var customer = customers[random.Next(customers.Length)];
            var held = model.Where(m => m.Status is "HELD").ToList();
            var confirmed = model.Where(m => m.Status is "CONFIRMED").ToList();

            switch (random.Next(7))
            {
                case 0 or 1: // submit
                {
                    var quantity = (random.Next(2) + 1) * 1000L;
                    var free = units * 1000L - model.Where(m => m.Status is "HELD" or "CONFIRMED").Sum(m => m.Quantity);
                    var openHolds = model.Count(m => m.Customer == customer && m.Status is "HELD");
                    if (openHolds >= 20) break;

                    var outcome = await host.SubmitAsync(CapacityTestHost.Submit(customer, $"s{step}", quantity));
                    var expected = quantity <= free ? ReservationStatus.Held : ReservationStatus.Rejected;
                    Assert.Equal(expected, outcome.Reservation.Status);

                    // The domain's own formula decides: sellable = floor((total - reserved) * ratio).
                    Assert.Equal(units * 1000L, InventorySnapshot.ComputeSellable(units * 1000L, 0, 1m));

                    if (expected is ReservationStatus.Held)
                        model.Add(new ModelReservation(outcome.Reservation.Id, customer, quantity, "HELD", false));
                    break;
                }
                case 2 when held.Count > 0: // confirm
                {
                    var target = held[random.Next(held.Count)];
                    await host.ConfirmAsync(target.Id, target.Customer, $"c{step}");
                    Replace(model, target, target with { Status = "CONFIRMED" });
                    break;
                }
                case 3 when held.Count > 0: // cancel
                {
                    var target = held[random.Next(held.Count)];
                    await host.CancelAsync(target.Id, target.Customer, $"x{step}");
                    Replace(model, target, target with { Status = "CANCELLED" });
                    break;
                }
                case 4 when held.Count > 0: // extend
                {
                    var target = held[random.Next(held.Count)];
                    if (target.Extended)
                    {
                        await Assert.ThrowsAnyAsync<DomainException>(() => host.ExtendAsync(target.Id, target.Customer, $"e{step}"));
                    }
                    else
                    {
                        await host.ExtendAsync(target.Id, target.Customer, $"e{step}");
                        Replace(model, target, target with { Extended = true });
                    }
                    break;
                }
                case 5 when held.Count > 0: // time passes, then the sweeper
                {
                    var target = held[random.Next(held.Count)];
                    await host.Age(target.Id);
                    await host.SweepAsync();
                    Replace(model, target, target with { Status = "EXPIRED" });
                    break;
                }
                case 6 when confirmed.Count > 0: // release by the product service
                {
                    var target = confirmed[random.Next(confirmed.Count)];
                    await host.ReleaseAsync(target.Id, target.Customer, $"r{step}");
                    Replace(model, target, target with { Status = "RELEASED" });   // CONFIRMED, allocation RELEASED
                    break;
                }
            }

            await AssertAgrees(host, model);
        }
    }

    private static void Replace(List<ModelReservation> model, ModelReservation old, ModelReservation next) =>
        model[model.IndexOf(old)] = next;

    private static async Task AssertAgrees(CapacityTestHost host, List<ModelReservation> model)
    {
        foreach (var m in model)
        {
            var status = await host.Scalar(
                $"SELECT CASE stored_status WHEN 'HELD' THEN 1 WHEN 'CONFIRMED' THEN 2 WHEN 'CANCELLED' THEN 3 WHEN 'EXPIRED' THEN 4 ELSE 0 END FROM reservations WHERE id = '{m.Id}'");
            Assert.Equal(m.Status switch { "HELD" => 1, "CONFIRMED" or "RELEASED" => 2, "CANCELLED" => 3, _ => 4 }, status);
        }

        // Released allocations are CONFIRMED in the model too; the database knows the difference.
        var heldExpected = model.Where(m => m.Status is "HELD").Sum(m => m.Quantity);
        var usedExpected = await host.Scalar(
            "SELECT COALESCE(sum(l.quantity), 0) FROM reservation_lines l JOIN reservations r ON r.id = l.reservation_id WHERE r.stored_status = 'CONFIRMED' AND r.allocation_state = 'ACTIVE'");

        Assert.Equal(heldExpected, await host.Scalar("SELECT held FROM inventories WHERE resource_class = 'VCPU' AND total = 6000"));
        Assert.Equal(usedExpected, await host.Scalar("SELECT used FROM inventories WHERE resource_class = 'VCPU' AND total = 6000"));

        await host.AssertInvariantsAsync();
    }
}
