namespace Arvan.Capacity.Application.Repositories;

/// <summary>
/// The generic write-side base port. Small on purpose: aggregate-specific methods carry business
/// meaning and do not belong on a generic base. No method returns <c>IQueryable</c> or accepts an
/// expression, so nothing about the persistence technology leaks into Application.
/// </summary>
/// <typeparam name="TAggregate">The aggregate root this repository persists.</typeparam>
public interface IRepository<TAggregate> where TAggregate : class
{
    /// <summary>
    /// Persists a new aggregate inside the open transaction. Asynchronous, unlike a change-tracking
    /// repository's <c>Add</c>: the statement runs now.
    /// </summary>
    /// <param name="aggregate">The aggregate to persist.</param>
    /// <param name="ct">A token to observe while waiting for the task to complete.</param>
    Task AddAsync(TAggregate aggregate, CancellationToken ct);
}
