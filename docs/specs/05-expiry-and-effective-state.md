# Spec 05 — Expiry and Effective State

Readers always see the state that is true **now**; the stored state catches up through the sweeper or the hold path's finalizer. Related: [ADR-0022](../adr/0022-two-state-machines-effective-state.md), [ADR-0005](../adr/0005-synchronous-hold-with-lease.md).

## 1. Principle

- Reads never write.
- A stored intermediate state past its deadline is reported as its final state, with `settled: false`.
- Some other process (sweeper or inline finalizer) stores the final state and returns the capacity.
- The only clock is the database's `now()`.

## 2. Effective status

| Stored status | Condition at read | Effective status | `reason_code` | `settled` |
| --- | --- | --- | --- | --- |
| `HELD` | `expires_at <= now()` | `EXPIRED` | `HOLD_EXPIRED` | false |
| `WAITING` | `wait_deadline <= now()` | `REJECTED` | `WAIT_TIMEOUT` | false |
| Any other | — | Stored status | Stored reason | true |

Defined once, as a view:

```sql
CREATE VIEW reservation_effective AS
SELECT r.*,
       CASE
         WHEN r.stored_status = 'HELD'    AND r.expires_at    <= now() THEN 'EXPIRED'
         WHEN r.stored_status = 'WAITING' AND r.wait_deadline <= now() THEN 'REJECTED'
         ELSE r.stored_status
       END AS effective_status,
       CASE
         WHEN r.stored_status = 'HELD'    AND r.expires_at    <= now() THEN 'HOLD_EXPIRED'
         WHEN r.stored_status = 'WAITING' AND r.wait_deadline <= now() THEN 'WAIT_TIMEOUT'
         ELSE r.reason_code
       END AS effective_reason_code,
       NOT (   (r.stored_status = 'HELD'    AND r.expires_at    <= now())
            OR (r.stored_status = 'WAITING' AND r.wait_deadline <= now())) AS settled
FROM reservations AS r;
```

All API reads use the view. `stored_status` is visible only to transition code. History filters by `effective_status`.

## 3. Write guards match the effective status

Every transition's `WHERE` clause is the effective-status condition, so a write can never contradict what a reader was told.

| Writer | Guard |
| --- | --- |
| confirm, cancel, extend on HELD | `stored_status = 'HELD' AND expires_at > now()` |
| expire | `stored_status = 'HELD' AND expires_at <= now()` |
| cancel, promote on WAITING | `stored_status = 'WAITING' AND wait_deadline > now()` |
| timeout | `stored_status = 'WAITING' AND wait_deadline <= now()` |

The guards for the same stored state are complementary in time: at no instant are both a client event and the timer legal. If both arrive together, the row lock serialises them and the second finds zero rows ([C-4](../edge-cases/edge-cases.md#1-concurrency-on-capacity)).

## 4. Sweeper

Runs in the `sweeper` role every second. Each step is its own transaction; several sweeper instances are safe because of `SKIP LOCKED`.

| Step | Selection | Effect |
| --- | --- | --- |
| Expire | `stored_status = 'HELD' AND expires_at <= now()` ordered by `expires_at`, `LIMIT 200 FOR UPDATE SKIP LOCKED` | Per [spec 04 §8](04-capacity-allocation.md#8-later-transitions): quotas, then inventories, sorted across the batch; `EXPIRED`; ledger `RETURN`; events |
| Time out | `stored_status = 'WAITING' AND wait_deadline <= now()`, `LIMIT 200 FOR UPDATE SKIP LOCKED` | `REJECTED`, `WAIT_TIMEOUT`; event |
| Promote | For each (region, product) that had capacity returned in this cycle: `stored_status = 'WAITING' AND wait_deadline > now()` ordered by `created_at`, `LIMIT 50 FOR UPDATE SKIP LOCKED` | Hold attempt per request under its own savepoint ([spec 04 §4](04-capacity-allocation.md#4-hold-transaction)); success → HELD with `expires_at = now + hold_ttl`; failure → stays WAITING |
| Purge keys | `idempotency_keys.expires_at < now()`, 1,000 per batch | Delete |
| Reconcile | Hourly | Report only ([spec 09 §6](09-admin-api.md#6-reconciliation-report)) |

A batch loops immediately while it returns a full batch, so a backlog drains at full speed. Counter changes are summed per (pool, class) across the batch, so 200 reservations touch only a handful of inventory and quota rows. The expected rate is in the thousands per second per instance, above the design target of 500 per second; the load test measures it.

## 5. Inline finalizer

The hold path finalizes expired holds on the pools it is short on, then retries once ([spec 04 §6](04-capacity-allocation.md#6-retry-after-finalizing-expired-holds)). Together with effective reads, this means a stopped sweeper causes neither oversell nor undersell; it only delays when the final state is stored ([T-1](../edge-cases/edge-cases.md#4-time-and-expiry)).

## 6. Capacity reads

Capacity levels and fit-check use effective free capacity: held quantities of reservations past `expires_at` are added back to free. Computed with a join on `reservation_lines` and the partial index on `reservations (expires_at) WHERE stored_status = 'HELD'`. Expected rows: release lag × expiry rate, about 30 at the estimated peak and 2,500 at the design target.

## 7. Clock rules

- Only `now()` of the current transaction is used; the host clock never enters a decision ([T-2](../edge-cases/edge-cases.md#4-time-and-expiry)).
- `now()` is the transaction start time. With `statement_timeout = 2s`, a transaction cannot run long enough for this to matter ([T-5](../edge-cases/edge-cases.md#4-time-and-expiry)).
- `expires_at` is computed once, at hold time, from the product's TTL then. Changing the TTL affects only new holds ([T-6](../edge-cases/edge-cases.md#4-time-and-expiry)).

## 8. Metrics

`capacity_expiry_lag_seconds` (now − `expires_at` at the moment of storing EXPIRED), `capacity_unsettled_holds` (gauge), `capacity_sweeper_batch_duration_seconds`. Alert when lag p99 > 5 s for 5 minutes ([spec 10](10-observability.md)).
