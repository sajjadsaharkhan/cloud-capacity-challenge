namespace Arvan.Capacity.Application.Abstractions;

/// <summary>
/// The only clock a decision may read: the database's. Application code never reads the host's
/// clock, so a skewed instance cannot disagree with the rows it is judging.
/// </summary>
public interface IClock
{
    /// <summary>
    /// The database time. Inside a transaction it is that transaction's start time, fixed for the
    /// transaction's life; outside one it is the current time.
    /// </summary>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task<DateTimeOffset> GetNowAsync(CancellationToken ct);
}
