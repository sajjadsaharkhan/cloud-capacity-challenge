# Glossary

Every project-specific word used in this guide, in plain language. Terms are grouped by topic; each one links to the chapter where it matters most.

## Capacity and resources

| Term | Meaning |
| --- | --- |
| **Resource class** | A kind of capacity: `Vcpu` (millicores), `RamMb` (megabytes), `DiskGb` (gigabytes), `Ipv4` (addresses), `Gpu` (cards). Always counted as whole integers in that base unit |
| **Scope** | The level a class is counted at: **zone** for CPU, RAM, disk; **region** for IPv4; **host** for GPU |
| **Region / zone** | A data-centre area (`ir-thr`, `ir-tbz`, `ir-ahv`) and a part of it (`ir-thr-1`). Every request targets exactly one region |
| **Pool** | One unit of capacity bookkeeping: a region, a zone, or a single host. Has an admin state (`Registered`, `Available`, `Offline`, `Retired`) |
| **Inventory** | One row per (pool, resource class) holding `total`, `reserved`, `allocation_ratio`, size limits, and the two counters `held` and `used` |
| **Total** | Physical units in the pool for that class |
| **Reserved (margin)** | Units deliberately never sold, about 10% of a zone, to absorb **fragmentation** |
| **Allocation ratio** | Overcommit factor (≥ 1). A ratio of 4 sells each physical core four times |
| **Sellable capacity** | `floor((total − reserved) × allocation_ratio)`. The most that may ever be held plus used |
| **Held** | Units on a short-term hold (a lease), not yet confirmed |
| **Used** | Units confirmed and allocated to a live service |
| **Free** | `sellable − held − used`, only on an `Available` pool |
| **Effective free** | Free plus units still counted as held by holds whose deadline has **already passed** |
| **Capacity level** | What customers see instead of numbers: `Available`, `Limited` (below 10% free), `Unavailable` |
| **Fit-check** | An advisory "would this fit right now, including my quota?" call. It never reserves anything |
| **Fragmentation** | Counters say 100 cores are free, but no single host has 16 in one place. The counter model cannot see this; the margin covers it |
| **Hot row** | An inventory row that many requests update at the same moment. Its lock is the real throughput ceiling |
| **Round trip** | One request from the service to PostgreSQL and its reply. A hold takes 9 (down from 28); the hot row is locked only for the last `UPDATE` and the commit |

