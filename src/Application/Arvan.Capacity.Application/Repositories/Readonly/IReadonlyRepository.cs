namespace Arvan.Capacity.Application.Repositories.Readonly;

/// <summary>
/// Marks a read-side port: bound to its own connection, outside any transaction, returning
/// projections and never entities.
/// </summary>
/// <typeparam name="TAggregate">The aggregate whose projections this repository serves.</typeparam>
public interface IReadonlyRepository<TAggregate> where TAggregate : class;
