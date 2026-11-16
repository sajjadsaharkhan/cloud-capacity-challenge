# ADR-0014: REST with OpenAPI and Problem Details

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 01](../specs/01-api.md), [PRD A-16](../prd/PRD.md#7-assumptions)

## Context

Consumers are the customer panel, customer automation (Terraform providers, scripts) and internal services. Automation needs machine-readable errors and safe retries.

## Decision

- REST over JSON under `/v1`, OpenAPI 3.1 generated from the endpoints.
- Errors as RFC 9457 Problem Details with a stable `code` and `retryable`.
- `Idempotency-Key` header on every mutation; cursor pagination.
- Identity from gateway headers (`X-Customer-Id`, `X-Actor-Kind`, `X-Actor-Id`); authentication is out of scope.

## Options considered

| Option | Verdict |
| --- | --- |
| REST + OpenAPI | **Chosen.** Works for every consumer, including the evaluator with `curl`. |
| gRPC only | Rejected. Friction for customer tooling and evaluation. Can be added for internal services on the same application layer. |
| GraphQL | Rejected. The API is command-oriented; flexible queries add nothing. |

## Consequences

- One contract file for all consumers; CI checks it against the committed OpenAPI document.

## Revisit when

Internal call volume or latency makes gRPC worthwhile.
