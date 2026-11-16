using Arvan.Capacity.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// One PostgreSQL container for the whole run, holding a migrated and seeded template database.
/// Each test clones it, which takes milliseconds, so every test starts from the same demo catalog
/// and none can see another's rows.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().WithImage("postgres:17").Build();

    /// <summary>
    /// The connection string of the container's maintenance database.
    /// </summary>
    public string AdminConnectionString => _container.GetConnectionString();

    /// <inheritdoc cref="IAsyncLifetime.InitializeAsync"/>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var template = new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = "capacity_template" }.ConnectionString;
        DatabaseMigrator.Migrate(template, seedDemoData: true, NullLogger.Instance);
        NpgsqlConnection.ClearAllPools();
    }

    /// <inheritdoc cref="IAsyncLifetime.DisposeAsync"/>
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Clones the template into a fresh database and returns its connection string.
    /// </summary>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"t_{Guid.NewGuid():N}";

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {name} TEMPLATE capacity_template", connection);
        await command.ExecuteNonQueryAsync();

        return new NpgsqlConnectionStringBuilder(AdminConnectionString) { Database = name, MaxPoolSize = 200 }.ConnectionString;
    }
}

/// <summary>
/// Shares <see cref="PostgresFixture"/> between the integration test classes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    /// <summary>
    /// The collection name.
    /// </summary>
    public const string Name = "postgres";
}
