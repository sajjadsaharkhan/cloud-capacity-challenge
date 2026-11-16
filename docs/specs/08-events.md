# Spec 08 — Events

Every reservation transition is recorded in `reservation_events` in the same transaction, and a relay publishes it to RabbitMQ afterwards. The table is both the audit history and the transactional outbox. Related: [ADR-0009](../adr/0009-transactional-outbox.md), [ADR-0016](../adr/0016-message-broker-rabbitmq.md).

## 1. Event types

| Routing key | Transition |
| --- | --- |
| `reservation.held` | NEW → HELD, WAITING → HELD |
| `reservation.waiting` | NEW → WAITING |
| `reservation.rejected` | NEW → REJECTED, WAITING → REJECTED |
| `reservation.extended` | HELD → HELD |
| `reservation.confirmed` | HELD → CONFIRMED |
| `reservation.cancelled` | HELD → CANCELLED, WAITING → CANCELLED |
| `reservation.expired` | HELD → EXPIRED |
| `reservation.released` | `allocation_state` ACTIVE → RELEASED |

Admin changes to pools, inventories, products and quotas are logged and audited, not published in this version.

## 2. Envelope

```json
{
  "event_id": "ir-thr:184467",
  "event_type": "reservation.confirmed",
  "occurred_at": "2026-11-16T10:03:12.481Z",
  "reservation_id": "0193a4f2-7c1e-7b2a-9f3e-5d6c7b8a9e01",
  "version": 3,
  "customer_id": "c-5521",
  "region": "ir-thr",
  "product": "vm-standard",
  "from_status": "HELD",
  "to_status": "CONFIRMED",
  "actor": { "kind": "SERVICE", "id": "vm-service" },
  "reason_code": null,
  "lines": [ { "resource_class": "VCPU", "quantity": 4000, "zone": "ir-thr-1" } ],
  "expires_at": null
}
```

`event_id` = cell (region) + `reservation_events.id`. `version` is the reservation's version after the transition.

## 3. Broker topology

| Item | Value |
| --- | --- |
| Exchange | `capacity.events`, type `topic`, durable |
| Routing key | Event type, e.g. `reservation.confirmed` |
| Message | Persistent, `content-type: application/json`, `message-id = event_id` |
| Consumers | Bind their own durable quorum queues, e.g. `vm-service.reservations` on `reservation.*` |

## 4. Relay

Runs in the `relay` role, every 200 ms, looping while batches are full:

```sql
SELECT id, payload
FROM   reservation_events
WHERE  published_at IS NULL
ORDER  BY id
LIMIT  500
FOR UPDATE SKIP LOCKED;
```

Publish each with publisher confirms; after all confirms, `UPDATE reservation_events SET published_at = now() WHERE id = ANY(@ids)`; commit. If the relay dies between publish and commit, the batch is published again.

## 5. Delivery guarantees

| Property | Guarantee | Consumer responsibility |
| --- | --- | --- |
| Delivery | At least once | Deduplicate by `event_id` |
| Order | Per reservation, `version` increases | Ignore an event whose `version` is not greater than the last applied |
| Latency | Typically < 1 s after commit | — |
| Broker outage | Events accumulate in the table and are sent later; reservations are unaffected | — |

## 6. Retention

Events are history and are not deleted with publication. Online retention follows reservations (13 months); see [database growth path](../database/database.md#8-growth-path-to-the-real-environment) for partitioning.

## 7. Metrics

`capacity_outbox_backlog` (unpublished rows), `capacity_outbox_oldest_age_seconds`, `capacity_relay_publish_failures_total`. Alert on age > 60 s.
