# Cloud Capacity Allocation — Documentation

Backend core that lets customers see capacity, reserve it atomically across several resources, and confirm or cancel the reservation, without ever selling the same capacity twice.

## Layout

| Folder | What it holds |
| --- | --- |
| [`prd/`](prd/PRD.md) | Product requirements: problem, users, scope, requirements, success metrics |
| [`architecture/`](architecture/backend-architecture.md) | [Backend architecture](architecture/backend-architecture.md) (clean architecture) and the [load model](architecture/load-model.md) |
| [`specs/`](specs/README.md) | Implementation specs, one concern per file |
| [`adr/`](adr/README.md) | Architecture decision records, one decision per file |
| [`database/`](database/database.md) | Data model, ERD, constraints, indexes, growth path |
| [`edge-cases/`](edge-cases/edge-cases.md) | Edge cases, how the system handles each, and the tests that prove it |

## Reading order

1. [PRD](prd/PRD.md) — what and why.
2. [Backend architecture](architecture/backend-architecture.md) — how the code is shaped.
3. [Request state machine](specs/02-request-state-machine.md) and [resource state machine](specs/03-resource-state-machine.md) — the core model.
4. [Capacity allocation](specs/04-capacity-allocation.md) — the hot path.
5. [Edge cases](edge-cases/edge-cases.md) — what can go wrong.
6. [ADRs](adr/README.md) — why each choice was made and what was rejected.

## Conventions

- Every assumption the challenge left open is written down in [PRD §7](prd/PRD.md#7-assumptions) and referenced by ID (`A-n`).
- Edge cases are referenced by code (`C-1`, `R-3`, …) from [edge-cases.md](edge-cases/edge-cases.md).
- Numbers come from the hypothetical [load model](architecture/load-model.md). The challenge gives no real figures; the load test in the repository measures the real ceilings.
