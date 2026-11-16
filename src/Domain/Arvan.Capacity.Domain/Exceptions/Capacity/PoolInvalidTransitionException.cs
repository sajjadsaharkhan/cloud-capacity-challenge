using Arvan.Capacity.Domain.Enums;

namespace Arvan.Capacity.Domain.Exceptions.Capacity;

/// <summary>
/// Thrown when a pool state change is not legal from the pool's current state.
/// </summary>
public sealed class PoolInvalidTransitionException(int poolId, PoolAdminState state, string attempted)
    : DomainException(
        "pool.invalid_state_transition",
        $"Pool {poolId} is {state} and cannot {attempted}.");
