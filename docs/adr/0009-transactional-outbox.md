# ADR-0009: Events through a transactional outbox merged with the event history

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 08](../specs/08-events.md), [ADR-0016](0016-message-broker-rabbitmq.md)

## Context

The system is used by internal services that need to know about state changes. A broker outage must not stop reservations, and an event must never be published for a change that did not commit, or lost for one that did.

## Decision

Each transition inserts a row into `reservation_events` in the same transaction. The same table is the transition history (actor, reason, from and to) and the outbox (`published_at` is null until relayed). A relay reads unpublished rows with `FOR UPDATE SKIP LOCKED`, publishes with confirms and marks them. Delivery is at least once; consumers deduplicate by `event_id` and order by reservation `version`.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | Publish to the broker inside the request handler (dual write) | Rejected. Event without commit, or commit without event. |
| 2 | Transactional outbox and relay | **Chosen** |
| 3 | CDC with Debezium, schema registry, signed customer webhooks | Rejected for now. Extra infrastructure for this volume. The events table is a natural CDC source later. |
| — | Separate `outbox` and `reservation_transitions` tables | Rejected. Same content written twice per transition. |

## Consequences

- Hundreds of milliseconds of event latency.
- One fewer insert per transition than separate tables.
- Events are kept as history, so publication does not delete them.

## Revisit when

Event rate reaches several thousand per second: move the relay to CDC.
