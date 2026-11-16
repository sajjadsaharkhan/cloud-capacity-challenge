# Spec 04 — Capacity Allocation

How a submit becomes a hold: validation, pool selection, the hold transaction, shortage classification, and the counter changes of every later transition. Related: [ADR-0004](../adr/0004-concurrency-control.md), [ADR-0003](../adr/0003-ledger-and-counters.md), [ADR-0021](../adr/0021-shortage-responses-and-wait-queue.md), [database](../database/database.md).

## 1. Definitions

| Term | Definition |
| --- | --- |
| Sellable capacity | `floor((total − reserved) × allocation_ratio)` per inventory row |
| Free | `sellable − held − used` on a pool whose `admin_state = AVAILABLE` |
| Effective free | Free plus held quantities of reservations whose `expires_at <= now()` but are still stored as HELD |
| Registered capacity (for classification) | Sellable capacity of pools in `AVAILABLE` or `OFFLINE` |

## 2. Validation

Before any lock, in this order; the first failure is returned.

1. Identity and `Idempotency-Key` present ([spec 06](06-idempotency.md)).
2. Body shape; 1–16 lines; each class unique.
3. Product exists and is `ACTIVE`; every class is in the product's `resource_classes` → else `PRODUCT_NOT_AVAILABLE`.
4. Region exists; zone, if given, belongs to it.
5. Each quantity is a positive integer, `>= min_unit`, `<= max_unit`, a multiple of `step_size` → else `VALIDATION_FAILED`.
6. Fairness pre-checks ([spec 07](07-quotas-and-fairness.md)): cooldown, active-hold limit, waiting limit.

## 3. Pool selection

Each resource class has a scope from configuration:

| Scope | Classes | Pool chosen |
| --- | --- | --- |
| `ZONE` | `VCPU`, `RAM_MB`, `DISK_GB` | The zone pool. All zone-scoped lines of one request use the same zone. |
| `REGION` | `IPV4` | The region pool |
| `HOST` | `GPU` | One host pool in the chosen zone |

Algorithm, using the cached capacity snapshot (≤ 2 s old):

1. **Zone.** If the request names a zone, use it. Otherwise rank the region's zones by the smallest ratio `effective_free / requested` across zone-scoped lines, highest first.
2. **Host.** For host-scoped lines, rank hosts in the zone by best fit: the smallest effective free that is still `>= q`. Take up to 3 candidates.
3. **Attempts.** Try the hold with the first zone and first host candidate. On a capacity shortage of a zone- or host-scoped line, try the next candidate under a new savepoint. At most 2 zones × 3 hosts, and never more than 4 attempts in total.

The snapshot is only a ranking hint. Correctness comes from the conditional updates in §4.

## 4. Hold transaction