See [Data model](#05-data-model) and [Core logic](#07-core-logic).

## Reservations

| Term | Meaning |
| --- | --- |
| **Reservation** | One customer request for capacity, with its own state machine. Called a *request* in the specs |
| **Line** | One (resource class, quantity) pair inside a reservation. A reservation has 1–16 lines |
| **Hold** | Taking capacity aside for a short time, atomically for all lines. Result: status `Held` |
| **Lease / TTL** | The hold's lifetime (default 15 minutes, per product). It ends at `expires_at` |
| **Confirm** | Turning a hold into an allocation (`held → used`) before the lease ends. The product service then provisions |
| **Cancel** | Giving back a hold or a wait voluntarily |
| **Extend** | Adding one more TTL to a hold. Allowed **once** |
| **Expire** | The timer event: a hold past `expires_at` becomes `Expired` and its capacity returns |
| **Release** | Freeing *confirmed* capacity when the service is deleted or provisioning failed (`used` drops). Called by product services |
| **Allocation state** | `Active` or `Released`, only on a confirmed reservation. Release changes this, not the reservation status |
| **Wait / Waiting** | Opt-in (`onShortage: Wait`) parking for a **temporary** shortage, up to 15 minutes. Holds nothing |
| **Promote** | Moving a waiting reservation to `Held` when capacity comes back, oldest first |
| **Timeout** | The timer event for a wait past its deadline: it becomes `Rejected` (`WaitTimeout`) |
| **All-or-nothing** | Either every line is held or none is. Implemented with a database savepoint |
| **Client reference** | An optional id from the client (e.g. order number), unique among open and confirmed reservations. A second guard against duplicates |
| **Try-confirm-cancel** | The contract with product services: hold, provision **after** confirm, release on deletion |

See [State machines](#06-state-machines).

## Shortage answers

| Code | Meaning | Retry helps? |
| --- | --- | --- |
| **ExceedsRegisteredCapacity** | Bigger than any pool could ever provide | Never |
| **CapacitySoldOut** | Fits in theory, but confirmed allocations fill it | Not on a known timescale |
| **CapacityTemporarilyUnavailable** | Short only because of open holds or an offline pool | Yes, within one TTL (`Retry-After`) |
| **QuotaExceeded** | Over the customer's contract | Only after their own usage drops |
| **PoolShareExceeded** | Over the customer's share of a scarce pool | Only after their own usage drops |

## State and time

| Term | Meaning |
| --- | --- |
| **DFA** | Deterministic finite automaton: a fixed set of states and a table of legal moves. Every (state, event) pair has exactly one result or is refused |
| **Request DFA** | The reservation's state machine: `New → Waiting / Held → Confirmed / Rejected / Cancelled / Expired` |
| **Resource DFA** | The state machine of one capacity unit: `Registered → Available → Held → Allocated → …`, stored as counters, not rows |
| **Stored status** | The status column in the database |
| **Effective status** | What is true **now**: a stored `Held` past its deadline *is* `Expired`. All reads return this |
| **Settled** | `true` when stored and effective status agree; `false` while a past-deadline reservation waits for the sweeper |
| **Guard** | The condition that makes a transition legal, e.g. `now < expires_at` for confirm |
| **Database clock** | The transaction's `now()` from PostgreSQL. The only clock any decision uses; server clocks are ignored |
| **Version** | A counter on each reservation, +1 per transition. Used for safe updates and for ordering events |

## Background roles

| Term | Meaning |
| --- | --- |
| **Role** | One of three ways the same image runs: `api`, `sweeper`, `relay` |
| **Sweeper** | The background worker that runs every second. It **expires** holds past their lease, **times out** waits, **promotes** waiters, **purges** old idempotency keys, and computes the **hourly reconciliation**. Works in batches of 200 with `SKIP LOCKED`, so several sweepers can run safely |
| **Inline finalizer** | The hold path's own mini-sweeper: when a submit is short only because expired holds have not been swept yet, it finalizes those holds and retries **once** |
| **Relay** | The background worker that reads unpublished rows from the outbox and publishes them to RabbitMQ, then marks them published |
| **Outbox (transactional)** | Writing the event row in the **same transaction** as the state change, so an event exists if and only if the change committed. Here the outbox is the `reservation_events` table, which is also the history |
| **At-least-once delivery** | An event may be published twice (e.g. the relay crashed after publishing). Consumers de-duplicate by event id |
| **Migrator** | The one-off `migrate` command that applies database migrations (DbUp) before the roles start |

See [Core logic](#07-core-logic).

## Correctness techniques

| Term | Meaning |
| --- | --- |
| **Conditional update** | `UPDATE … SET held = held + q WHERE held + used + q <= sellable`. One row changed means success; zero means shortage. No read-then-write gap |
| **CHECK constraint** | A rule the database itself enforces on every row (e.g. `held + used <= sellable`). The last line of defence if code is wrong |
| **Global lock order** | Every writer locks rows in the same order: reservation → quota → inventory, each sorted. This makes deadlocks impossible |
| **Savepoint** | A marker inside a transaction that can be rolled back to without aborting the whole transaction. Used to try the next zone or host |
| **`FOR UPDATE SKIP LOCKED`** | "Lock these rows, and skip any another worker already locked." Lets several sweepers or relays share work without waiting |
| **`READ COMMITTED`** | The PostgreSQL isolation level used. Combined with conditional updates it is enough, without the retries of `SERIALIZABLE` |
| **Ledger** | The append-only table `ledger_entries`. Every change to `held` or `used` writes one row (`HOLD`, `COMMIT`, `RETURN`, `RELEASE`, `ADJUST`). **The source of truth** |
| **Counter** | `held` and `used` on inventories and quotas. A fast projection of the ledger, updated in the same transaction |
| **Reconciliation** | Recomputing counters from the ledger and reservations and reporting any difference |
| **Drift** | A difference found by reconciliation. Target: 0 rows |
| **ADJUST** | A ledger row written by an operator to correct drift. The only way to change a counter by hand |
| **Fail closed** | When unsure (e.g. the database is unreachable), refuse rather than risk overselling |
| **Negative cache (sold-out)** | Remembering for 1 second that a pool is sold out, so further requests skip the hot row and are refused cheaply |

## Retries

| Term | Meaning |
| --- | --- |
| **Idempotency** | Sending the same request twice has the same effect as sending it once |
| **Idempotency key** | The `Idempotency-Key` header. Stored per customer with the response, in the same transaction as the work |
| **Fingerprint** | SHA-256 of the operation name and the request body. A reused key with a different fingerprint is refused (`422`) |
| **Replay** | Returning the stored response for a repeated key, marked `Idempotent-Replayed: true` |
| **Retry-After** | A header telling the client how many seconds to wait before retrying |

See [Idempotency](#08-idempotency).

## Limits and fairness

| Term | Meaning |
| --- | --- |
| **Quota** | A customer's contract limit per (resource class, region). Open holds count against it |
| **Token bucket** | The rate-limit algorithm: a bucket of N tokens refilled at a fixed rate; each request takes one, an empty bucket means `429` |
| **Rate group** | Which bucket an endpoint uses: `submit`, `fitcheck`, `reads`, `transitions` |
| **Hold limit** | Maximum active holds per customer and product |
| **Waiting limit** | Maximum waiting reservations per customer and product (4) |
| **Pool share cap** | On scarce products, the maximum share of one pool a customer may hold (25%) |
| **Cooldown** | After 5 expired holds in an hour, a 10-minute pause on new holds for that product |
| **Hoarding** | Taking holds only to keep capacity from others. The share cap, hold limit and cooldown exist against it |

See [Rate limits and fairness](#09-rate-limits-fairness).

## Actors

| Term | Meaning |
| --- | --- |
| **Gateway** | The API gateway in front of this service. It authenticates and passes trusted identity headers |
| **Customer** | `X-Actor-Kind: Customer`. A user of the panel or their automation |
| **Service** | `X-Actor-Kind: Service`. An internal product service (VM, DBaaS, IP) acting for a customer; the only caller of release besides admins |
| **Admin** | `X-Actor-Kind: Admin`. A capacity operator using the admin API |
| **Product service** | The system that actually builds the VM or IP. It confirms holds and releases capacity |

## Architecture and code

| Term | Meaning |
| --- | --- |
| **Modular monolith** | One codebase and one deployable image, split into modules inside, rather than microservices |
| **Clean architecture** | Layers where inner ones never depend on outer ones: Domain ← Application ← Infrastructure, Presentation |
| **UseCase** | One business operation, and the only code allowed to own a transaction |
| **Flow** | A sequence across two or more aggregates, run inside the UseCase's transaction (e.g. `ExpireHeldFlow`, shared by submit and the sweeper) |
| **Operation** | Work on one aggregate through its repository |
| **Port** | An interface the Application layer defines and Infrastructure implements (e.g. the capacity ledger repository) |
| **Aggregate** | A cluster of data changed as a unit; here a reservation with its lines |
| **ApiResult envelope** | The common JSON shape `{ isSuccess, data, error, traceId }` of every response |
| **Regional cell** | A full copy of the system (database plus roles) serving exactly one region |
| **ADR** | Architecture Decision Record: one decision, the rejected options and when to revisit it |

## Operations and scale

| Term | Meaning |
| --- | --- |
| **RPO** | Recovery Point Objective: how much committed data may be lost in a failure. Target 0 |
| **RTO** | Recovery Time Objective: how long until service is back. Target under 60 s |
| **Synchronous replication** | A commit is acknowledged only after a standby has it, so a primary crash loses nothing |
| **Failover** | Promoting a standby when the primary dies |
| **Escrow** | Splitting one pool's capacity into independent slices so they can be updated in parallel. A future scale step |
| **CDC** | Change Data Capture: streaming database changes. A future replacement for the relay at high event volume |
| **p50 / p99** | The latency that 50% / 99% of requests stay under |
| **k6** | The load-testing tool used in `load/k6/` |
| **Testcontainers** | A library that starts a real PostgreSQL in Docker for each test run |
