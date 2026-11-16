# ADR-0008: Consistency over availability; fail closed; one cell per region

- Status: Accepted
- Date: 2026-11-16
- Related: [PRD A-5, A-15, A-19](../prd/PRD.md#7-assumptions), [ADR-0018](0018-postgresql-high-availability.md), edge cases [F-1 … F-11](../edge-cases/edge-cases.md#5-failure-and-recovery)

## Context

Part of the infrastructure may be unavailable for a while. Overselling a scarce resource means a service that fails after the customer has committed. The brief also asks that the design show how the system develops in a real environment, while saying the delivered implementation need not reproduce real multi-region infrastructure.

## Decision

- Writes go only to the primary, which has one synchronous standby. During failover (30–60 s) writes return `503` with `Retry-After`; idempotency makes the retry safe.
- Reads continue from cache with `stale: true`.
- Never fail open, for any product.
- Every transaction touches exactly one region; region is a column on every hot table. The implementation runs one database; in production each region is its own cell (database and roles), with no schema change.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | One database instance, behaviour on outage undefined | Rejected. |
| 2 | CP writes, sync replica, automatic failover, 503 + retry; region-scoped transactions | **Chosen** |
| 3 | Fully independent cell per region with static stability, plus bounded fail-open for abundant products with per-instance escrow budgets | Rejected for the implementation; cells are on the growth path. Fail-open is rejected outright by assumption A-5. |
| — | AP with later compensation | Rejected for GPUs and IPs. |
| — | Multi-master | Rejected. Conflict resolution on counters breaks the invariant. |
| — | One global database for all regions | Rejected for production. Its outage is every region's outage, and inter-city latency enters the lock time. |

## Consequences

- The 99.95% budget is ~22 minutes per month; one 60-second failover uses ~5% of it.
- With 3 regions and 6 holds/s peak, separate cells buy no throughput, only a smaller blast radius.

## Revisit when

- An SLO is set per region, or a fourth region (for example outside the country) is added: deploy one cell per region.
- The target becomes 99.99% (4.4 minutes per month): shorten failover rather than accept oversell.
