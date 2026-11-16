# Spec 11 — Testing

Correctness lives in SQL and transactions, so the important tests run against real PostgreSQL. Related: [ADR-0020](../adr/0020-delivery-and-testing.md), [edge cases §9](../edge-cases/edge-cases.md#9-tests-that-prove-the-critical-cases).

## 1. Layers

| Project | Runs against | Proves |
| --- | --- | --- |
| `Capacity.Domain.Tests` | Nothing | Every (state, event) pair of both DFAs; shortage classifier; quantity rules; extension cap |
| `Capacity.Application.Tests` | In-memory fakes of the ports | Use-case orchestration: order of calls, error mapping, idempotency branches |
| `Capacity.IntegrationTests` | PostgreSQL and RabbitMQ in Testcontainers | Concurrency, invariants, recovery, SQL ↔ domain agreement, API contract |
| `Capacity.ArchitectureTests` | Assemblies | Dependency rule of the clean architecture |
| `load/k6` | Full stack in Docker Compose | Hot-row ceiling, scarce burst, sweeper drain rate |

xUnit, FluentAssertions-style assertions, Testcontainers for .NET, `WebApplicationFactory` for API tests.

## 2. Critical integration tests

| # | Test | Edge cases | Pass condition |
| --- | --- | --- | --- |
| 1 | 1,000 parallel submits on a pool with capacity 100 | C-1, C-3, Q-1 | Exactly 100 HELD; `held <= sellable` at every sampled moment |
| 2 | Multi-line requests with random line order, in parallel | C-2 | No deadlock errors |
| 3 | Confirm and sweeper racing on holds expiring at the same instant | C-4, C-6, C-7, T-2, T-4 | Each hold has exactly one final state; ledger sums equal counters |
| 4 | Each operation repeated with the same key, serially and in parallel | R-1, R-2, R-3, F-2 | One reservation, identical responses |
| 5 | API process killed under load, clients retry with their keys | F-1, F-2 | No duplicate or partial reservations |
| 6 | Sweeper stopped while load continues | T-1, T-7, Q-6 | Reads show EXPIRED; new holds succeed using expired capacity |
| 7 | Customer A uses customer B's ids | D-4 | Every response 404 |
| 8 | Repeated release; release on a non-confirmed reservation; confirm after expiry | D-5, R-7 | Capacity returns once; late confirm gets 409 |
| 9 | One customer looping hold and abandon on a scarce product | A-1, A-2, A-3 | Share cap, hold limit and cooldown apply |
| 10 | Invariant check after every test, plus k6 load on one pool | F-11, C-10 | Drift 0; hot-row ceiling measured |

## 3. Property test: SQL agrees with the domain

Random sequences of submit, confirm, cancel, extend, expire, release and admin inventory changes run against the database. After each step, a pure in-memory model built from the domain's `SellableCapacity` and the two state machines predicts counters and statuses; the test asserts the database matches. FsCheck generates the sequences.

## 4. Invariants checked after every integration test

- `held + used <= sellable` for every inventory row.
- Counters equal ledger sums.
- `held` equals lines of HELD reservations; `used` equals lines of CONFIRMED reservations with ACTIVE allocation.
- Every reservation's stored status is consistent with its columns (the `CHECK` would fail otherwise).

## 5. Load test scenarios (k6)

| Scenario | Shape | Records |
| --- | --- | --- |
| Hot row | Ramp submits on one pool from 50 to 600 / s | Throughput and lock-wait p99 at which latency breaks 150 ms |
| Scarce burst | 300 / s for 2 min on a product with 200 units | Rejection mix, p99, oversell (must be 0) |
| Expiry storm | 50k holds expiring within 1 min | Expiry lag, sweeper rate |
| Mixed | Load-model peak × 10 for 30 min | All SLOs |

Results are committed to `load/results/` with the hardware used, and the load model is updated when they differ.

## 6. CI

`dotnet test` for all projects (Docker required for integration tests), architecture tests, and an OpenAPI diff check against the committed spec.
