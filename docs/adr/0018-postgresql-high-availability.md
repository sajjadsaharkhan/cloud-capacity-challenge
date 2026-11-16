# ADR-0018: CloudNativePG with synchronous replication (design only)

- Status: Accepted
- Date: 2026-11-16
- Related: [ADR-0008](0008-failure-mode-and-regional-cells.md), [ADR-0012](0012-database-postgresql.md), [spec 12 §5](../specs/12-configuration-and-deployment.md#5-production-topology-design-only)

## Context

RPO 0 for confirmed reservations and RTO under 60 seconds. The platform is Kubernetes. The delivered implementation does not need to reproduce this.

## Decision

Per regional cell, a CloudNativePG cluster with one primary and two standbys, quorum synchronous replication requiring one standby to acknowledge, automatic failover, and PgBouncer in transaction mode in front. Clients write only through the read-write service the operator maintains. The delivered Docker Compose stack runs a single PostgreSQL.

## Options considered

| Option | Verdict |
| --- | --- |
| CloudNativePG, sync quorum 1 of 2 | **Chosen.** Native Kubernetes operator, CNCF project, handles failover, fencing of the old primary and backups. |
| Patroni on VMs or StatefulSets | Rejected for Kubernetes; the right choice if PostgreSQL runs outside Kubernetes. |
| Asynchronous replication | Rejected. The last holds are lost on failover; a client that received 201 has no reservation. |
| Managed database service | Not available in the target environment. |

## Consequences

- Losing both standbys stops writes rather than continuing without replication: deliberate, consistent with fail-closed.
- The operator fences the old primary, preventing split brain ([F-5](../edge-cases/edge-cases.md#5-failure-and-recovery)).

## Revisit when

PostgreSQL is run outside Kubernetes in the target environment.
