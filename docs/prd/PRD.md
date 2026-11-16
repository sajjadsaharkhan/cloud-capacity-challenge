# PRD — Cloud Capacity Allocation Service

| | |
| --- | --- |
| Status | Draft for implementation |
| Scope | Backend core only |
| Related | [Architecture](../architecture/backend-architecture.md) · [Specs](../specs/README.md) · [ADRs](../adr/README.md) · [Edge cases](../edge-cases/edge-cases.md) |

## 1. Problem

A cloud provider sells several products (VMs, managed services, GPUs, public IPs) in several regions. Before some services are created, the provider must check that the customer's request can be served, and sometimes keep that capacity aside for a short time while the service is being built.

Today this is split across tools and manual steps. As customers and products grow, three failures appear:

| Symptom in the brief | Root cause | What this service guarantees |
| --- | --- | --- |
| Customer sees capacity, but it is gone when the service is created | Check and take are separate steps | An atomic, time-limited **hold** taken in one transaction |
| Outcome of a request is unclear and the customer sends it again | No durable request state, no deduplication | Durable request state machine and **idempotency** on every mutation |
| A process starts and never finishes | No owner for abandoned work | Every intermediate state has a **deadline**; capacity always comes back |

## 2. Goals

| ID | Goal | Measure |
| --- | --- | --- |
| G-1 | Never sell the same capacity twice | Oversell incidents = 0; ledger/counter drift = 0 |
| G-2 | Every request has one deterministic, queryable outcome | Retries with the same key return the same result |
| G-3 | Abandoned requests never hold capacity | Expired-hold release lag p99 < 5 s |
| G-4 | Behaviour after any outage is explainable | Every counter is reproducible from the append-only ledger |
| G-5 | Fast enough to sit in the service-creation path | Hold p99 < 150 ms; availability 99.95% |
| G-6 | New products and resources without code changes | Adding a product or resource class is a data change |

## 3. Non-goals

From the brief: graphical UI, full authentication and user management, real payment and billing, real VM or infrastructure creation, real multi-region deployment.

Also out of scope for this version:

- Placement or bin-packing on physical hosts. The service counts capacity; schedulers place it ([A-9](#7-assumptions)).
- Orchestrating service creation ([A-2](#7-assumptions)).
- Customer-facing webhooks ([A-17](#7-assumptions)).

The design must still show how the system grows into the real environment. That growth path is in [database.md §8](../database/database.md#8-growth-path-to-the-real-environment) and [backend-architecture.md §9](../architecture/backend-architecture.md#9-growth-path).

## 4. Users

| User | Uses the service for | Needs |
| --- | --- | --- |
| Customer, through the panel | Seeing availability, reserving, tracking | Clear messages; never told "available" for something it cannot get |
| Customer automation (Terraform, scripts) | The same, at machine speed with retries | Idempotency, machine-readable error codes, `Retry-After` |
| Internal product services (VM, DBaaS, IP) | Confirming holds after provisioning, releasing on deletion | Fast, safe confirm and release; events on state changes |
| Capacity operators | Registering pools, setting overcommit, taking pools offline, setting quotas | Admin API, drift report, metrics |

## 5. Capabilities from the brief

| # | Brief | Requirements |
| --- | --- | --- |
| 1 | View product capacity status | FR-1, FR-2 |
| 2 | Submit a capacity request | FR-3, FR-11, FR-12, FR-13, FR-14 |
| 3 | Track the request status | FR-4 |
| 4 | Complete or cancel the allocation | FR-5, FR-6, FR-7, FR-9, FR-10 |
| 5 | View previous requests | FR-8 |

## 6. Functional requirements

| ID | Requirement | Spec |
| --- | --- | --- |
| FR-1 | Return a capacity level (`AVAILABLE`, `LIMITED`, `UNAVAILABLE`) per product and region. Exact numbers only for internal and admin callers. | [API](../specs/01-api.md) |
| FR-2 | Fit-check: answer whether a given request would be accepted now, including the caller's own quota. Advisory only. | [API](../specs/01-api.md) |
| FR-3 | Submit a reservation of up to 16 resource lines in one region. All lines are held or none are. | [Allocation](../specs/04-capacity-allocation.md) |
| FR-4 | Read a reservation's effective status at the moment of reading, including whether the stored state has caught up (`settled`). | [Expiry](../specs/05-expiry-and-effective-state.md) |
| FR-5 | Confirm a held reservation before it expires; held capacity becomes allocated. | [Request DFA](../specs/02-request-state-machine.md) |
| FR-6 | Cancel a held or waiting reservation; held capacity returns immediately. | [Request DFA](../specs/02-request-state-machine.md) |
| FR-7 | Extend a hold once, by the product's TTL. | [Request DFA](../specs/02-request-state-machine.md) |
| FR-8 | List the caller's reservations, newest first, filtered by status, product and region, with cursor pagination. | [API](../specs/01-api.md) |
| FR-9 | Product services release allocated capacity when the service is deleted or provisioning fails. | [Resource DFA](../specs/03-resource-state-machine.md) |
| FR-10 | Holds and waits past their deadline become final automatically and return capacity. | [Expiry](../specs/05-expiry-and-effective-state.md) |
| FR-11 | Opt-in wait: when the shortage is temporary only, the request may wait up to 15 minutes for capacity. | [Allocation](../specs/04-capacity-allocation.md) |
| FR-12 | Enforce per-customer contract quotas, counting open holds. | [Quotas](../specs/07-quotas-and-fairness.md) |
| FR-13 | Fairness: cap on concurrent holds, cap on one customer's share of a scarce pool, cooldown after repeated expiries. | [Quotas](../specs/07-quotas-and-fairness.md) |
| FR-14 | Every mutating call takes an `Idempotency-Key`; a retry returns the original result. | [Idempotency](../specs/06-idempotency.md) |
| FR-15 | Errors are RFC 9457 Problem Details with a stable machine-readable `code`; shortages say whether retrying can help. | [API](../specs/01-api.md) |
| FR-16 | Admin API for pools, inventories, products and quotas. | [Admin API](../specs/09-admin-api.md) |
| FR-17 | Publish an event for every reservation state change, at least once, in order per reservation. | [Events](../specs/08-events.md) |
| FR-18 | Reconciliation report comparing counters with the ledger. | [Admin API](../specs/09-admin-api.md) |

## 7. Assumptions

The brief leaves these open on purpose. Each is a decision; the ADR column names where it is argued.

| ID | Assumption | ADR |
| --- | --- | --- |
| A-1 | This service is the source of truth for **sellable** capacity. Physical capacity is entered through the admin API or a sync job. | [0001](../adr/0001-system-boundary.md) |
| A-2 | The service reserves only. Product services provision and call confirm, cancel and release. | [0001](../adr/0001-system-boundary.md) |
| A-3 | Product services call release when a service is deleted. A periodic reconciler is specified as an interface only. | [0001](../adr/0001-system-boundary.md) |
| A-4 | Divisible resources (CPU, RAM, disk) are counted per zone, IPs per region, GPUs per host. 10% of each zone is reserved as a fragmentation margin. | [0002](../adr/0002-resource-model.md) |
| A-5 | Never oversell. When the service's own database is unavailable it fails closed. | [0008](../adr/0008-failure-mode-and-regional-cells.md) |
| A-6 | Load figures are hypothetical; see the [load model](../architecture/load-model.md). | — |
| A-7 | Hold TTL is per product: default 15 minutes, range 2–60 minutes, one extension. | [0005](../adr/0005-synchronous-hold-with-lease.md) |
| A-8 | Overcommit is an `allocation_ratio` per inventory, set by operators. | [0002](../adr/0002-resource-model.md) |
| A-9 | The service counts units; it does not pick which IP or which GPU. | [0002](../adr/0002-resource-model.md) |
| A-10 | Requests are all-or-nothing. A rejection says which line was short. | [0004](../adr/0004-concurrency-control.md) |
| A-11 | Quotas are hard, per (customer, resource class, region). | [0007](../adr/0007-quota-in-the-ledger.md) |
| A-12 | Open holds count against quota. | [0007](../adr/0007-quota-in-the-ledger.md) |
| A-13 | No dedicated enterprise pools or priority classes in this version; the pool model allows them later. | [0024](../adr/0024-lean-data-model.md) |
| A-14 | Customers see a qualitative level and a fit-check; exact numbers are for internal and admin callers. | [0021](../adr/0021-shortage-responses-and-wait-queue.md) |
| A-15 | Availability 99.95%; hold p99 < 150 ms. | [0008](../adr/0008-failure-mode-and-regional-cells.md) |
| A-16 | The API gateway authenticates and passes trusted identity headers. Platform standards: PostgreSQL, RabbitMQ, Kubernetes. | [0014](../adr/0014-rest-api.md) |
| A-17 | Events are internal, published through the outbox. No customer webhooks. | [0009](../adr/0009-transactional-outbox.md) |
| A-18 | History stays online for 13 months, then is archived. | [0024](../adr/0024-lean-data-model.md) |
| A-19 | A request targets exactly one region. Multi-region bundles are several requests coordinated by the client. | [0008](../adr/0008-failure-mode-and-regional-cells.md) |
| A-20 | Waiting is opt-in, only for temporary shortages, for at most 15 minutes. | [0021](../adr/0021-shortage-responses-and-wait-queue.md) |

## 8. Non-functional requirements

| Area | Requirement |
| --- | --- |
| Correctness | No oversell under any concurrency; enforced by database constraints as well as code. |
| Latency | Hold and confirm p99 < 150 ms; capacity read p99 < 50 ms. |
| Throughput | 500 holds / s per regional cell sustained (design target; ~80× the estimated peak). |
| Availability | 99.95% on the hold and confirm path. |
| Durability | RPO 0 for confirmed reservations; RTO < 60 s. |
| Recovery | No in-memory state; any instance can be killed at any time. |
| Auditability | Every capacity change is an append-only ledger row with actor and reason. |
| Operability | Metrics for every threshold in the ADRs; reconciliation report. |
| Setup | An evaluator runs the whole system with one command (`make up`). |

## 9. Success metrics

| Metric | Target |
| --- | --- |
| Oversell incidents | 0 |
| Ledger/counter drift | 0 rows |
| Duplicate reservations from retries | 0 |
| Expired-hold release lag p99 | < 5 s |
| Hold p99 latency | < 150 ms |
| Share of rejections that are `CAPACITY_TEMPORARILY_UNAVAILABLE` and later succeed on retry | Tracked; no target |

## 10. Delivery phases

| Phase | Contents | Exit criterion |
| --- | --- | --- |
| 1. Core | Catalog, pools, inventories, submit / confirm / cancel / extend, idempotency, sweeper, effective-state reads, ledger | Concurrency, invariant and idempotency tests pass on real PostgreSQL |
| 2. Control | Quotas, fairness controls, opt-in wait, release, admin API, reconciliation report | Edge-case tests for quota and fairness pass |
| 3. Integration | Outbox relay to RabbitMQ, OpenTelemetry, Prometheus metrics, k6 load test, docs | `make up && make test && make demo` works on a clean machine |

## 11. Risks

| Risk | Mitigation |
| --- | --- |
| Hypothetical load figures are off by 100× | Pool splitting needs data changes only; measured by the load test |
| Fragmentation is invisible to counters | 10% reserved margin; release after failed provisioning; host-level pools when needed |
| Capacity return depends on product services calling release | Reconciler interface; drift metric |
| Global quotas across regions are not supported | Documented; revisit [ADR-0007](../adr/0007-quota-in-the-ledger.md) if required |
