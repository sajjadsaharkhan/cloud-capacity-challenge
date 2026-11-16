# ADR-0024: Lean data model for the delivered implementation

- Status: Accepted
- Date: 2026-11-16
- Related: [database](../database/database.md), [ADR-0008](0008-failure-mode-and-regional-cells.md), [PRD A-13, A-18](../prd/PRD.md#7-assumptions)

## Context

The brief says the delivered implementation need not reproduce real multi-region infrastructure or the company's operational load, but the design must show how the system can develop in a real environment. An earlier draft had 18 tables, many of which existed only for that real environment.

## Decision

Nine tables and one view in the implementation: `pools`, `inventories`, `products`, `quotas`, `reservations`, `reservation_lines`, `reservation_events`, `ledger_entries`, `idempotency_keys`, and the view `reservation_effective`. Everything only needed in the real environment is written down as a growth step with a trigger, not built.

Kept despite the size, because correctness depends on them: the ledger, idempotency keys, counters with `CHECK` constraints.

| Removed from the draft | Now | Why |
| --- | --- | --- |
| `regions`, `zones` | Text columns on `pools`, validated against configuration | Three fixed regions do not need tables |
| `resource_classes` | Text column; definition in configuration | Five classes with no behaviour in the database |
| `customers` | Removed | Identity is out of scope; the id comes from a header |
| `product_policies`, `product_resources` | Columns of `products` | One-to-one and an array |
| `outbox` | Merged into `reservation_events` | Each event is a transition |
| `hold_cooldowns` | Query on `reservations` | Derivable from recent expiries |
| `unmet_demand` | Structured log and metric | Analytics, not part of the decision |
| Monthly partitions on ledger and events | Plain tables | Pointless at delivered volume |

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | Fewer tables: drop the ledger, store idempotency in memory | Rejected. Loses correctness and explainability. |
| 2 | Nine tables plus a documented growth path | **Chosen** |
| 3 | The 18-table model with partitions, reference tables and hierarchical customers | Rejected for the implementation; its parts are the growth path. |

## Consequences

- Region, zone and class are validated by application configuration rather than foreign keys.
- Every growth step is additive; none changes the API or the state machines.

## Revisit when

Each row of the [growth path](../database/database.md#8-growth-path-to-the-real-environment) has its own trigger.
