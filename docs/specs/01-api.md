# Spec 01 — API

REST over JSON, versioned under `/v1`, described by OpenAPI 3.1 generated from the endpoints. Related: [ADR-0014](../adr/0014-rest-api.md), [ADR-0006](../adr/0006-idempotency.md), [ADR-0021](../adr/0021-shortage-responses-and-wait-queue.md).

## 1. Identity headers

The gateway authenticates and sets these headers; the service trusts them ([A-16](../prd/PRD.md#7-assumptions)).

| Header | Values | Required |
| --- | --- | --- |
| `X-Customer-Id` | Customer identifier | Customer and service calls |
| `X-Actor-Kind` | `CUSTOMER`, `SERVICE`, `ADMIN` | Always |
| `X-Actor-Id` | User, service or operator identifier | Always |

Missing or malformed identity → `401 IDENTITY_MISSING`. A `SERVICE` actor acts on behalf of `X-Customer-Id`.

## 2. Idempotency

Every `POST` that changes state requires `Idempotency-Key` (1–64 characters, `[A-Za-z0-9_-]`). Behaviour is in [spec 06](06-idempotency.md). Responses to a replay carry `Idempotent-Replayed: true`.

## 3. Endpoints

| Method | Path | Actor | Purpose | FR |
| --- | --- | --- | --- | --- |
| GET | `/v1/capacity` | Any | Capacity level per product and region | FR-1 |
| POST | `/v1/capacity/fit-check` | Customer, service | Would this request be accepted now? | FR-2 |
| POST | `/v1/reservations` | Customer, service | Submit a reservation | FR-3, FR-11 |
| GET | `/v1/reservations/{id}` | Customer, service | Effective status | FR-4 |
| GET | `/v1/reservations` | Customer, service | History | FR-8 |
| POST | `/v1/reservations/{id}/confirm` | Customer, service | Confirm a hold | FR-5 |
| POST | `/v1/reservations/{id}/cancel` | Customer, service | Cancel a hold or a wait | FR-6 |
| POST | `/v1/reservations/{id}/extend` | Customer, service | Extend a hold once | FR-7 |
| POST | `/v1/reservations/{id}/release` | Service, admin | Release allocated capacity | FR-9 |

Admin endpoints are in [spec 09](09-admin-api.md).

## 4. Payloads

### 4.1 `GET /v1/capacity?region=ir-thr&product=vm-standard`

Both query parameters are optional filters.

```json
{
  "items": [
    { "product": "vm-standard", "region": "ir-thr", "level": "AVAILABLE" },
    { "product": "gpu-a100",    "region": "ir-thr", "level": "LIMITED" }
  ],
  "as_of": "2026-11-16T10:00:00Z",
  "stale": false
}
```

- Level per product and region is the worst level across the product's resource classes.
- For each class: `UNAVAILABLE` when effective free capacity is 0 in every pool; `LIMITED` when effective free capacity is below 10% of sellable capacity; otherwise `AVAILABLE`. Effective free capacity counts expired-but-unsettled holds as free ([spec 05](05-expiry-and-effective-state.md)).
- `ADMIN` and `SERVICE` actors may pass `detail=true` to receive per-pool `sellable`, `held`, `used` and `free`.
- `stale: true` when the snapshot is served from cache because the database is unreachable.

### 4.2 `POST /v1/capacity/fit-check`

Same body as submit, without `on_shortage` and `client_reference`. Response:

```json
{ "fits": false, "code": "QUOTA_EXCEEDED", "lines": [ { "resource_class": "VCPU", "code": "QUOTA_EXCEEDED" } ] }
```

Advisory only: only a hold guarantees capacity. The response never states how much is free. Rate-limited per customer ([spec 07](07-quotas-and-fairness.md)).

### 4.3 `POST /v1/reservations`

```json
{
  "product": "vm-standard",
  "region": "ir-thr",
  "zone": "ir-thr-1",
  "lines": [
    { "resource_class": "VCPU",    "quantity": 4000 },
    { "resource_class": "RAM_MB",  "quantity": 8192 },
    { "resource_class": "DISK_GB", "quantity": 80 },
    { "resource_class": "IPV4",    "quantity": 1 }
  ],
  "on_shortage": "REJECT",
  "client_reference": "order-8841"
}
```

| Field | Rules |
| --- | --- |
| `product` | Active product code |
| `region` | Configured region |
| `zone` | Optional; must belong to the region; otherwise the service picks one ([spec 04 §3](04-capacity-allocation.md#3-pool-selection)) |
| `lines` | 1–16 lines; each `resource_class` allowed by the product and unique in the request; `quantity` is a positive integer in the class's base unit, satisfying `min_unit`, `step_size` and `max_unit` |
| `on_shortage` | `REJECT` (default) or `WAIT` |
| `client_reference` | Optional, ≤ 128 characters; unique per customer among `WAITING`, `HELD` and `CONFIRMED` reservations |

Base units: `VCPU` in millicores, `RAM_MB` in megabytes, `DISK_GB` in gigabytes, `IPV4` and `GPU` in units.

### 4.4 Reservation resource

Returned by submit, get, confirm, cancel, extend and release.

```json
{
  "id": "0193a4f2-7c1e-7b2a-9f3e-5d6c7b8a9e01",
  "status": "HELD",
  "settled": true,
  "reason_code": null,
  "product": "vm-standard",
  "region": "ir-thr",
  "lines": [
    { "resource_class": "VCPU", "quantity": 4000, "zone": "ir-thr-1" }
  ],
  "expires_at": "2026-11-16T10:15:00Z",
  "wait_deadline": null,
  "extended": false,
  "allocation_state": null,
  "client_reference": "order-8841",
  "version": 1,
  "created_at": "2026-11-16T10:00:00Z",
  "updated_at": "2026-11-16T10:00:00Z"
}
```

- `status` is the **effective** status. `settled: false` means the stored state has not caught up yet (for example a hold past `expires_at` reads `EXPIRED` before the sweeper stores it).
- `allocation_state` is `ACTIVE` or `RELEASED` for `CONFIRMED` reservations, otherwise `null`.
- Pool identifiers are never exposed to customers; `zone` is.

### 4.5 Submit responses

| Outcome | HTTP | Body |
| --- | --- | --- |
| Held | `201 Created`, `Location` header | Reservation, `status: HELD` |
| Waiting | `202 Accepted`, `Location` header | Reservation, `status: WAITING` |
| Rejected | `409` or `422` (§6) | Problem Details with `reservation_id`, `code` and per-line `lines` |

A rejected reservation is stored, so it appears in history and its idempotency key replays the same rejection.

### 4.6 `GET /v1/reservations`

Query: `status` (effective, repeatable), `product`, `region`, `created_after`, `created_before`, `limit` (1–100, default 20), `cursor`. Sorted by `created_at` descending, then `id`. Response: `{ "items": [...], "next_cursor": "…" }`. The cursor is an opaque, signed encoding of `(created_at, id)`.

### 4.7 Confirm, cancel, extend, release

Empty body, except release: `{ "reason": "SERVICE_DELETED" | "PROVISIONING_FAILED" | "RECONCILED" }`.

| Call | Success | Allowed when (effective status) |
| --- | --- | --- |
| confirm | `200`, `CONFIRMED`, `allocation_state: ACTIVE` | `HELD` |
| cancel | `200`, `CANCELLED` | `HELD` or `WAITING` |
| extend | `200`, `HELD`, new `expires_at` | `HELD` and not yet extended |
| release | `200`, `CONFIRMED`, `allocation_state: RELEASED` | `CONFIRMED` with `ACTIVE` allocation; actor `SERVICE` or `ADMIN` |

Calling an operation that already produced the current state (confirm on `CONFIRMED`, cancel on `CANCELLED`, release on `RELEASED`) returns `200` with the current resource, even with a new idempotency key.

## 5. Status codes

| HTTP | Meaning |
| --- | --- |
| 200 | Read or transition succeeded |
| 201 | Hold created |
| 202 | Waiting reservation created |
| 400 | Malformed request or missing `Idempotency-Key` |
| 401 | Identity headers missing |
| 403 | Actor kind not allowed |
| 404 | Not found, or belongs to another customer |
| 409 | Valid request, current state or capacity prevents it |
| 422 | Request can never succeed as written |
| 429 | Limit reached; `Retry-After` set |
| 503 | Service temporarily unable to decide; `Retry-After` set; safe to retry with the same key |

## 6. Errors

RFC 9457 Problem Details, `application/problem+json`, with extension members `code`, `retryable`, optional `reservation_id` and `lines`.

```json
{
  "type": "https://errors.capacity.internal/CAPACITY_TEMPORARILY_UNAVAILABLE",
  "title": "Capacity is temporarily unavailable",
  "status": 409,
  "code": "CAPACITY_TEMPORARILY_UNAVAILABLE",
  "retryable": true,
  "reservation_id": "0193a4f2-…",
  "lines": [ { "resource_class": "GPU", "code": "CAPACITY_TEMPORARILY_UNAVAILABLE" } ]
}
```

| Code | HTTP | Retryable | Meaning |
| --- | --- | --- | --- |
| `VALIDATION_FAILED` | 422 | No | Field rules in §4.3 violated; `errors` lists fields |
| `PRODUCT_NOT_AVAILABLE` | 422 | No | Unknown or retired product, or class not allowed |
| `EXCEEDS_REGISTERED_CAPACITY` | 422 | No | More than any pool in scope can ever provide |
| `CAPACITY_SOLD_OUT` | 409 | Not soon | Fits registered capacity, but live allocations fill it |
| `CAPACITY_TEMPORARILY_UNAVAILABLE` | 409 | Yes, `Retry-After` | Short only because of open holds or an offline pool |
| `QUOTA_EXCEEDED` | 409 | No | Contract limit, including the caller's open holds |
| `POOL_SHARE_EXCEEDED` | 409 | No | Caller's share of a scarce pool would exceed the cap |
| `HOLD_LIMIT_REACHED` | 429 | Yes | Too many active holds or waiting requests for this product |
| `COOLDOWN_ACTIVE` | 429 | Yes, `Retry-After` | Too many expired holds recently |
| `RATE_LIMITED` | 429 | Yes, `Retry-After` | Request rate limit |
| `INVALID_STATE_TRANSITION` | 409 | No | Operation not allowed from the current effective status; body includes `status` |
| `RESERVATION_EXPIRED` | 409 | No | Confirm or extend after `expires_at` |
| `NOT_FOUND` | 404 | No | Unknown id, or another customer's |
| `IDEMPOTENCY_KEY_REQUIRED` | 400 | No | Header missing |
| `IDEMPOTENCY_KEY_REUSED` | 422 | No | Same key, different body or operation |
| `IDENTITY_MISSING` | 401 | No | Identity headers absent |
| `FORBIDDEN` | 403 | No | Actor kind not allowed for the endpoint |
| `SERVICE_UNAVAILABLE` | 503 | Yes, `Retry-After` | Database unreachable or failing over |

When several lines are short, the reservation's `code` is the most severe line code, in the order `EXCEEDS_REGISTERED_CAPACITY` > `CAPACITY_SOLD_OUT` > `CAPACITY_TEMPORARILY_UNAVAILABLE`.

## 7. Limits

| Limit | Value |
| --- | --- |
| Request body | 16 KB |
| Lines per request | 16 |
| Page size | 100 |
| Request timeout | 2 s (`statement_timeout` on the database side) |
