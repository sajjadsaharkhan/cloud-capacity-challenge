# ADR-0013: Npgsql and Dapper with hand-written SQL; DbUp migrations

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR-0004](0004-concurrency-control.md), [ADR-0023](0023-clean-architecture.md)

## Context

Correctness depends on a few exact SQL statements: conditional updates, lock order, one round trip per hold, `SKIP LOCKED`. They must be visible and reviewable.

## Decision

- Npgsql (ADO.NET) through `NpgsqlDataSource`; Dapper only to map results.
- SQL lives in embedded `.sql` files in the infrastructure layer and is reviewed like code.
- The hold path sends its statements as one `NpgsqlBatch`.
- Migrations with DbUp: plain, versioned SQL scripts, run by a separate `migrate` command before roles start.

## Options considered

| Option | Verdict |
| --- | --- |
| Npgsql + Dapper | **Chosen** |
| EF Core for writes | Rejected. The change tracker and generated SQL hide lock boundaries and round trips; its default concurrency pattern is optimistic tokens, which [ADR-0004](0004-concurrency-control.md) rejects for hot rows. |
| EF Core for reads only | Rejected. Workable, but two data-access tools are not worth it for ~20 queries. |
| EF Core migrations | Rejected. Tied to the EF model, which is not used. |

## Consequences

- More SQL to write by hand; every statement is explicit.
- The property test checks that SQL and the domain agree ([spec 11 §3](../specs/11-testing.md#3-property-test-sql-agrees-with-the-domain)).

## Revisit when

Not expected to change.
