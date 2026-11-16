using Arvan.Capacity.Application;
using Arvan.Capacity.Application.UseCases.Reservations.CancelReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ConfirmReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ExpireHolds;
using Arvan.Capacity.Application.UseCases.Reservations.ExtendReservation;
using Arvan.Capacity.Application.UseCases.Reservations.ReleaseReservation;
using Arvan.Capacity.Application.UseCases.Reservations.SubmitReservation;
using Arvan.Capacity.Domain.Common;
using Arvan.Capacity.Domain.Enums;
using Arvan.Capacity.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// The application wired against one cloned database, with helpers to drive the UseCases the way
/// a controller does, and to read and bend the database the way an outage or an operator would.
/// </summary>
public sealed class CapacityTestHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    /// <summary>
    /// The connection string of this host's database.
    /// </summary>
    public string ConnectionString { get; }

    private CapacityTestHost(string connectionString)
    {
        ConnectionString = connectionString;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Database"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddCapacityApplication();
        services.AddCapacityPersistence(configuration);
        _services = services.BuildServiceProvider();
    }

    /// <summary>
    /// Clones the fixture's template into a new host.
    /// </summary>
    public static async Task<CapacityTestHost> CreateAsync(PostgresFixture fixture) =>
        new(await fixture.CreateDatabaseAsync());

    /// <summary>
    /// Runs <paramref name="action"/> in a fresh scope, as one request would.
    /// </summary>
    public async Task<T> Run<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>
    /// A customer actor.
    /// </summary>
    public static Actor Customer(string id) => new(ActorKind.Customer, id);

    /// <summary>
    /// A submit of vCPU in a zone of ir-thr.
    /// </summary>
    public static SubmitReservationInput Submit(
        string customer, string key, long vcpu = 1000, string? zone = "ir-thr-1", string product = "vm-standard",
        ShortagePolicy onShortage = ShortagePolicy.Reject, string? clientReference = null) =>
        new(customer, Customer(customer), key, product, "ir-thr", zone,
            [new SubmitReservationLineInput(ResourceClass.Vcpu, vcpu)], onShortage, clientReference);

    /// <summary>
    /// Submits.
    /// </summary>
    public Task<SubmitReservationOutput> SubmitAsync(SubmitReservationInput input, CancellationToken ct = default) =>
        Run(sp => sp.GetRequiredService<SubmitReservationUseCase>().ExecuteAsync(input, ct));

    /// <summary>
    /// Confirms.
    /// </summary>
    public Task<ConfirmReservationOutput> ConfirmAsync(Guid id, string customer, string key) =>
        Run(sp => sp.GetRequiredService<ConfirmReservationUseCase>().ExecuteAsync(
            new ConfirmReservationInput(id, customer, Customer(customer), key), default));

    /// <summary>
    /// Cancels.
    /// </summary>
    public Task<CancelReservationOutput> CancelAsync(Guid id, string customer, string key) =>
        Run(sp => sp.GetRequiredService<CancelReservationUseCase>().ExecuteAsync(
            new CancelReservationInput(id, customer, Customer(customer), key), default));

    /// <summary>
    /// Extends.
    /// </summary>
    public Task<ExtendReservationOutput> ExtendAsync(Guid id, string customer, string key) =>
        Run(sp => sp.GetRequiredService<ExtendReservationUseCase>().ExecuteAsync(
            new ExtendReservationInput(id, customer, Customer(customer), key), default));

    /// <summary>
    /// Releases, as a service.
    /// </summary>
    public Task<ReleaseReservationOutput> ReleaseAsync(Guid id, string customer, string key) =>
        Run(sp => sp.GetRequiredService<ReleaseReservationUseCase>().ExecuteAsync(
            new ReleaseReservationInput(id, customer, new Actor(ActorKind.Service, "vm-service"), key, ReleaseReason.ServiceDeleted), default));

    /// <summary>
    /// One sweeper pass.
    /// </summary>
    public Task<ExpireHoldsOutput> SweepAsync() =>
        Run(sp => sp.GetRequiredService<ExpireHoldsUseCase>().ExecuteAsync(new ExpireHoldsInput(200), default));

    /// <summary>
    /// Reads a server setting as a connection from the application's own pool sees it.
    /// </summary>
    public async Task<string> ShowAsync(string setting)
    {
        await using var scope = _services.CreateAsyncScope();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
        await using var command = dataSource.CreateCommand($"SHOW {setting}");
        return (string)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Runs SQL.
    /// </summary>
    public async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Reads one number.
    /// </summary>
    public async Task<long> Scalar(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    /// <summary>
    /// The id of the demo zone pool that holds a class, so a test can shrink it.
    /// </summary>
    public Task<long> ZonePool(string zone) =>
        Scalar($"SELECT id FROM pools WHERE zone = '{zone}' AND host_ref IS NULL");

    /// <summary>
    /// Shrinks the vCPU of a zone to <paramref name="units"/> units of 1000, so contention is total.
    /// </summary>
    public Task ShrinkVcpu(string zone, int units) =>
        Execute($"""
            UPDATE inventories SET total = {units * 1000}, reserved = 0, allocation_ratio = 1, min_unit = 1000, step_size = 1000, max_unit = {units * 1000}
            WHERE resource_class = 'VCPU' AND pool_id = (SELECT id FROM pools WHERE zone = '{zone}' AND host_ref IS NULL)
            """);

    /// <summary>
    /// Makes a reservation's lease end in the past, as time passing would.
    /// </summary>
    public Task Age(Guid id) =>
        Execute($"UPDATE reservations SET expires_at = now() - interval '1 second' WHERE id = '{id}' AND stored_status = 'HELD'");

    /// <summary>
    /// The invariants every test must leave true: nothing oversold, counters equal their ledger
    /// sums, counters equal what the reservations say, and quotas equal their ledger sums.
    /// </summary>
    public async Task AssertInvariantsAsync()
    {
        Assert.Equal(0, await Scalar(
            "SELECT count(*) FROM inventories WHERE held < 0 OR used < 0 OR held + used > floor((total - reserved) * allocation_ratio)"));

        Assert.Equal(0, await Scalar(
            """
            SELECT count(*) FROM (
              SELECT i.pool_id FROM inventories i LEFT JOIN ledger_entries l USING (pool_id, resource_class)
              GROUP BY i.pool_id, i.resource_class, i.held, i.used
              HAVING i.held <> COALESCE(SUM(l.delta_held), 0) OR i.used <> COALESCE(SUM(l.delta_used), 0)) d
            """));

        Assert.Equal(0, await Scalar(
            """
            SELECT count(*) FROM (
              SELECT q.customer_id FROM quotas q
              LEFT JOIN ledger_entries l ON l.customer_id = q.customer_id AND l.resource_class = q.resource_class AND l.region = q.region
              GROUP BY q.customer_id, q.resource_class, q.region, q.held, q.used
              HAVING q.held <> COALESCE(SUM(l.delta_held), 0) OR q.used <> COALESCE(SUM(l.delta_used), 0)) d
            """));

        Assert.Equal(0, await Scalar(
            """
            SELECT count(*) FROM inventories i
            WHERE i.held <> COALESCE((SELECT sum(l.quantity) FROM reservation_lines l JOIN reservations r ON r.id = l.reservation_id
                                      WHERE l.pool_id = i.pool_id AND l.resource_class = i.resource_class AND r.stored_status = 'HELD'), 0)
               OR i.used <> COALESCE((SELECT sum(l.quantity) FROM reservation_lines l JOIN reservations r ON r.id = l.reservation_id
                                      WHERE l.pool_id = i.pool_id AND l.resource_class = i.resource_class
                                        AND r.stored_status = 'CONFIRMED' AND r.allocation_state = 'ACTIVE'), 0)
            """));
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
    }
}
