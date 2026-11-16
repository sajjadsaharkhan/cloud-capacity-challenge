# ADR-0003: Append-only ledger is the source of truth; counters are updated in the same transaction

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 04](../specs/04-capacity-allocation.md), [spec 09 §6](../specs/09-admin-api.md#6-reconciliation-report)

## Context

The brief requires that behaviour after recovery be explainable and reviewable. The hold decision also has to be made in a few milliseconds and enforce a hard upper bound.

## Decision

Every change to `held` or `used` writes one `ledger_entries` row (`HOLD`, `COMMIT`, `RETURN`, `RELEASE`, `ADJUST`) and updates the inventory and quota counters **in the same transaction**. Counters are a projection of the ledger. An hourly reconciliation recomputes counters from the ledger and reports drift.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | Mutable status column and counters only, plus application logs | Rejected. After an incident nobody can say why a counter holds its value. |
| 2 | Append-only ledger + counters in the same transaction + reconciliation | **Chosen** |
| 3 | Full event sourcing with snapshots, reconciliation against hypervisor inventory, tamper-evident log | Rejected for now. Operational complexity and a dependency on real infrastructure. |
| — | Ledger only, `SUM` on every request | Rejected. Cost grows with history and the bound cannot be enforced cheaply. |
| — | Counters projected asynchronously from the ledger | Rejected. Projection lag means oversell. |

## Consequences

- About 12 row writes per hold (4 lines, 4 ledger rows, quota, reservation, event, idempotency). At 500 holds/s that is ~6,000 row writes/s, within one primary's capacity.
- Corrections are `ADJUST` rows, never direct updates ([spec 09 §5](../specs/09-admin-api.md#5-ledger-adjustments)).
- Ledger volume ~200M rows per year; partitioning is on the growth path ([ADR-0024](0024-lean-data-model.md)).

## Revisit when

Not expected to change with scale.
