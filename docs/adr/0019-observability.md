# ADR-0019: OpenTelemetry and Prometheus

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 10](../specs/10-observability.md)

## Context

Every "revisit when" trigger in these ADRs is a number. Without the matching metric, moving to the next level is a guess.

## Decision

OpenTelemetry SDK for traces and metrics with ASP.NET Core, Npgsql and RabbitMQ instrumentation; OTLP export; Prometheus exposition at `/metrics`; structured JSON logs carrying `trace_id`, `reservation_id` and `customer_id`. Each ADR threshold has a named metric and an alert.

## Options considered

| Option | Verdict |
| --- | --- |
| OpenTelemetry + Prometheus | **Chosen.** Open standard, vendor-neutral, first-class in .NET. |
| Logs only | Rejected. Thresholds cannot be detected from logs. |
| A vendor APM agent | Rejected. Lock-in and licensing; OTLP can feed one later. |

## Consequences

- An optional Compose profile runs a collector, Prometheus and Grafana with a prepared dashboard.

## Revisit when

Not expected to change.
