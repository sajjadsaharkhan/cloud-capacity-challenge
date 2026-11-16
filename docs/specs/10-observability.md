# Spec 10 — Observability

Every threshold in the ADRs is a measurable metric, so a decision to move to the next level is made from data. Related: [ADR-0019](../adr/0019-observability.md), [load model](../architecture/load-model.md).

## 1. Stack

| Signal | Tool |
| --- | --- |
| Traces | OpenTelemetry SDK; ASP.NET Core, Npgsql and RabbitMQ instrumentation; OTLP exporter |
| Metrics | OpenTelemetry metrics, Prometheus exposition at `/metrics` |
| Logs | Structured JSON to stdout with `trace_id`, `reservation_id`, `customer_id` |

In Docker Compose: an OpenTelemetry Collector, Prometheus and Grafana with a prepared dashboard (optional `observability` profile).

## 2. Metrics

| Metric | Type | Labels | Drives |
| --- | --- | --- | --- |
| `capacity_holds_total` | counter | region, product, outcome (`held`, `waiting`, `rejected`), code | Rejection mix |
| `capacity_hold_duration_seconds` | histogram | region, product | p99 < 150 ms |
| `capacity_transitions_total` | counter | event, outcome | |
| `capacity_inventory_update_lock_wait_seconds` | histogram | pool, class | Hot row; ADR-0004 trigger at p99 > 20 ms |
| `capacity_holds_per_row_rate` | gauge | pool, class | ADR-0004 trigger at 150 / s |
| `capacity_free_units` | gauge | pool, class | Capacity levels |
| `capacity_expiry_lag_seconds` | histogram | region | Release lag < 5 s |
| `capacity_unsettled_holds` | gauge | region | Sweeper health |
| `capacity_waiting_requests` | gauge | region, product | ADR-0021 trigger at 10,000 |
| `capacity_ledger_drift_rows` | gauge | table | Must be 0 |
| `capacity_outbox_backlog` / `capacity_outbox_oldest_age_seconds` | gauge | | Relay health |
| `capacity_idempotent_replays_total` | counter | operation | Retry behaviour of clients |
| `capacity_db_pool_wait_seconds` | histogram | role | Connection pressure |

## 3. Traces

One span per use case (`reservation.submit`, `reservation.confirm`, …) with attributes `reservation.id`, `customer.id`, `region`, `outcome`, `code`; child spans for each SQL batch and each publish. The `traceparent` of the HTTP request is copied into the event payload's metadata so consumers can continue the trace.

## 4. Alerts

| Alert | Condition | Severity |
| --- | --- | --- |
| Ledger drift | `capacity_ledger_drift_rows > 0` | Page |
| Hold latency | p99 > 150 ms for 10 min | Ticket |
| Expiry lag | p99 > 5 s for 5 min | Page |
| Outbox stuck | oldest age > 60 s | Page |
| Hot row near limit | `capacity_holds_per_row_rate > 120` for 5 min | Ticket |
| Error budget | 99.95% availability burn rate > 2× over 1 h | Page |

## 5. Health endpoints

`/health/live`: process alive. `/health/ready`: database reachable and schema at the expected migration version; for the relay, broker connection open.
