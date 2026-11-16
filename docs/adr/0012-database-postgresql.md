# ADR-0012: PostgreSQL as the only source of truth

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR-0004](0004-concurrency-control.md), [ADR-0018](0018-postgresql-high-availability.md), [database](../database/database.md)

## Context

The database must provide:

1. Multi-row ACID transactions.
2. A conditional `UPDATE` that returns its result in the same statement.
3. Queue-style reads that skip locked rows (sweeper, relay).
4. Partial indexes over HELD and WAITING reservations only.
5. Synchronous replication with RPO 0.
6. Simple operation on Kubernetes without license cost.

## Decision

PostgreSQL 17 or newer is the only source of truth for capacity, quotas, reservations, ledger, idempotency keys and events.

## Options considered

| Criterion | PostgreSQL | MySQL (InnoDB) | SQL Server | SQLite |
| --- | --- | --- | --- | --- |
| Concurrent writers | Row locks with MVCC | Row locks with MVCC | Row locks | One writer per database |
| Conditional update returning rows | `RETURNING` | No; second statement needed | `OUTPUT` | `RETURNING` |
| Skip locked rows | `SKIP LOCKED` | `SKIP LOCKED` (8.0+) | `READPAST` hint | No |
| Partial index for HELD rows | Yes | No | Filtered index | Yes |
| Transactional DDL for migrations | Yes | No | Yes | Yes |
| Default isolation | READ COMMITTED with MVCC | REPEATABLE READ with gap locks | Lock-based READ COMMITTED unless RCSI is on | Serializable |
| Connection model | Process per connection | Thread per connection | Internal thread pool | In-process, no network |
| Sync replication and failover | Quorum sync + operator | Semi-sync or Group Replication | Always On; Standard edition limited to two replicas and one database | None |
| Kubernetes | Mature open-source operators | Open-source operators | Containers supported; HA needs separate tooling | Single pod only |
| License | Open source | Open source | Commercial, per core | Public domain |

**PostgreSQL — chosen.** Meets all six needs directly, adds declarative partitioning for the ledger, and Npgsql is a mature .NET driver. One primary on ordinary hardware handles thousands of write transactions per second, several times the 500/s target.

**Concurrency and threads.** Writer concurrency is decided by row locks and MVCC, not by the server's threading model. The real difference is connection cost: PostgreSQL forks a process per connection, more expensive than MySQL or SQL Server threads. The need is ~300 client connections (10 instances × 30); PgBouncer in transaction mode reduces them to a few dozen server connections, so the weakness is neutralised.

**Scaling on Kubernetes.** CloudNativePG manages failover, synchronous standbys and backups; it joined the CNCF Sandbox in January 2025 and was promoted to Incubation in November 2026 ([EDB](https://enterprisedb.com/blog/postgres-belongs-kubernetes-cloudnativepg-reaches-cncf-incubation)). Scale path: vertical first, then a read replica for reads, then one cluster per region. Sharding is never needed because each transaction is single-region.

**MySQL — rejected, but defensible.** The closest alternative. Costs for this design: no `RETURNING`, so a hold needs a procedure or a second statement per line; no partial indexes, so the expiry index covers all history; gap locks under default REPEATABLE READ increase lock waits and deadlocks on the sweeper's range scans unless isolation is lowered; DDL is not transactional. Cheaper connections are its advantage. If the operations team is MySQL-centric, it is an acceptable choice and the change stays in the repository layer.

**SQL Server — rejected.** Technically comparable and the most natural pairing with .NET. Rejected for four reasons. Per-core licensing, and Standard edition availability groups are limited to two replicas and one database ([Microsoft Learn](https://learn.microsoft.com/sql/database-engine/availability-groups/windows/basic-availability-groups-always-on-availability-groups)), so "one synchronous standby out of two" needs Enterprise. Default READ COMMITTED is lock-based, so capacity reads wait behind writers on the hot row until RCSI is enabled. Lock escalation can turn a large sweeper batch from row locks into a table lock that blocks every hold. And there is no open-source Kubernetes operator of comparable maturity. Formal licence procurement is also a separate obstacle for a company in Iran.

**SQLite — rejected.** One writer for the whole database and no row locks. More importantly, it is a file inside one process: several API instances cannot share it, there is no synchronous replication, and the system stops when that pod dies. It cannot meet the availability requirement. It is also unsuitable for tests, because its locking differs from production.

**Also rejected:** Redis as source of truth (asynchronous replication and periodic fsync lose acknowledged holds on failover); CockroachDB, YugabyteDB, TiDB (solve multi-region writes that the regional cells avoid on purpose; consensus per commit and more retries on hot rows); MongoDB and Cassandra (multi-row, multi-table invariants are the core of the problem; Cassandra's lightweight transactions are slow and its counters are not safe here); etcd (built for small metadata, few-GB size limit).

## Consequences

- PgBouncer and an operator join the operational footprint.
- The hot inventory rows are updated constantly: `fillfactor = 50` and aggressive autovacuum on that table prevent bloat.

## Revisit when

The organisation standard is MySQL, or quotas must be strongly consistent across regions (then distributed SQL becomes relevant).
