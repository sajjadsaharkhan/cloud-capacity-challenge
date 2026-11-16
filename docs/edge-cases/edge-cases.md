# Edge Cases

61 edge cases in seven groups. 18 are **critical**: unhandled, they sell capacity twice, lose a confirmed reservation, or expose one customer's data to another. The design is in the [specs](../specs/README.md) and [ADRs](../adr/README.md); this file states what happens in each case, what breaks without handling, and what the system does.

| Group | Code | Cases | Critical |
| --- | --- | --- | --- |
| [Concurrency on capacity](#1-concurrency-on-capacity) | C | 11 | 5 |
| [Retries and unknown outcomes](#2-retries-and-unknown-outcomes) | R | 9 | 3 |
| [Abuse and fairness](#3-abuse-and-fairness) | A | 8 | 1 |
| [Time and expiry](#4-time-and-expiry) | T | 7 | 1 |
| [Failure and recovery](#5-failure-and-recovery) | F | 11 | 4 |
| [Quotas and contracts](#6-quotas-and-contracts) | Q | 6 | 1 |
| [Data, model and access](#7-data-model-and-access) | D | 9 | 3 |

Severity:

- **Critical:** oversell, loss of a confirmed reservation, or a leak between customers.
- **Major:** capacity locked for no reason, a wrong answer to the customer, or overload.
- **Minor:** behaviour must be defined, but nothing is lost.

## 1. Concurrency on capacity

Solved in the database, not in application code: conditional updates, a fixed lock order, and guards that cannot both be true ([spec 04](../specs/04-capacity-allocation.md), [ADR-0004](../adr/0004-concurrency-control.md)).

| Code | Case | Without handling | System behaviour | Severity |
| --- | --- | --- | --- | --- |
| C-1 | Two concurrent requests for the last unit | Both succeed; oversell | Conditional `UPDATE`; exactly one becomes HELD, the other gets a shortage code. `CHECK` constraint as last defence | Critical |
| C-2 | Two multi-line requests need shared rows in different orders | Deadlock and timeouts | Rows always locked in global order (reservation → quota → inventory, sorted); no deadlock cycle can form | Major |
| C-3 | In a four-line request, the third line is short | First two lines stay held; capacity leaks | One transaction with a savepoint; rolled back; nothing stays held | Critical |
| C-4 | Confirm and expiry arrive at the same instant | Request CONFIRMED while its capacity is also returned; sold twice | Complementary guards on `expires_at` plus the row lock; exactly one applies, the other gets 409 | Critical |
| C-5 | Confirm and cancel at the same time from two clients | Final state undefined | Row lock; first wins; second gets 409 with the effective status | Major |
| C-6 | Two concurrent confirms (double click or retry) | `held` moved to `used` twice | Guard `stored_status = 'HELD'`; the second affects zero rows and returns the current resource | Critical |
| C-7 | Sweeper and inline finalizer both try to expire one hold | Capacity returned twice; counters wrong | `SKIP LOCKED` and the `stored_status` guard; only one takes the row | Critical |
| C-8 | Several sweeper instances at once | Duplicate work or lock waits | Each takes its own batch with `SKIP LOCKED` | Minor |
| C-9 | Freed capacity is wanted by a waiting request and a new one | The older waiter is overtaken | No capacity is reserved for waiters; whichever transaction comes first wins. Accepted ([§8](#8-accepted-risks)) | Minor |
| C-10 | 300 requests/s on one scarce pool | Lock queue, timeouts, more retries | 1-second sold-out negative cache skips the hot-row update; per-customer rate limit; `lock_timeout` 500 ms → 503 with retry | Major |
| C-11 | Operator lowers `total` or `allocation_ratio` while holds are open | Sellable capacity falls below `held + used` | `CHECK` rejects the change; with `force` the pool goes OFFLINE so no new holds land until it drains | Major |

## 2. Retries and unknown outcomes

Machine clients retry on every timeout; every mutating call must give the same result when repeated ([spec 06](../specs/06-idempotency.md)).

| Code | Case | Without handling | System behaviour | Severity |
| --- | --- | --- | --- | --- |
| R-1 | Response lost after commit; client sends again | Two reservations for one intent | `Idempotency-Key`; the retry gets the stored response | Critical |
| R-2 | Two concurrent requests with the same key | Both run | Primary key on (customer, key); the second waits and replays the first's result | Critical |
| R-3 | Same key, different body | Response of another request returned | Body fingerprint compared; `422 IDEMPOTENCY_KEY_REUSED` | Major |
| R-4 | Retry after 24 hours, when the key has been purged | Duplicate reservation | `client_reference` is unique among open and confirmed reservations; without it, accepted ([§8](#8-accepted-risks)) | Minor |
| R-5 | Client retries without a key | Duplicate holds | Key is mandatory (`400`); quota, hold limit and TTL bound any damage | Major |
| R-6 | Retry storm after an outage | System overloaded again | `Retry-After`, per-customer rate limits; idempotency makes retries harmless | Major |
| R-7 | Provisioning takes longer than the TTL; confirm arrives after expiry | Service built while its capacity was freed and possibly resold | Contract: confirm first, then provision. Late confirm gets `409 RESERVATION_EXPIRED` and the product service must not continue | Critical |
| R-8 | Confirm succeeds but provisioning fails | Capacity stays ALLOCATED with no service | Product service calls release with `PROVISIONING_FAILED`; reconciler interface as backstop | Major |
| R-9 | Duplicate or out-of-order event reaches a consumer | Consumer applies an old state | `event_id` for deduplication; reservation `version` lets consumers drop older events | Major |

## 3. Abuse and fairness

A hold is free, so without limits it is the cheapest way to take a scarce product off the market ([spec 07](../specs/07-quotas-and-fairness.md)).

| Code | Case | Without handling | System behaviour | Severity |
| --- | --- | --- | --- | --- |
| A-1 | One customer holds all capacity with back-to-back requests | Nobody else gets anything | Holds count against quota; cap on active holds; cap on one customer's share of a scarce pool (default 25%) | Critical |
| A-2 | Hold-and-abandon loop keeps capacity permanently locked | Free capacity never reachable | Rate limit on holds; after 5 expiries in an hour, 10-minute cooldown for that product | Major |
| A-3 | Repeated extension of one hold | Endless hold | One extension only, capped | Major |
| A-4 | Several accounts to bypass per-customer limits | Limits ineffective | Outside this service; identity is controlled at the gateway. Accepted ([§8](#8-accepted-risks)) | Major |
| A-5 | Probing exact inventory with repeated fit-checks | Business information leaked | Only qualitative levels shown; fit-check is rate-limited and never returns quantities | Minor |
| A-6 | Filling the wait queue | Queue useless for others | At most 4 waiting requests per customer and product; WAITING holds nothing | Minor |
| A-7 | Very large request, or hundreds of lines | Heavy transaction, long locks | `max_unit` per class and at most 16 lines; rejected in validation, before the database | Minor |
| A-8 | A buggy internal service holds without limit | Customers' capacity consumed | Service identity has its own quota and rate limit | Major |

## 4. Time and expiry

The only valid clock is the database's; status is always computed at read time ([spec 05](../specs/05-expiry-and-effective-state.md)).

| Code | Case | Without handling | System behaviour | Severity |
| --- | --- | --- | --- | --- |
| T-1 | Sweeper stopped for an hour | Expired holds keep capacity | Reads return the effective status; the hold path finalizes expired holds on its pools. On restart the backlog (~22k rows at the hypothetical peak) drains in seconds | Major |
| T-2 | API instances' clocks differ | One instance confirms an expired hold | Only the database `now()` is used in conditions | Critical |
| T-3 | Database clock jumps a few hundred ms after failover | Deadlines slightly early or late | TTLs are minutes; a jump only shifts the expiry moment and cannot oversell in either direction | Minor |
| T-4 | Event arrives exactly at `expires_at` | Undefined behaviour | Explicit inequalities: confirm only when `now < expires_at`; expire when `now >= expires_at` | Major |
| T-5 | Confirm transaction starts before the deadline and commits after it | Confirm after the deadline | Transaction start time is the reference; `statement_timeout` 2 s bounds the gap | Minor |
| T-6 | Product TTL changes while holds are open | Existing holds suddenly expire or extend | `expires_at` is fixed when the hold is created; the new policy applies to new holds only | Minor |
| T-7 | Stored status is HELD but the deadline has passed | Customer sees a dead hold as live | Effective status `EXPIRED` with `settled: false` | Major |

## 5. Failure and recovery

There is no in-memory state; whatever did not commit did not happen, and whatever did can be asked about with a retry ([ADR-0008](../adr/0008-failure-mode-and-regional-cells.md)).

| Code | Case | Without handling | System behaviour | Severity |
| --- | --- | --- | --- | --- |
| F-1 | API instance dies mid-transaction | Half-written reservation | Database rolls back; client retries with the same key | Critical |
| F-2 | Instance dies after commit, before responding | Client does not know the result and reserves again | Retry with the same key returns the stored response | Critical |
| F-3 | Database primary dies | Confirmed reservation lost | Synchronous replication, RPO 0; 30–60 s of writes return 503 with `Retry-After`; reads continue from cache marked `stale` | Critical |
| F-4 | Synchronous standby unreachable | Commits stall | Quorum of one out of two standbys; with both gone, writes stop deliberately rather than replicate asynchronously | Major |
| F-5 | Old primary keeps accepting writes after failover | Split history; oversell | The operator fences the old primary; clients write only through the operator's read-write service | Critical |
| F-6 | RabbitMQ down for an hour | Reservations stop, or events are lost | Reservations do not depend on the broker; events wait in `reservation_events` (~70k rows at the hypothetical peak) and are sent later | Major |
| F-7 | Relay dies after publishing, before marking rows | Event published twice | At-least-once delivery; consumers deduplicate | Minor |
| F-8 | Connection pool exhausted | Requests wait indefinitely | Bounded pool with a short acquire timeout; fast 503; PgBouncer in front of the database | Major |
| F-9 | A zone goes down with open holds and allocations | New holds land on dead capacity | Pool goes OFFLINE and takes no new holds; existing holds are confirmed or expire, and returned units land in OFFLINE | Major |
| F-10 | Restore from a backup taken earlier | Services exist whose reservations are no longer recorded | Ledger and counters come back consistent with each other; mismatch with the outside world is reported by the reconciler. With RPO 0, this path is a last resort | Major |
| F-11 | Ledger and counters disagree (bug or manual change) | Shown capacity is wrong | Reconciliation reports drift; corrections only as `ADJUST` ledger rows, never direct updates | Major |

## 6. Quotas and contracts

The quota uses the same logic as capacity, on a row per customer, in the same transaction ([ADR-0007](../adr/0007-quota-in-the-ledger.md)).

| Code | Case | Without handling | System behaviour | Severity |
| --- | --- | --- | --- | --- |
| Q-1 | Two parallel requests from one customer, each within quota but not together | Customer exceeds the contract | Same conditional `UPDATE` on the quota row in the same transaction | Critical |
| Q-2 | Contract shrinks below current usage | Undefined behaviour | Allowed; existing holds and allocations stay; new holds are rejected until usage drops under the limit | Major |
| Q-3 | No quota row for a new customer or product | Error, or unlimited usage | Default quota for the class created with `INSERT … ON CONFLICT`; a missing row never means unlimited | Major |
| Q-4 | Customer suspended while holding open reservations | Capacity locked until expiry | The gateway stops issuing identity; `cancel-open` admin call cancels holds and waits. Allocations belong to billing, out of scope | Major |
| Q-5 | Shortage is the quota, but the customer asked to wait | Pointless wait | Waiting is only for capacity shortage; quota shortage is rejected immediately | Minor |
| Q-6 | The customer's own expired holds fill their quota | Customer rejected for no reason | Inline finalizer settles the customer's expired holds on the short pools and retries | Major |

## 7. Data, model and access

Invalid input is rejected before the database, and every query is scoped to the customer.

| Code | Case | Without handling | System behaviour | Severity |
| --- | --- | --- | --- | --- |
| D-1 | Zero, negative, below `min_unit` or not a multiple of `step_size` | Wrong counters | Validation, `422`; integer base units (MB, millicore) | Major |
| D-2 | Same class twice in one request | Two locks on one row | Rejected in validation; primary key on (reservation, class) | Minor |
| D-3 | Unknown or retired region, zone or product | Unclear error | `422` with a machine-readable code; a retired product takes no new holds, but existing holds can complete | Minor |
| D-4 | Customer reads, confirms or cancels another customer's reservation | Leak or sabotage across customers | Customer id in every query; `404`, so even existence is not revealed | Critical |
| D-5 | Release twice, or for a reservation that is not CONFIRMED | Capacity freed twice | Release applies only to an ACTIVE allocation; repeat returns the same result; invalid state gets 409 | Critical |
| D-6 | Release never arrives | Capacity ALLOCATED forever | Periodic reconciler finds orphaned allocations; in the delivered version only its interface exists | Major |
| D-7 | Release for a service that is still running | Capacity in use sold again | Only the owning service identity may release; trusting its claim is accepted ([§8](#8-accepted-risks)) | Critical |
| D-8 | History paginated while new rows arrive | Duplicate or missing rows | Cursor pagination on (`created_at`, `id`) | Minor |
| D-9 | Next month's ledger partition does not exist | Ledger insert fails, so every hold fails | Not applicable to the delivered version (no partitions). On the growth path, partitions are created three months ahead with a default partition | Major |

## 8. Accepted risks

Six cases are bounded rather than solved, and stated here so the evaluator knows they were seen.

| Code | What we accept | Why | If it matters |
| --- | --- | --- | --- |
| C-9 | A new request can take capacity before an older waiter | Reserving capacity for waiters deadlocks them; the 15-minute deadline bounds the harm | A real queue with priority and fair queueing |
| R-4 | A retry after 24 hours without `client_reference` creates a new reservation | Keeping keys forever is costly; quota and TTL bound the error | Make `client_reference` mandatory |
| A-4 | Several accounts bypass per-customer caps | Authentication is out of scope | Caps per organisation with hierarchical quotas |
| D-7 | The owning service can wrongly release capacity of a live service | This service cannot see real infrastructure | Reconcile against hypervisor inventory |
| F-10 | After a backup restore, the outside world is ahead of the ledger | Rare with synchronous replication; a full fix needs every product service | Automatic reconciliation with product services |
| — | Fragmentation: counters say capacity exists but no host fits | Counter model; the 10% margin and release on provisioning failure cover it | Host-level pools, as for GPUs |

## 9. Tests that prove the critical cases

Ten tests on real PostgreSQL cover all 18 critical cases except F-3 and F-5, which need the HA infrastructure, and D-7, which is accepted. Details in [spec 11](../specs/11-testing.md).

| # | Test | Cases | What it proves |
| --- | --- | --- | --- |
| 1 | 1,000 parallel requests on a pool with capacity 100 | C-1, C-3, Q-1 | Exactly 100 HELD; `held` never exceeds capacity |
| 2 | Multi-line requests with random line order | C-2 | Zero deadlocks |
| 3 | Confirm and sweeper together on holds expiring now | C-4, C-6, C-7, T-2, T-4 | Each hold has exactly one final state; ledger equals counters |
| 4 | Each call repeated with the same key, serial and parallel | R-1, R-2, R-3, F-2 | One reservation, one response |
| 5 | API instance killed under load | F-1, F-2 | No duplicate or partial reservations after retries |
| 6 | Sweeper stopped while load continues | T-1, T-7, Q-6 | Effective status correct; new holds succeed using expired capacity |
| 7 | Customer A with customer B's ids | D-4 | Every response is 404 |
| 8 | Double release, release of a non-confirmed reservation, late confirm | D-5, R-7 | Capacity returns once; late confirm gets 409 |
| 9 | One customer's hold loop on a scarce product | A-1, A-2, A-3 | Share cap, hold limit and cooldown apply |
| 10 | Invariant check after every test, plus k6 on one pool | F-11, C-10 | Drift is zero; hot-row ceiling measured |
