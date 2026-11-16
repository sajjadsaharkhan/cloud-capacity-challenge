# Cloud Capacity Allocation

A backend service that lets customers see cloud capacity, reserve it atomically across several resources, and confirm or cancel the reservation, without ever selling the same capacity twice.

## The problem

A cloud provider sells several products (VMs, managed services, GPUs, public IPs) in several regions. Before a service is created, the provider must check that the request can be served, and often keep that capacity aside for a short time while the service is built. Done as separate "check" and "take" steps, this fails in three ways:

| What goes wrong | Cause | What this service guarantees |
| --- | --- | --- |
| The customer sees capacity, but it is gone when the service is created | Checking and taking are separate steps | An atomic, time-limited **hold** taken in one transaction |
| The outcome of a request is unclear, so the customer sends it again | No durable request state, no deduplication | A durable request state machine and **idempotency** on every mutation |
| A process starts and never finishes | Nobody owns abandoned work | Every intermediate state has a **deadline**, and capacity always comes back |

Out of scope: a UI, authentication and user management, billing, creating real VMs, and a real multi-region deployment. The full problem, scope, requirements and the 20 assumptions the brief left open are in the [PRD](docs/prd/PRD.md).

## The solution

A customer submits a reservation of up to 16 resource lines (vCPU, RAM, disk, IPv4, GPU) in one region. Every line is held, or none is.

```
submit ──► HELD ──confirm──► CONFIRMED ──release──► (capacity returns)
   │         ├──cancel───► CANCELLED
   │         └──lease ends► EXPIRED
   ├──► WAITING (opt-in, short temporary shortage only) ──capacity returns──► HELD
   └──► REJECTED (stored, with the reason)
```

- **No oversell.** Capacity lives in counters on inventory rows. A hold is a conditional `UPDATE ... WHERE held + used + q <= sellable`, backed by a `CHECK` constraint. Locks are taken in one fixed order, so there are no deadlock cycles.
- **Every change is explainable.** An append-only ledger records each counter movement. Each counter must equal the sum of its ledger rows, and a reconciliation report checks it.
- **Retries are safe.** Every state-changing call takes an `Idempotency-Key`. The same key returns the same answer and never creates a second reservation. A refusal is stored, so it replays too.
- **Nothing hangs.** A hold has a lease (default 15 minutes, one extension). A sweeper stores the end of expired holds and returns their capacity. Reads already report the true state: a hold past its lease reads `Expired` with `settled: false` before the sweeper stores it.
- **Honest shortage answers.** `exceeds_registered` (never), `sold_out` (not soon), or `temporarily_unavailable` (with `Retry-After`). Only the last can opt in to waiting, for at most 15 minutes.
- **Fairness.** Per-customer quotas (open holds count), a cap on concurrent holds, a cap on one customer's share of a scarce pool, a cooldown after repeated expiries, and per-customer rate limits.
- **Events without coupling.** Every transition is written to a transactional outbox in the same transaction and relayed to RabbitMQ afterwards. The broker is never on the decision path.

### Architecture

```
client ──► gateway ──► api (stateless, 3+ replicas) ──► PostgreSQL 17 (source of truth)
                                                          ▲        │
                                  sweeper (expire / time out / promote)   relay ──► RabbitMQ
```

One codebase, three runtime roles (`api`, `sweeper`, `relay`). The code follows the Operation / Flow / UseCase framework used in the SnappFood `credit` project:

- **UseCase** owns the transaction. Every endpoint and background job is one.
- **Flow** sequences two or more aggregates and is shared by two or more callers.
- **Operation** owns one aggregate and never commits.

Layers are `Domain → Application → Infrastructure → Presentation`, enforced by architecture tests. Data access is Npgsql and Dapper with hand-written SQL, not an ORM. Design details: [architecture](docs/architecture/backend-architecture.md), [24 ADRs](docs/adr/README.md), [specs](docs/specs/README.md), [database](docs/database/database.md), [61 edge cases](docs/edge-cases/edge-cases.md).

## Stack

| | |
| --- | --- |
| Language and framework | C# on .NET 10, ASP.NET Core controllers, FluentValidation |
| Database | PostgreSQL 17, Npgsql and Dapper, DbUp migrations |
| Messaging | RabbitMQ 4 (topic exchange `capacity.events`, publisher confirms) |
| Observability | OpenTelemetry traces and metrics, Prometheus `/metrics`, JSON logs, optional Grafana |
| Tests | xUnit, Testcontainers (real PostgreSQL), NetArchTest, k6 for load |
| Delivery | Docker, Docker Compose, Make |

## How to run

**Requirements:** Docker with Compose v2 and `make`. The .NET 10 SDK is only needed for `make test`.

