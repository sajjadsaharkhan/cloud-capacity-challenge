namespace Arvan.Capacity.Application.Common.Exceptions;

/// <inheritdoc cref="NotFoundException"/>
/// <typeparam name="TEntity">The entity type that could not be found; its name is used in the message and code.</typeparam>
public class EntityNotFoundException<TEntity>(object id)
    : NotFoundException($"{EntityName.ToLowerInvariant()}.not_found", $"There is no any {EntityName} with ID '{id}'")
{
    /// <summary>
    /// The name of <typeparamref name="TEntity"/>, used to build the failure's message and code.
    /// </summary>
    public static string EntityName { get; } = typeof(TEntity).Name;
}
