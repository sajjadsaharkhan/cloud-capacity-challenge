# ADR-0022: Two DFAs; effective state computed on read

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 02](../specs/02-request-state-machine.md), [spec 03](../specs/03-resource-state-machine.md), [spec 05](../specs/05-expiry-and-effective-state.md)

## Context

Requests and capacity have different lifecycles. A request ends when it is confirmed; the capacity it took lives until the service is deleted. Deadlines must be honoured even when no background job has run yet.

## Decision

- **Request DFA:** start `NEW`; intermediate `WAITING`, `HELD`; final `CONFIRMED`, `REJECTED`, `CANCELLED`, `EXPIRED`. Each intermediate state has a deadline that splits time: before it only client events are legal, after it only the timer event.
- **Resource DFA** over one capacity unit: start `REGISTERED`; intermediate `AVAILABLE`, `HELD`, `ALLOCATED`, `OFFLINE`; final `RETIRED`. Stored as counters, not unit rows. Release lives here, not in the request.
- **Effective state on read:** readers never see a stored intermediate state past its deadline; they see the final state with `settled: false` until the sweeper or the inline finalizer stores it. Reads never write.
- Every write's guard is the effective-state condition, using the database clock.

## Options considered

| Option | Verdict |
| --- | --- |
| Two DFAs, effective state on read | **Chosen** |
| One DFA with `RELEASED` as a request state | Rejected. `CONFIRMED` would be an intermediate state with no deadline, open for months, which breaks the termination guarantee. |
| Update the row when it is read past its deadline | Rejected. Reads become writes, fail on read replicas, and contend with the sweeper. |
| Trust the stored state; rely on the sweeper | Rejected. A stopped sweeper would show dead holds as live and keep their capacity. |

## Consequences

- Every request reaches a final state within 45 minutes with default policy.
- A stopped sweeper causes neither oversell nor undersell.
- The definition of effective state exists once, in a database view.

## Revisit when

Not expected to change.
