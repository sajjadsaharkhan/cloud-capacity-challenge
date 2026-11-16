# ADR-0010: Modular monolith with three runtime roles

- Status: Accepted
- Date: 2026-11-16
- Related: [architecture](../architecture/backend-architecture.md), [ADR-0023](0023-clean-architecture.md)

## Context

Capacity, quota and reservation state must change in one transaction. The service is used in the service-creation path and must be simple for an evaluator to run.

## Decision

One repository, one build, one container image, with modules (Reservations, Capacity, Catalog, Admin, Jobs) inside a clean architecture. The image runs in three roles chosen by configuration: `api`, `sweeper`, `relay`.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | One process doing everything with in-memory timers | Rejected. Timers die with the process; cannot scale API and background work separately. |
| 2 | Modular monolith, three roles, all state in PostgreSQL | **Chosen** |
| 3 | Microservice per domain (quota, capacity, reservation, events) | Rejected. Puts a service boundary through the main transaction and requires sagas, with no scale or team reason. |

## Consequences

- Horizontal scale by adding `api` instances; roles scale independently.
- Any instance can be killed at any time; recovery needs no coordination.

## Revisit when

Separate teams own quotas and capacity, or one module needs a different release cadence.
