using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Logging;

namespace Arvan.Capacity.Infrastructure.Persistence;

/// <summary>
/// Applies the embedded SQL scripts in order, each once, journaled in <c>schema_versions</c>.
/// </summary>
public static class DatabaseMigrator
{
    /// <summary>
    /// Creates the database if it is missing and applies pending migrations, then the demo catalog
    /// when asked and the database has not had it yet.
    /// </summary>
    /// <param name="connectionString">The connection string of the database to migrate.</param>
    /// <param name="seedDemoData">Whether to load the demo catalog.</param>
    /// <param name="logger">Where progress is reported.</param>
    /// <exception cref="InvalidOperationException">Thrown when a script fails.</exception>
    public static void Migrate(string connectionString, bool seedDemoData, ILogger logger)
    {
        EnsureDatabase.For.PostgresqlDatabase(connectionString);

        Run(connectionString, ".Migrations.", "schema_versions", logger);

        if (seedDemoData)
            Run(connectionString, ".Seeds.", "seed_versions", logger);
    }

    private static void Run(string connectionString, string scriptFolder, string journalTable, ILogger logger)
    {
        var result = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                typeof(DatabaseMigrator).Assembly,
                name => name.Contains(scriptFolder, StringComparison.Ordinal))
            .JournalToPostgresqlTable("public", journalTable)
            .LogToNowhere()
            .Build()
            .PerformUpgrade();

        if (!result.Successful)
            throw new InvalidOperationException($"Database script failed: {result.ErrorScript?.Name}", result.Error);

        logger.LogInformation(
            "Applied {Count} script(s) from {Folder}",
            result.Scripts.Count(),
            scriptFolder.Trim('.'));
    }

    /// <summary>
    /// Whether the database is reachable and every embedded migration has been applied: what
    /// <c>/health/ready</c> reports.
    /// </summary>
    /// <param name="dataSource">The data source to check.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    public static async Task<bool> IsReadyAsync(Npgsql.NpgsqlDataSource dataSource, CancellationToken ct)
    {
        try
        {
            var expected = typeof(DatabaseMigrator).Assembly.GetManifestResourceNames()
                .Count(n => n.Contains(".Migrations.", StringComparison.Ordinal));

            await using var command = dataSource.CreateCommand("SELECT count(*) FROM schema_versions");
            return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) >= expected;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
