# Architecture Decision Records

One decision per file. Each records the context, the decision, the options considered (usually at three maturity levels: minimal, chosen middle, full enterprise), why the others were rejected, the consequences, and the measurable condition under which to revisit it.

Status values: `Proposed`, `Accepted`, `Superseded by ADR-NNNN`.

## Design decisions

| ADR | Decision | Status |
| --- | --- | --- |
| [0001](0001-system-boundary.md) | The service reserves capacity; it does not orchestrate service creation | Accepted |
| [0002](0002-resource-model.md) | Resources modelled as pools, classes and counter-based inventories | Accepted |
| [0003](0003-ledger-and-counters.md) | Append-only ledger is the source of truth; counters are updated in the same transaction | Accepted |
| [0004](0004-concurrency-control.md) | Concurrency by conditional atomic updates in a fixed lock order | Accepted |
| [0005](0005-synchronous-hold-with-lease.md) | Synchronous hold decision with a time-limited lease | Accepted |
| [0006](0006-idempotency.md) | Client idempotency keys stored in the same transaction | Accepted |
| [0007](0007-quota-in-the-ledger.md) | Quotas are counters in the same transaction; holds count | Accepted |
| [0008](0008-failure-mode-and-regional-cells.md) | Consistency over availability; fail closed; one cell per region | Accepted |
| [0009](0009-transactional-outbox.md) | Events through a transactional outbox merged with the event history | Accepted |
| [0010](0010-modular-monolith.md) | Modular monolith with three runtime roles | Accepted |
| [0021](0021-shortage-responses-and-wait-queue.md) | Three shortage responses; opt-in, bounded wait | Accepted |
| [0022](0022-two-state-machines-effective-state.md) | Two DFAs; effective state computed on read | Accepted |
| [0023](0023-clean-architecture.md) | Clean architecture; cross-aggregate invariants behind a port, enforced in SQL | Accepted |
| [0024](0024-lean-data-model.md) | Lean data model for the delivered implementation | Accepted |

## Technology decisions

| ADR | Decision | Status |
| --- | --- | --- |
| [0011](0011-language-dotnet.md) | C# on .NET 10 with ASP.NET Core | Accepted |
| [0012](0012-database-postgresql.md) | PostgreSQL as the only source of truth | Accepted |
| [0013](0013-data-access-npgsql-dapper.md) | Npgsql and Dapper with hand-written SQL; DbUp migrations | Accepted |
| [0014](0014-rest-api.md) | REST with OpenAPI and Problem Details | Accepted |
| [0015](0015-no-redis-in-v1.md) | No Redis in the first version | Accepted |
| [0016](0016-message-broker-rabbitmq.md) | RabbitMQ as the event broker | Accepted |
| [0017](0017-no-workflow-engine.md) | No workflow engine | Accepted |
| [0018](0018-postgresql-high-availability.md) | CloudNativePG with synchronous replication (design only) | Accepted |
| [0019](0019-observability.md) | OpenTelemetry and Prometheus | Accepted |
| [0020](0020-delivery-and-testing.md) | Docker Compose delivery; tests against real PostgreSQL | Accepted |

## Template

```markdown
# ADR-NNNN: Title

- Status: Accepted
- Date: YYYY-MM-DD
- Related: …

## Context
## Decision
## Options considered
| Level | Option | Verdict |
## Consequences
## Revisit when
```
