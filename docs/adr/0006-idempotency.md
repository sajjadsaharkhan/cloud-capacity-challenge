# ADR-0006: Client idempotency keys stored in the same transaction

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 06](../specs/06-idempotency.md), edge cases [R-1 … R-5](../edge-cases/edge-cases.md#2-retries-and-unknown-outcomes)

## Context

"Sometimes the outcome of a request is unclear to the customer and they send it again." Automation retries on every timeout. A retry must never create a second reservation.

## Decision

Every mutating call carries an `Idempotency-Key`, unique per customer, inserted into `idempotency_keys` inside the same transaction as the operation. A retry returns the stored response; the same key with a different body returns `422`. Keys live 24 hours. `client_reference`, unique among open and confirmed reservations, is a second line of defence. All transitions are also naturally idempotent on their own final state.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | No deduplication, or dedupe by body hash | Rejected. Retries duplicate; two legitimate identical requests become one. |
| 2 | Client key in the same transaction, plus `client_reference` | **Chosen** |
| 3 | Natural keys from upstream systems and exactly-once effects across services (outbox + consumer dedupe everywhere) | Rejected for now. Needs a contract across teams; the outbox part is adopted in [ADR-0009](0009-transactional-outbox.md). |
| — | Keys in Redis | Rejected. Separate from the transaction; a crash between the two leaves a window. |

## Consequences

- Concurrent duplicates serialize on the primary key; the second waits and then replays the first's result.
- The table stays near 150k rows at the estimated load.
- Clients must generate keys; machine clients are required to.

## Revisit when

Not expected to change.
