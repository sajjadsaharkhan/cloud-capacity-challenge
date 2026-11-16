using Arvan.Capacity.Application.Abstractions;

namespace Arvan.Capacity.Infrastructure.Persistence;

/// <inheritdoc cref="IClock"/>
internal sealed class DbClock(PostgresSession session) : IClock
{
    /// <inheritdoc cref="IClock.GetNowAsync"/>
    public Task<DateTimeOffset> GetNowAsync(CancellationToken ct) => session.NowAsync(ct);
}
