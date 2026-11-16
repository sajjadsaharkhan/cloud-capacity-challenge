# Edge cases

I listed **61 edge cases in seven groups**. **18 are critical**: unhandled, they sell capacity twice, lose a confirmed reservation, or expose one customer's data to another.

Each row answers three questions: *what happens, what breaks if nobody handles it, and what the system does.* The full table is in `docs/edge-cases/edge-cases.md`.

| Group | Code | Cases | Critical |
| --- | --- | --- | --- |
| Concurrency on capacity | C | 11 | 5 |
| Retries and unknown outcomes | R | 9 | 3 |
| Abuse and fairness | A | 8 | 1 |
| Time and expiry | T | 7 | 1 |
| Failure and recovery | F | 11 | 4 |
| Quotas and contracts | Q | 6 | 1 |
| Data, model and access | D | 9 | 3 |
| **Total** | | **61** | **18** |

**Severity.** *Critical:* oversell, loss of a confirmed reservation, or a leak between customers. *Major:* capacity locked for no reason, a wrong answer, or overload. *Minor:* behaviour must be defined, nothing is lost.

## The 18 critical cases

### Concurrency

| Code | Case | What the system does |
| --- | --- | --- |
| C-1 | Two requests for the **last unit** | Conditional `UPDATE`: exactly one becomes HELD. The `CHECK` is the last defence |
| C-3 | The third line of a four-line request is short | One transaction with a savepoint rolls back. **Nothing stays held** |
| C-4 | **Confirm and expiry at the same instant** | Complementary guards on `expires_at` plus the row lock. Exactly one applies, the other gets 409 |
| C-6 | Two concurrent confirms (double click) | Guard `stored_status = 'HELD'`. The second updates zero rows and returns the current resource |
| C-7 | Sweeper and inline finalizer both expire one hold | `SKIP LOCKED` plus the status guard. Only one takes the row |

### Retries

| Code | Case | What the system does |
| --- | --- | --- |
| R-1 | Response lost after commit, client resends | The `Idempotency-Key` returns the stored response |
| R-2 | Two concurrent requests with the same key | The primary key makes the second **wait** and then replay |
| R-7 | Provisioning outlasts the TTL, confirm arrives late | The contract is *confirm first, then provision*. Late confirm gets `409 RESERVATION_EXPIRED` |

### Abuse, time, quota

| Code | Case | What the system does |
| --- | --- | --- |
| A-1 | One customer holds everything | Holds count against quota, active-hold cap, 25% pool-share cap on scarce products |
| T-2 | API instance clocks differ | Only the **database `now()`** is used in any condition |
| Q-1 | Two parallel requests, each in quota but not together | Same conditional `UPDATE` on the quota row, same transaction |

### Failure

| Code | Case | What the system does |
| --- | --- | --- |
| F-1 | API instance dies mid-transaction | The database rolls back. The client retries with the same key |
| F-2 | Instance dies after commit, before responding | The retry returns the stored response |
| F-3 | Database primary dies | Synchronous replication (RPO 0); 30–60 s of `503` + `Retry-After`; reads from cache marked `stale` *(design, needs HA infrastructure)* |
| F-5 | Old primary keeps writing after failover | The operator fences it; clients write only through the operator's service *(design)* |

### Data and access

| Code | Case | What the system does |
| --- | --- | --- |
| D-4 | A customer reads or cancels another's reservation | `customer_id` in every query; `404`, so even existence is hidden |
| D-5 | Release twice, or on a non-confirmed reservation | Release acts only on an ACTIVE allocation. A repeat returns the same result |
| D-7 | The owning service wrongly releases a live service's capacity | **Accepted.** This service cannot see real infrastructure. Fix: reconcile against hypervisor inventory |

## A few major cases worth knowing

| Code | Case | System behaviour |
| --- | --- | --- |
| C-2 | Two multi-line requests need shared rows in opposite order | Global lock order, rows sorted: no deadlock cycle can form |
| C-10 | 300 req/s on one scarce pool | 1 s sold-out cache, per-customer rate limit, `lock_timeout` 500 ms → 503 and retry |
| C-11 | Operator lowers `total` below `held + used` | The `CHECK` refuses; with `force`, the pool goes OFFLINE and drains |
| T-1 | Sweeper stopped for an hour | Reads show effective status; the hold path finalizes expired holds; the backlog drains in seconds on restart |
| T-7 | Stored HELD but deadline passed | Effective `EXPIRED` with `settled: false` |
| F-6 | RabbitMQ down for an hour | Reservations unaffected; events wait in the table |
| F-11 | Ledger and counters disagree | Reconciliation reports drift; fixes only as `ADJUST` ledger rows |
| Q-2 | A contract shrinks below usage | Allowed; existing holds stay; new ones fail |

## Accepted risks (seen, bounded, not solved)

| Code | What I accept | Why | If it ever matters |
| --- | --- | --- | --- |
| C-9 | A new request can take capacity before an older waiter | Reserving capacity for waiters can deadlock them. The 15-minute deadline bounds the harm | A real queue with priority and fair queueing |
| R-4 | A retry after 24 h without `client_reference` makes a new reservation | Keeping keys forever is costly. Quota and TTL bound the error | Make `client_reference` mandatory |
| A-4 | Several accounts bypass per-customer caps | Authentication is out of scope | Per-organisation caps |
| D-7 | Wrong release by the owning service | Cannot see real infrastructure | Reconcile with the hypervisor |
| F-10 | After a backup restore, the outside world is ahead of the ledger | Rare with synchronous replication | Reconcile with product services |
| – | **Fragmentation**: counters say capacity exists, but no host fits | Counter model; a 10% margin and release on failure cover it | Host-level pools, as for GPUs |

## How the critical cases are proven

Ten test scenarios on a real PostgreSQL cover all 18 critical cases **except F-3 and F-5** (they need HA infrastructure) **and D-7** (accepted). See [Testing](#12-testing).
