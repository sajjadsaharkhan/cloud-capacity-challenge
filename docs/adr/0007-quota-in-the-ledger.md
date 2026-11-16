# ADR-0007: Quotas are counters in the same transaction; holds count

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 07](../specs/07-quotas-and-fairness.md), [PRD A-11, A-12](../prd/PRD.md#7-assumptions)

## Context

"Each customer has limits based on their contract." Limits must hold under parallel requests from the same customer.

## Decision

A quota row per (customer, resource class, region) with `quota_limit`, `held` and `used`, updated by the same kind of conditional `UPDATE` in the same transaction as the capacity rows. Open holds count against quota. Quota rows are locked before inventory rows. The limit is a condition in the update, not a `CHECK`, so that lowering a contract below current usage remains possible.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | Check in application code before the hold, or count from the services table | Rejected. Two parallel requests both pass the check. |
| 2 | Quota counters in the same transaction | **Chosen** |
| 3 | Hierarchical quotas (organisation, project), versioned contracts, soft limits with burst, quota-increase workflow, global quotas with escrow per region | Rejected for now. A separate service; no requirement for it in the brief. |
| — | Count only confirmed allocations | Rejected. Parallel holds let a customer exceed the contract. |
| — | Separate quota service | Rejected. Turns the hottest path into a distributed transaction. |

## Consequences

- One quota row per customer and class; it is not a hot row across customers.
- Locking the quota row first also serializes a customer's own parallel requests, which the pool share cap relies on ([spec 07 §4](../specs/07-quotas-and-fairness.md#4-pool-share-cap)).
- Quotas across regions are not supported.

## Revisit when

A contract spans regions or organisations need hierarchical limits.
