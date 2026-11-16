# ADR-0016: RabbitMQ as the event broker

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR-0009](0009-transactional-outbox.md), [spec 08](../specs/08-events.md)

## Context

Internal services need reservation events. Volume: each reservation produces about 3 events over its life, so ~20 events/s at the estimated peak and ~1,500/s at the design target. Consumers each want a subset of event types.

## Decision

RabbitMQ with a durable topic exchange `capacity.events`; consumers bind their own durable quorum queues by routing key. The relay publishes through a `Publisher` interface, so the broker is replaceable. In the delivered Compose stack RabbitMQ runs under an optional profile; without it, events stay in the table and are logged.

## Options considered

| Option | Verdict |
| --- | --- |
| RabbitMQ | **Chosen.** Quorum queues handle tens of thousands of messages per second, more than ten times the design target. Topic routing gives each consumer its subset without code. Lighter to run in Compose and in production. |
| Kafka | Rejected. Its strengths are throughput in the hundreds of thousands per second, long log retention and replay for independent consumer groups. None is needed: volume is far below RabbitMQ's ceiling, and replay comes from `reservation_events` and the ledger in PostgreSQL, which are the source of truth. It adds a heavier cluster and partition and offset management. |
| NATS JetStream | Rejected narrowly. Technically sufficient; RabbitMQ chosen for topic routing and more established operational tooling. If NATS is the organisation's standard, only the `Publisher` implementation changes. |
| A queue on the reservation path | Rejected. The serialization point is the database anyway; a queue adds latency and unknown states. |

## Consequences

- RabbitMQ does not guarantee order across competing consumers. Each event carries the reservation `version`; consumers drop older versions. Where strict order is needed, a consumer uses a queue with single active consumer.

## Revisit when

Sustained event rate above 20,000/s, an analytics consumer that streams months of history, or Kafka as the organisation standard. In each case only the `Publisher` implementation changes.
