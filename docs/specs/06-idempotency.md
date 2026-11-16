# Spec 06 — Idempotency

Every state-changing call can be retried safely: the same key returns the same result, and never creates a second reservation. Related: [ADR-0006](../adr/0006-idempotency.md).

## 1. Scope

Required on: submit, confirm, cancel, extend, release, and every admin `POST`/`PUT`. Missing header → `400 IDEMPOTENCY_KEY_REQUIRED`.

## 2. Key

| Property | Rule |
| --- | --- |
| Format | 1–64 characters, `[A-Za-z0-9_-]` |
| Namespace | Per customer (`X-Customer-Id`); admin calls use the actor id |
| Bound to | Operation (`POST /v1/reservations`, `confirm:{id}`, …) and request fingerprint |
| Retention | 24 hours from first use, longer than any reservation's lifetime (max 45 minutes) |

Fingerprint: SHA-256 over the operation name and the canonical JSON body (keys sorted, no whitespace).

## 3. Table

`idempotency_keys (customer_id, idem_key)` primary key, with `operation`, `request_hash`, `reservation_id`, `status_code`, `response_body`, `expires_at`. See [database](../database/database.md).

## 4. Algorithm

Inside the use case's transaction, before any other write:

```sql
INSERT INTO idempotency_keys (customer_id, idem_key, operation, request_hash, expires_at)
VALUES (@customer, @key, @operation, @hash, now() + interval '24 hours')
ON CONFLICT (customer_id, idem_key) DO NOTHING
RETURNING 1;
```

| Insert result | Meaning | Action |
| --- | --- | --- |
| 1 row | New key | Run the use case; before commit, `UPDATE … SET status_code, response_body, reservation_id` |
| 0 rows, stored operation and hash match | Retry of a completed call | Return the stored status and body with `Idempotent-Replayed: true`; roll back |
| 0 rows, operation or hash differs | Key reused for something else | `422 IDEMPOTENCY_KEY_REUSED` |

Concurrent duplicates: the second `INSERT` waits on the first transaction's uncommitted row. When the first commits, the second gets 0 rows and replays its response. When the first rolls back, the second inserts and runs. No "in progress" state is ever visible ([R-2](../edge-cases/edge-cases.md#2-retries-and-unknown-outcomes)).

## 5. What is stored

The full response, including rejections (`409`, `422`), because those are deterministic outcomes of that request. Not stored: `401`, `403`, `429` and `503`, which are rejected before the transaction or by rolling it back; the key stays free and the retry runs normally.

## 6. Second line of defence

`client_reference` is unique per customer among `WAITING`, `HELD` and `CONFIRMED` reservations. A client that retries with a new key after 24 hours but the same reference gets `409 INVALID_STATE_TRANSITION` pointing at the existing reservation instead of a duplicate ([R-4](../edge-cases/edge-cases.md#2-retries-and-unknown-outcomes)).

## 7. Purge

The sweeper deletes rows whose `expires_at < now()`, 1,000 per batch. At ~50k mutating calls per day the table stays near 150k rows.
