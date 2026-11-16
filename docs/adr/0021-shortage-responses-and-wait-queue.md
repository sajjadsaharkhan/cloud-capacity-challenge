# ADR-0021: Three shortage responses; opt-in, bounded wait

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 04 §5](../specs/04-capacity-allocation.md#5-shortage-classification), [spec 01 §6](../specs/01-api.md#6-errors), [PRD A-5, A-14, A-20](../prd/PRD.md#7-assumptions)

## Context

The brief does not say what a customer hears when capacity is short. Assumption A-5 says never oversell. A customer, and especially automation, needs to know whether the answer is "never", "not now" or "in a few minutes". A message such as "we will get back to you" must be true when it is given.

## Decision

Compare the request with registered capacity, with capacity minus live allocations, and with free capacity, and answer one of three codes:

| Condition | Code | Retry helps |
| --- | --- | --- |
| More than any pool can ever provide | `EXCEEDS_REGISTERED_CAPACITY` (422) | Never |
| Fits registered capacity, but live allocations fill it | `CAPACITY_SOLD_OUT` (409) | Not on a known timescale |
| Short only because of open holds or an offline pool | `CAPACITY_TEMPORARILY_UNAVAILABLE` (409, `Retry-After`) | Within one TTL |

Only in the third case, and only when the client sends `on_shortage = WAIT`, the request becomes `WAITING` with a deadline of at most 15 minutes. It holds neither capacity nor quota while waiting. At most 4 waiting requests per customer and product. Default is `REJECT`.

The capacity view shows levels (`AVAILABLE`, `LIMITED`, `UNAVAILABLE`), not numbers, for customers.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | One generic "no capacity" error | Rejected. Automation either retries forever or gives up when a retry would have succeeded. |
| 2 | Three classified codes with `Retry-After`, plus bounded opt-in wait | **Chosen** |
| 3 | Full waiting queue with priorities, fair queueing and notifications | Rejected. Fairness and head-of-line blocking problems; a wait on SOLD_OUT is the "process that never finishes". |
| — | Two codes (exceeds total / exceeds free) | Rejected. "Exceeds free" mixes capacity held by live services (returns only when they are deleted) with capacity in holds (returns within one TTL). A promise to get back is true only for the second. |
| — | Reserve part of capacity for waiting requests | Rejected. Two waiters each holding half a request deadlock each other. |
| — | Exact free numbers for customers | Rejected. Leaks business information, is stale by the time the hold arrives, and makes "seen but gone" worse. |

## Consequences

- One extra state (`WAITING`) and one sweeper step (promotion).
- FIFO promotion, but a smaller later request that fits can be served first; a large request's starvation is bounded by its deadline.
- Every `CAPACITY_SOLD_OUT` is a signal for capacity planning (counted in metrics).

## Revisit when

More than 10,000 concurrent waiting requests, or a requirement for contractual priority.