```bash
make up      # build, start PostgreSQL + RabbitMQ + migrations (with demo data) + api + sweeper + relay
make demo    # walk through every outcome with curl and print each request and response
make test    # domain, concurrency, recovery, HTTP contract and architecture tests (needs Docker + .NET 10 SDK)
make load    # k6 scenarios against the running stack
make down    # stop and remove containers and volumes
```

Also: `make observability` is `make up` plus an OpenTelemetry Collector, Prometheus and Grafana.

After `make up`:

| | |
| --- | --- |
| API | http://localhost:8080 |
| OpenAPI document | http://localhost:8080/openapi/v1.json (committed copy: [docs/openapi/capacity.v1.json](docs/openapi/capacity.v1.json)) |
| Health | http://localhost:8080/health/live, `/health/ready` |
| Metrics | http://localhost:8080/metrics |
| RabbitMQ management | http://localhost:15672 (guest / guest) |
| Prometheus, Grafana | http://localhost:9090, http://localhost:3000 (after `make observability`) |

### Try it by hand

The gateway normally authenticates and sets the identity headers. Here you set them yourself. The demo data has customers `demo-small`, `demo-medium` and `demo-large`.

```bash
curl -i -X POST http://localhost:8080/v1/reservations \
  -H "X-Customer-Id: demo-medium" -H "X-Actor-Kind: CUSTOMER" -H "X-Actor-Id: me" \
  -H "Idempotency-Key: try-1" -H "Content-Type: application/json" \
  -d '{"product":"vm-standard","region":"ir-thr",
       "lines":[{"resourceClass":"Vcpu","quantity":4000},{"resourceClass":"RamMb","quantity":8192}]}'
```

| Endpoint | Purpose |
| --- | --- |
| `GET /v1/capacity` | Capacity level per product and region |
| `POST /v1/capacity/fit-check` | Would this request be accepted now? (advisory) |
| `POST /v1/reservations` | Submit a reservation |
| `GET /v1/reservations`, `GET /v1/reservations/{id}` | History and effective status |
| `POST /v1/reservations/{id}/confirm`, `/cancel`, `/extend` | Transitions |
| `POST /v1/reservations/{id}/release` | Give confirmed capacity back (service or admin) |
| `/v1/admin/...` | Pools, inventories, products, quotas, ledger adjustments, reconciliation (admin) |

Responses use the envelope `{ "isSuccess", "data", "error": { "code", "message" }, "traceId" }`. A refused reservation is a stored `409` or `422` carrying the reservation in `data` and the reason in `error.code`.

### Configuration

Environment variables override `appsettings.json`: `ConnectionStrings__Database`, `Capacity__Roles` (`sweeper`, `relay`), `RabbitMq__Uri` (empty means the relay logs events instead of publishing), `Database__Migrate`, `Database__SeedDemoData`. See [spec 12](docs/specs/12-configuration-and-deployment.md).

## Repository layout

```
src/            Domain, Application, Infrastructure, Presentation (Api, BackgroundService), Framework
tests/          Arvan.Capacity.Invariants.Tests, Arvan.Capacity.ArchitectureTests
load/           k6 scenarios and recorded results
deploy/         Prometheus, Grafana, OpenTelemetry Collector configuration
scripts/        demo.sh
docs/           PRD, architecture, specs, ADRs, database, edge cases, reports
```

## Performance, honestly

The target is hold p99 under 150 ms. On one laptop Docker VM, with a single hot inventory row and one API replica, p99 for a granted hold stays under 150 ms up to 200 requests/s and breaks between 200 and 300/s. A hold costs 9 database round trips (28 before the batching work). Rejections are fast at 200/s but their tail at 100/s and 300/s is still noisy. Nothing has been measured on Linux hardware, with several replicas, or with synchronous replication. Details and the plan for the rest: [hold-latency report](docs/reports/hold-latency.html) and [load/results](load/results/). Under every load tried, there was no oversell and zero ledger drift.

## Documentation

Start at [docs/README.md](docs/README.md).

| | |
| --- | --- |
| [PRD](docs/prd/PRD.md) | Problem, scope, requirements, assumptions |
| [Architecture](docs/architecture/backend-architecture.md) | Layers, project layout, flows, growth path |
| [Specs](docs/specs/README.md) | API, state machines, allocation, expiry, idempotency, events, admin, observability, testing, deployment |
| [ADRs](docs/adr/README.md) | 24 decisions with rejected alternatives |
| [Database](docs/database/database.md) | ERD, constraints, indexes |
| [Edge cases](docs/edge-cases/edge-cases.md) | 61 cases, how each is handled, and the tests |
| [Hold-latency report](docs/reports/hold-latency.html) | Where hold latency goes, the plan, and results (self-contained HTML) |

Where the written specs and the implementation differ (response envelope and error codes, page-numbered lists, the framework's tiers instead of a mediator), the implementation follows the `credit` framework.