One READ COMMITTED transaction, global lock order: reservation rows → quota rows → inventory rows ([architecture §8.3](../architecture/backend-architecture.md#83-global-lock-order)).

```text
BEGIN
  now := transaction timestamp
  idempotency: INSERT key (spec 06) → replay / mismatch / continue
  ensure quota rows exist (INSERT … ON CONFLICT DO NOTHING, default limit from config)
  SAVEPOINT attempt
    -- one NpgsqlBatch, one round trip
    for each line, sorted by resource_class:     UPDATE quotas      … (4.1)
    pool share check for capped products         SELECT …           (spec 07 §4)
    for each line, sorted by (pool_id, class):   UPDATE inventories … (4.2)
    if every statement affected exactly 1 row:
        INSERT reservation (HELD, expires_at = now + hold_ttl)
        INSERT reservation_lines (with pool_id)
        INSERT ledger_entries (HOLD, delta_held = +q) per line
        INSERT reservation_events (submit, NEW → HELD)
    else:
        ROLLBACK TO SAVEPOINT attempt
        next candidate (§3), or classify (§5)
  complete idempotency key with the response
COMMIT
```

### 4.1 Quota update

```sql
UPDATE quotas
SET    held = held + @q
WHERE  customer_id = @customer
  AND  resource_class = @class
  AND  region = @region
  AND  held + used + @q <= quota_limit
RETURNING held;
```

Zero rows → `QUOTA_EXCEEDED`. The quota is not retried on another candidate.

### 4.2 Inventory update

```sql
UPDATE inventories AS i
SET    held = i.held + @q
FROM   pools AS p
WHERE  i.pool_id = @pool
  AND  i.resource_class = @class
  AND  p.id = i.pool_id
  AND  p.admin_state = 'AVAILABLE'
  AND  i.held + i.used + @q <= floor((i.total - i.reserved) * i.allocation_ratio)
RETURNING i.held;
```

Zero rows → capacity shortage for that line. The table's `CHECK` constraint enforces the same bound if a bug ever bypasses this statement.

## 5. Shortage classification

After `ROLLBACK TO SAVEPOINT`, read the inventory rows of all candidate pools in scope (without locks) and classify each short line with `q` = requested quantity:

| Condition | Code |
| --- | --- |
| `q > max(sellable)` over pools in AVAILABLE or OFFLINE | `EXCEEDS_REGISTERED_CAPACITY` |
| Else `q > max(sellable − used)` over the same pools | `CAPACITY_SOLD_OUT` |
| Else (short only because of `held` or an offline pool) | `CAPACITY_TEMPORARILY_UNAVAILABLE` |

The reservation's code is the most severe line code. The classifier is a pure domain function (`ShortageClassifier`); the snapshot may be a few milliseconds old, which only affects the advice, never the decision.

`Retry-After` for `CAPACITY_TEMPORARILY_UNAVAILABLE`: seconds until the earliest `expires_at` among HELD reservations on the short pools, clamped to 1–60 s; 60 s when an offline pool is the cause.

## 6. Retry after finalizing expired holds

If the classification is `CAPACITY_TEMPORARILY_UNAVAILABLE` and the short pools hold reservations past `expires_at` that are still stored as HELD:

1. Roll back the whole transaction (including the idempotency key insert).
2. Run one finalization transaction for those pools: the sweeper's expire step limited to reservations with a line on the short pools, `LIMIT 200 FOR UPDATE SKIP LOCKED` ([spec 05 §4](05-expiry-and-effective-state.md#4-sweeper)).
3. Run the submit transaction again from the start, once.

The two transactions keep the global lock order; finalizing inside the hold transaction would lock reservation rows after inventory rows ([architecture §8.4](../architecture/backend-architecture.md#84-the-one-two-transaction-path)).

## 7. Rejected and waiting outcomes

| Final classification | `on_shortage` | Waiting limit | Result |
| --- | --- | --- | --- |
| `CAPACITY_TEMPORARILY_UNAVAILABLE` | `WAIT` | Not reached | `WAITING`, `wait_deadline = now + wait_max_seconds`; lines stored with `pool_id = null` |
| Any other case | — | — | `REJECTED` with the code; lines stored with `pool_id = null` |

Both are stored with a `reservation_events` row and the completed idempotency key. No counter changes.

## 8. Later transitions

All run on a reservation row locked `FOR UPDATE`, then quota rows, then inventory rows, sorted.

| Transition | Quota row | Inventory row | Ledger `entry_type` |
| --- | --- | --- | --- |
| confirm | `held −= q; used += q` | `held −= q; used += q` | `COMMIT` (`delta_held = −q`, `delta_used = +q`) |
| cancel, expire | `held −= q` | `held −= q` | `RETURN` (`delta_held = −q`) |
| release | `used −= q` | `used −= q` | `RELEASE` (`delta_used = −q`) |
| promote | as hold (§4.1, §4.2) | as hold | `HOLD` |

Confirm moves units between two counters of the same row, so no capacity guard is needed. `CHECK (held >= 0 AND used >= 0)` catches double application.

## 9. Sold-out negative cache

When a line is classified `CAPACITY_SOLD_OUT` or `EXCEEDS_REGISTERED_CAPACITY`, cache `(pool, class) → max(sellable − used)` in process for 1 second. A later request in that second whose quantity exceeds the cached value skips the inventory `UPDATE` and goes straight to classification. The request is still stored as `REJECTED` with its key; only the hot-row lock is avoided ([C-10](../edge-cases/edge-cases.md#1-concurrency-on-capacity)). `CAPACITY_TEMPORARILY_UNAVAILABLE` is never cached, because holds expire.

## 10. Timeouts

`statement_timeout = 2s` and `lock_timeout = 500ms` for the api role. A lock timeout returns `503 SERVICE_UNAVAILABLE` with `Retry-After: 1`; the client retries with the same key.
