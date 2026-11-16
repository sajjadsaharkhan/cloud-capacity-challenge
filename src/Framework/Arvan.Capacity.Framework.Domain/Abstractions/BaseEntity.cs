namespace Arvan.Capacity.Framework.Domain.Abstractions;

/// <summary>
/// All entities base class
/// </summary>
/// <typeparam name="TKey">Type of primary key</typeparam>
public abstract class BaseEntity<TKey> : IEntity<TKey>
{
    /// <summary>
    /// primary key
    /// </summary>
    public TKey Id { get; protected set; } = default!;
}