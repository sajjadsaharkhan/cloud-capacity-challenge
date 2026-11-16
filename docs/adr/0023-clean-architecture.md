# ADR-0023: Clean architecture; cross-aggregate invariants behind a port, enforced in SQL

- Status: Accepted
- Date: 2026-11-16
- Related: [architecture](../architecture/backend-architecture.md), [ADR-0004](0004-concurrency-control.md), [ADR-0013](0013-data-access-npgsql-dapper.md)

## Context

The backend should follow clean architecture: business rules in the domain, frameworks and I/O at the edges. The most important rule, `held + used ≤ sellable`, cannot be checked in application memory: two requests would both see one unit left.

## Decision

Four projects with the dependency rule Domain ← Application ← Infrastructure, Host; checked by an architecture test.

Rules are placed by what they touch:

| Rule touches | Lives in | Enforced by |
| --- | --- | --- |
| One reservation (state transitions, deadlines, extension) | Domain (`Reservation`, `RequestStateMachine`) | Decided in C# on a row locked `FOR UPDATE`, written with a version check |
| Shared counters (capacity, quota) | Domain defines the formula (`SellableCapacity`); the `ICapacityLedger` port states the invariant | Conditional `UPDATE` and `CHECK` constraint in the infrastructure adapter |

The domain receives `now` from the transaction; it never reads a clock. The application owns transaction boundaries (one use case, one transaction) and the global lock order.

## Options considered

| Option | Verdict |
| --- | --- |
| Clean architecture with invariant-bearing ports | **Chosen** |
| Pure domain-driven check of capacity in C# before saving | Rejected. Race-prone; correct only with a global lock that destroys throughput. |
| Transaction script with SQL in endpoints | Rejected. Rules are scattered and untestable without a database. |
| Stored procedures holding all logic | Rejected. Hard to test and review; ties domain logic to the database. |

## Consequences

- The domain is unit-tested exhaustively without a database.
- Infrastructure contains SQL that enforces an invariant, which some readers see as business logic outside the domain. It is bounded to `ICapacityLedger`, and a property test checks it agrees with the domain formula.

## Revisit when

Not expected to change.
