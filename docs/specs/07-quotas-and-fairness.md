# Spec 07 — Quotas and Fairness

Contract limits and controls that stop one customer from consuming or blocking capacity. Related: [ADR-0007](../adr/0007-quota-in-the-ledger.md), edge cases [A-1 … A-8](../edge-cases/edge-cases.md#3-abuse-and-fairness) and [Q-1 … Q-6](../edge-cases/edge-cases.md#6-quotas-and-contracts).

## 1. Controls

| Control | Scope | Enforced | Error |
| --- | --- | --- | --- |
| Quota | Customer × class × region | Conditional `UPDATE` in the hold transaction | `409 QUOTA_EXCEEDED` |
| Pool share cap | Customer × pool × class, scarce products only | Query under the quota row lock | `409 POOL_SHARE_EXCEEDED` |
| Active hold limit | Customer × product | Pre-check | `429 HOLD_LIMIT_REACHED` |
| Waiting limit | Customer × product | Pre-check | `429 HOLD_LIMIT_REACHED` |
| Cooldown | Customer × product | Pre-check | `429 COOLDOWN_ACTIVE` |
| Rate limit | Customer × endpoint group | ASP.NET Core rate limiter, per instance | `429 RATE_LIMITED` |
| Extension limit | Reservation | Request DFA guard | `409 INVALID_STATE_TRANSITION` |

## 2. Quota

- Row per (customer, class, region): `quota_limit`, `held`, `used`.
- Holds count: `held + used + q <= quota_limit` ([A-12](../prd/PRD.md#7-assumptions)).
- Missing row: created with the class's `default_quota` from configuration (`INSERT … ON CONFLICT DO NOTHING`). A missing row never means unlimited ([Q-3](../edge-cases/edge-cases.md#6-quotas-and-contracts)).
- Lowering `quota_limit` below current usage is allowed: existing holds and allocations stay, new holds fail until usage drops under the limit ([Q-2](../edge-cases/edge-cases.md#6-quotas-and-contracts)). This is why the limit is a condition in the `UPDATE`, not a `CHECK` constraint.
- A quota shortage never puts a request into WAITING ([Q-5](../edge-cases/edge-cases.md#6-quotas-and-contracts)).

## 3. Policy values

Per product, in `products`:

| Column | Default | Scarce product (e.g. GPU) |
| --- | --- | --- |
| `hold_ttl_seconds` | 900 | 120 |
| `wait_max_seconds` | 900 | 300 |
| `max_active_holds` | 20 | 4 |
| `max_waiting` | 4 | 4 |
| `max_pool_share_pct` | 100 (off) | 25 |
| Cooldown | 5 expiries in 1 h → 10 min | Same |

## 4. Pool share cap

For products with `max_pool_share_pct < 100`, after the quota row for the line is locked:

```sql
SELECT coalesce(sum(l.quantity), 0)
FROM   reservation_lines l
JOIN   reservations r ON r.id = l.reservation_id
WHERE  r.customer_id = @customer
  AND  l.pool_id = @pool
  AND  l.resource_class = @class
  AND  (r.stored_status = 'HELD'
        OR (r.stored_status = 'CONFIRMED' AND r.allocation_state = 'ACTIVE'));
```

Reject if `existing + q > sellable × max_pool_share_pct / 100`. All of a customer's requests for that class and region lock the same quota row first, so two concurrent requests from one customer cannot both pass the check.

Worked example: 200 GPUs, 2,000 customers in the first minute, TTL 2 minutes, at most 4 active holds per customer, 25% share cap. In the worst case hoarding is spread across at least 50 customers instead of one.

## 5. Cooldown

```sql
SELECT count(*)
FROM   reservations
WHERE  customer_id = @customer
  AND  product_code = @product
  AND  stored_status = 'EXPIRED'
  AND  updated_at > now() - interval '1 hour';
```

Five or more, and the latest within the last 10 minutes → `429 COOLDOWN_ACTIVE`, `Retry-After` = seconds until 10 minutes after the latest expiry. Served by a partial index. Not under a lock: a race lets at most one extra hold through, which cannot oversell.

## 6. Rate limits

ASP.NET Core `RateLimiter`, token bucket per customer and endpoint group, per instance. Defaults:

| Group | Rate | Burst |
| --- | --- | --- |
| Submit | 10 / s | 20 |
| Fit-check | 5 / s | 10 |
| Reads | 50 / s | 100 |
| Transitions | 20 / s | 40 |

Per-instance limits multiply with the number of instances; acceptable at 3 instances. A shared limiter in Redis is the [growth step](../architecture/backend-architecture.md#9-growth-path) when exact global limits are needed.

## 7. Internal services

Internal services call with `X-Actor-Kind: SERVICE` and a customer id. Their actions count against that customer's quota. A service identity that creates holds for itself (for example capacity tests) uses its own customer id with its own quota and limits ([A-8](../edge-cases/edge-cases.md#3-abuse-and-fairness)).

## 8. Suspended customers

Customer status belongs to the account system, and the gateway stops issuing identity for a suspended customer, so new requests never arrive. This service only cleans up: `POST /v1/admin/customers/{id}/cancel-open` cancels the customer's HELD and WAITING reservations in batches of 200 ([spec 09](09-admin-api.md)). Confirmed allocations are untouched; their fate belongs to billing ([Q-4](../edge-cases/edge-cases.md#6-quotas-and-contracts)).
