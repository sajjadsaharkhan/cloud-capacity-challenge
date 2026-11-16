namespace Arvan.Capacity.Invariants.Tests;

/// <summary>
/// The timeouts that turn overload into a fast 503 instead of an unbounded queue are connection
/// settings, so they are checked on a connection from the application's own pool.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConnectionSettingsTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Pooled_connections_carry_the_statement_and_lock_timeouts()
    {
        await using var host = await CapacityTestHost.CreateAsync(fixture);

        // Twice, so the second is a connection that has been returned to the pool and reused.
        for (var i = 0; i < 2; i++)
        {
            Assert.Equal("2s", await host.ShowAsync("statement_timeout"));
            Assert.Equal("500ms", await host.ShowAsync("lock_timeout"));
        }
    }
}
