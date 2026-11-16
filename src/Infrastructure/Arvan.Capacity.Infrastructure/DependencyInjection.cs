using Arvan.Capacity.Application.Abstractions;
using Arvan.Capacity.Application.Clients;
using Arvan.Capacity.Application.Repositories;
using Arvan.Capacity.Infrastructure.Messaging;
using Arvan.Capacity.Infrastructure.Telemetry;
using Arvan.Capacity.Application.Repositories.Readonly;
using Arvan.Capacity.Infrastructure.Persistence;
using Arvan.Capacity.Infrastructure.Repositories;
using Arvan.Capacity.Infrastructure.Repositories.Readonly;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Arvan.Capacity.Infrastructure;

/// <summary>
/// Wires the Capacity persistence layer into the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers the PostgreSQL data source, <see cref="ITransactionManager"/>, the database clock
    /// and every Repository and ReadonlyRepository implementation.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configuration">Supplies <c>ConnectionStrings:Database</c> and the <c>Database</c> section.</param>
    /// <exception cref="InvalidOperationException">Thrown when the connection string is missing.</exception>
    public static void AddCapacityPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        DapperTypeHandlers.Register();

        var connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is missing.");

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<CapacityOptions>(configuration.GetSection(CapacityOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddMemoryCache();
        var database = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

        // A factory, so the container owns the data source and disposes its connections with it.
        services.AddSingleton(_ => NpgsqlDataSource.Create(ConfigureConnection(connectionString, database)));

        services.AddScoped<PostgresSession>();
        services.AddScoped<ITransactionManager, TransactionManager>();
        services.AddScoped<IClock, DbClock>();

        services.AddScoped<IReservationRepository, ReservationRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICapacityLedgerRepository, CapacityLedgerRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddScoped<IPoolRepository, PoolRepository>();
        services.AddScoped<IQuotaRepository, QuotaRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        // The broker is optional: without a URI the relay logs events instead of publishing them.
        if (string.IsNullOrWhiteSpace(configuration[$"{RabbitMqOptions.SectionName}:Uri"]))
            services.AddSingleton<IEventPublisher, LoggingEventPublisher>();
        else
            services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

        // Readonly Repositories
        services.AddScoped<IReservationReadonlyRepository, ReservationReadonlyRepository>();
        services.AddScoped<ICapacityReadonlyRepository, CapacityReadonlyRepository>();
        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
    }

    /// <summary>
    /// Applies the connection settings that keep a hold's round trips few and cheap, unless the
    /// configured connection string already chose its own.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Statements are prepared automatically after two uses, so the server parses and plans each once per connection.</item>
    /// <item>A connection is not reset when it returns to the pool: nothing here keeps session state, and the reset costs a round trip per transaction.</item>
    /// <item>The pool is capped at 80 connections per process.</item>
    /// <item>The statement and lock timeouts are startup options, so a transaction starts with <c>BEGIN</c> and nothing else.</item>
    /// </list>
    /// </remarks>
    private static string ConfigureConnection(string connectionString, DatabaseOptions database)
    {
        // ShouldSerialize, not ContainsKey: the latter is true for every known keyword, set or not.
        var builder = new NpgsqlConnectionStringBuilder(connectionString);

        if (!builder.ShouldSerialize("Max Auto Prepare"))
            builder.MaxAutoPrepare = 64;

        if (!builder.ShouldSerialize("Auto Prepare Min Usages"))
            builder.AutoPrepareMinUsages = 2;

        if (!builder.ShouldSerialize("No Reset On Close"))
            builder.NoResetOnClose = true;

        // Bounded below the server's max_connections even with several processes, so overload queues in
        // the pool (and times out with a 503) instead of failing at the server with "too many clients".
        if (!builder.ShouldSerialize("Maximum Pool Size"))
            builder.MaxPoolSize = 80;

        if (!builder.ShouldSerialize("Options"))
            builder.Options = $"-c statement_timeout={(int)database.StatementTimeout.TotalMilliseconds} -c lock_timeout={(int)database.LockTimeout.TotalMilliseconds}";

        return builder.ConnectionString;
    }

    /// <summary>
    /// Registers the service that keeps the database-backed gauges (unsettled holds, waiting
    /// requests, free units, outbox backlog, ledger drift) fresh for the metrics endpoint.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public static void AddCapacityGauges(this IServiceCollection services) =>
        services.AddHostedService<GaugeRefresher>();
}
