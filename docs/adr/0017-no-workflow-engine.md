# ADR-0017: No workflow engine

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR-0001](0001-system-boundary.md), [spec 05](../specs/05-expiry-and-effective-state.md)

## Context

Holds and waits have deadlines, which suggests durable timers.

## Decision

The request lifecycle has seven states and two timers. A state machine in the database plus a sweeper is enough. No workflow engine.

## Options considered

| Option | Verdict |
| --- | --- |
| State machine in PostgreSQL + sweeper | **Chosen** |
| Temporal, Camunda or similar | Rejected. Adds a multi-service cluster with its own database to the critical path. |
| Redis TTL keys or a cron job per reservation | Rejected. Expiry events can be lost; cron races with confirm. |

## Consequences

- Timer precision is the sweeper interval (1 s), and correctness does not depend on it because reads compute the effective state.

## Revisit when

Orchestration of service creation enters the scope ([ADR-0001](0001-system-boundary.md)); then a workflow engine is the right tool.
