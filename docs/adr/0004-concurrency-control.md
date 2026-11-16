# ADR-0004: Concurrency by conditional atomic updates in a fixed lock order

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 04 §4](../specs/04-capacity-allocation.md#4-hold-transaction), [load model](../architecture/load-model.md), edge cases [C-1 … C-11](../edge-cases/edge-cases.md#1-concurrency-on-capacity)

## Context

The core invariant is `held + used ≤ sellable` for every inventory row, under hundreds of concurrent requests on the same row. A request touches several rows (CPU, RAM, disk, IP, and the customer's quota rows) and must take all or none.

## Decision

- Each line is one `UPDATE … SET held = held + q WHERE … AND held + used + q <= sellable`. Zero rows affected means a shortage.
- All lines run in one transaction, sent as one batch (one round trip), under READ COMMITTED.
- Global lock order: reservation rows → quota rows → inventory rows, each sorted by key. All writers follow it, so deadlock cycles cannot form.
- A `CHECK` constraint on the table enforces the same bound as a last line of defence.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | Read free capacity, then write it in application code; or a Redis counter without durability | Rejected. Race between two requests; acknowledged writes lost on failover. |
| 2 | Conditional atomic update in a local ACID transaction, fixed lock order | **Chosen** |
| 3 | Escrow (split a pool's capacity into independent slices), single-writer batching, or distributed SQL | Rejected for now. Rebalancing slices and stranded capacity in them; not needed at the estimated load. |
| — | Optimistic locking with a version column | Rejected. On a hot row most attempts fail and retry storms follow. |
| — | `SELECT … FOR UPDATE` then `UPDATE` | Rejected. The extra round trip doubles lock hold time. |
| — | SERIALIZABLE isolation | Rejected. Serialization failures and retries on the hot row. |
| — | Distributed lock in Redis | Rejected. No fencing; correctness depends on clocks. |

## Consequences

- Ceiling per hot row is ~200–300 holds/s (lock held ~3–5 ms); safe limit ~150/s. Estimated peak is 6/s.
- No structural deadlocks.
- Splitting pools per zone or cluster raises the ceiling with data changes only.

## Revisit when

- Sustained > 150 holds/s on one row, or lock-wait p99 > 20 ms: split pools by cluster.
- > 300 holds/s on a pool that cannot be split: escrow.
