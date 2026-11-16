# ADR-0020: Docker Compose delivery; tests against real PostgreSQL

- Status: Accepted
- Date: 2026-11-16
- Related: [spec 11](../specs/11-testing.md), [spec 12](../specs/12-configuration-and-deployment.md)

## Context

"Project setup must be simple and repeatable for the evaluator." The system's correctness depends on database locking behaviour.

## Decision

- Docker Compose with `make up`, `make test`, `make demo`, `make load`.
- Integration tests use Testcontainers with the same PostgreSQL major version as production, not mocks and not SQLite.
- k6 measures the hot-row ceiling and the sweeper drain rate; results are committed with the hardware used.

## Options considered

| Option | Verdict |
| --- | --- |
| Compose + Testcontainers + k6 | **Chosen** |
| Kubernetes manifests as the delivery | Rejected for evaluation; heavy for a reviewer. Production topology is documented instead. |
| Mocked repositories for concurrency tests | Rejected. Only the real database shows real locking. |
| SQLite or an in-memory provider for tests | Rejected. Different locking semantics give false confidence. |

## Consequences

- Docker is required to run integration tests.
- The hypothetical load model is checked against measured numbers.

## Revisit when

Not expected to change.
