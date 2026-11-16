# ADR-0001: The service reserves capacity; it does not orchestrate service creation

- Status: Accepted
- Date: 2026-11-16
- Related: [PRD A-1, A-2, A-3](../prd/PRD.md#7-assumptions), [ADR-0017](0017-no-workflow-engine.md)

## Context

The brief asks for "the backend core of a system that manages this process", but real VM and infrastructure creation is out of scope. Today the process is spread over several tools, and the failures named in the brief are about capacity (seen but gone), request state (unclear outcome) and abandoned work. Who owns physical capacity data is not stated.

## Decision

The service owns **sellable capacity**, **quotas** and the **reservation lifecycle**. Product services (VM, DBaaS, IP) provision resources themselves and talk to this service through hold, confirm, cancel and release, in a try-confirm-cancel style. Physical capacity enters through the admin API or a sync job; this service is the source of truth for what may be sold.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | One CRUD service that both stores capacity and "creates the service", with a status column | Rejected. Two responsibilities share one half-finished state: exactly the "process that never finishes" in the brief. |
| 2 | Reservation service with a TCC-style contract; provisioning elsewhere | **Chosen** |
| 3 | Full placement service at host level (traits, bin-packing, like OpenStack Placement) plus a scheduler | Rejected for now. Needs live host inventory and a dedicated team; nothing in the brief requires host-level placement for divisible resources. |
| — | Facade over existing systems that own capacity | Rejected. Without owning the counters it cannot make check and take atomic, so "seen but gone" remains. |

## Consequences

- Adding a product is a data change: a product row listing its resource classes.
- Counters cannot see fragmentation; mitigated by a reserved margin ([ADR-0002](0002-resource-model.md)).
- Capacity returns only if product services call release; a reconciler interface covers missed calls ([spec 09 §6](../specs/09-admin-api.md#6-reconciliation-report)).

## Revisit when

- Provisioning failures caused by fragmentation exceed ~1% of confirms, or a multi-GPU product is added: move pools to host level (data change).
- The organisation decides another system is the source of truth for sellable capacity: this service becomes a reservation layer over that system's counters.
