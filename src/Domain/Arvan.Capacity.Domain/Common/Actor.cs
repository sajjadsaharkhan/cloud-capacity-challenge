using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Common;

/// <summary>
/// Who caused a state change, as recorded on every event row.
/// </summary>
/// <param name="Kind">The kind of caller.</param>
/// <param name="Id">The caller's identifier: a user, a service or an operator.</param>
public sealed record Actor(ActorKind Kind, string Id)
{
    /// <summary>
    /// The background expiry process.
    /// </summary>
    public static Actor Sweeper { get; } = new(ActorKind.Sweeper, "sweeper");
}
