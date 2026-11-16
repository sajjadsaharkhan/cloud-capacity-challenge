# Spec 09 — Admin API

Operator endpoints for the capacity catalog. All require `X-Actor-Kind: ADMIN` and an `Idempotency-Key` on writes. Every change is logged with actor, before and after values. Related: [spec 03](03-resource-state-machine.md), [database](../database/database.md).

## 1. Endpoints

| Method | Path | Purpose |
| --- | --- | --- |
| PUT | `/v1/admin/pools/{id}` | Create or update a pool (region, zone, host reference) |
| POST | `/v1/admin/pools/{id}/state` | `{ "event": "activate" | "take_offline" | "restore" | "retire" }` |
| PUT | `/v1/admin/pools/{id}/inventories/{class}` | Set `total`, `reserved`, `allocation_ratio`, `min_unit`, `step_size`, `max_unit`; optional `force` |
| PUT | `/v1/admin/products/{code}` | Create or update a product and its policy |
| PUT | `/v1/admin/quotas/{customer}/{class}/{region}` | Set `quota_limit` |
| POST | `/v1/admin/customers/{id}/cancel-open` | Cancel the customer's HELD and WAITING reservations |
| POST | `/v1/admin/ledger/adjustments` | Correct a counter with an `ADJUST` ledger row; requires `reason` |
| GET | `/v1/admin/reconciliation` | Latest reconciliation report |
| GET | `/v1/capacity?detail=true` | Per-pool numbers ([spec 01](01-api.md#41-get-v1capacityregionir-thrproductvm-standard)) |

## 2. Pool state changes

Follow the [resource DFA](03-resource-state-machine.md):

| Event | Allowed from | Effect |
| --- | --- | --- |
| `activate` | REGISTERED | Pool becomes sellable |
| `take_offline` | AVAILABLE | No new holds; existing holds and allocations finish normally |
| `restore` | OFFLINE | Sellable again |
| `retire` | REGISTERED; OFFLINE with `held = 0` and `used = 0` for all classes | Final |

Any other combination → `409 INVALID_STATE_TRANSITION`. A health checker may call `take_offline` and `restore` with `X-Actor-Kind: SERVICE`.

## 3. Inventory changes

The update locks the inventory row. If the new sellable capacity is below `held + used`:

- Without `force`: `409 CAPACITY_BELOW_USAGE` with current `held` and `used`.
- With `force`: the change is not applied; instead the pool is taken OFFLINE so no new holds land on it, and the response reports `held` and `used`. The operator repeats the update once they fit. Keeping the change as a stored pending value would need extra state for a rare operation.

`allocation_ratio >= 1`, `reserved <= total`, integer units.

## 4. Product changes

- `status: RETIRED` blocks new submits (`PRODUCT_NOT_AVAILABLE`); existing holds can still be confirmed, cancelled or expire ([D-3](../edge-cases/edge-cases.md#7-data-model-and-access)).
- Policy changes (TTL, limits) apply to new reservations only ([T-6](../edge-cases/edge-cases.md#4-time-and-expiry)).

## 5. Ledger adjustments

The only way to change a counter outside a reservation transition. Body: `{ "pool_id", "resource_class", "delta_held", "delta_used", "reason" }`, or the quota equivalent. Writes one `ADJUST` ledger row and applies the delta under the same `CHECK` constraints. Direct `UPDATE` of counters is not possible for the application database role.

## 6. Reconciliation report

Computed hourly by the sweeper and on demand. Read-only.

| Check | Compares |
| --- | --- |
| Ledger vs counters | `inventories.held/used` and `quotas.held/used` vs sums of `ledger_entries` deltas |
| Reservations vs counters | `held` vs lines of HELD reservations; `used` vs lines of CONFIRMED reservations with ACTIVE allocation |
| Unsettled backlog | HELD past `expires_at`, WAITING past `wait_deadline` |
| Outbox backlog | Unpublished events and the oldest age |

Response lists each drifting row with expected and actual values. Metric `capacity_ledger_drift_rows` must be 0; any drift alerts immediately. Corrections are made with ledger adjustments (§5), never automatically.

The orphan-allocation reconciler (allocations whose service no longer exists) needs the real inventory of product services. Only its interface, `IAllocationReconciler`, ships in this version ([D-6](../edge-cases/edge-cases.md#7-data-model-and-access)).
