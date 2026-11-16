# ADR-0011: C# on .NET 10 with ASP.NET Core

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR-0013](0013-data-access-npgsql-dapper.md), [architecture](../architecture/backend-architecture.md)

## Context

The brief allows any language and counts Go as an advantage. The author is more fluent in ASP.NET Core than in Go.

## Decision

C# on .NET 10, the current LTS release, supported until November 2028 ([.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)). ASP.NET Core Minimal APIs for HTTP; `BackgroundService` for the sweeper and relay roles in the same host.

## Options considered

| Option | Verdict |
| --- | --- |
| C# / .NET 10 | **Chosen.** Correctness of this system lives in SQL and the transaction model, not in the language. Fluency reduces concurrency mistakes and delivery time and leaves more time for tests. Kestrel serves far more requests per second than the 500/s target; the bottleneck in any language is the hot database row. |
| Go | Rejected only for fluency. It is the better fit for a small infrastructure service and the brief prefers it. The design is language-neutral: about 20 SQL statements and two state machines, cheap to port. |
| Java / Kotlin | Rejected. Neither the brief's preference nor the author's fluency. |
| Python / Node.js | Rejected. Weaker typing and a single-threaded execution model add risk to a service whose value is concurrency correctness. |
| Rust | Rejected. Development speed. |

## Consequences

- The explicit Go advantage in evaluation is given up.
- Larger image and base memory than a Go binary.
- No equivalent of Go's race detector; integration tests against real PostgreSQL cover concurrency, where this system's races actually happen ([spec 11](../specs/11-testing.md)).

## Revisit when

The team that maintains the service is Go-centric.
