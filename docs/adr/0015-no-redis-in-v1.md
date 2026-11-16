# ADR-0015: No Redis in the first version

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR-0012](0012-database-postgresql.md), [spec 07 §6](../specs/07-quotas-and-fairness.md#6-rate-limits)

## Context

Caching capacity levels, the sold-out negative cache and rate limits are the usual reasons to add Redis.

## Decision

No Redis. The capacity snapshot is ~90 keys (3 regions × ~30 products) cached in process for 2 seconds; at that interval each API instance queries the database at most ~45 times per second for it, independent of read traffic. The sold-out cache and rate limits are per instance.

## Options considered

| Option | Verdict |
| --- | --- |
| In-process caches | **Chosen.** One fewer dependency is one fewer failure mode. |
| Redis for cache and rate limits | Rejected for now; on the growth path. |
| Redis on the correctness path (counters, locks, idempotency) | Rejected in any version ([ADR-0004](0004-concurrency-control.md), [ADR-0006](0006-idempotency.md), [ADR-0012](0012-database-postgresql.md)). |

## Consequences

- Rate limits are approximate: per-instance limits multiply with instance count.
- Different instances may serve capacity levels up to 2 seconds apart.

## Revisit when

More than 10 API instances, or exact global rate limits that the gateway cannot provide. Even then Redis stays off the correctness path.
