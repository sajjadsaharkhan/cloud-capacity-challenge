# ADR-0005: Synchronous hold decision with a time-limited lease

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 02](../specs/02-request-state-machine.md), [spec 05](../specs/05-expiry-and-effective-state.md), [PRD A-7](../prd/PRD.md#7-assumptions)

## Context

The brief says initial acceptance does not mean completion, and the customer may finalize, cancel, or abandon the process without a result. Capacity sometimes has to stay with the customer "for a short time". Abandoned processes must not keep capacity.

## Decision

- Submit decides in the same HTTP call: HELD, WAITING or REJECTED. The decision takes ~5 ms.
- A hold has `expires_at = now + hold_ttl` (per product; default 15 minutes, range 2–60) and can be extended once.
- The API returns a resource with a status from the start, so the contract supports later asynchronous states.
- Expiry is enforced three ways: effective status on read, the sweeper, and the hold path's inline finalizer. Confirm after `expires_at` is refused using the database clock.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | Synchronous yes/no without durable state | Rejected. Nothing to track, no history, no idempotency. |
| 2 | Synchronous decision + durable state machine + lease | **Chosen** |
| 3 | Asynchronous queue with priorities, waitlists, fair queueing and webhooks | Rejected. Adds "unknown" states for a 5 ms decision, which is the problem the brief describes. A bounded wait is kept for temporary shortages ([ADR-0021](0021-shortage-responses-and-wait-queue.md)). |
| — | Two-phase commit with provisioning services | Rejected. A blocking coordinator; one side's outage locks the other. |
| — | No hold, check only | Rejected. That is today's "seen but gone". |

## Consequences

- The application clock plays no part in correctness.
- A stopped sweeper delays only the stored final state, not correctness or capacity ([T-1](../edge-cases/edge-cases.md#4-time-and-expiry)).
- Worst-case lifetime of a request with defaults: 45 minutes.

## Revisit when

A decision depends on a human or another system (quota increase approval, capacity procurement): then a longer-running asynchronous state is justified.
