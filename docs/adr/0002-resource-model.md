# ADR-0002: Resources modelled as pools, classes and counter-based inventories

- Status: Accepted
- Date: 2026-11-16
- Related: [PRD A-4, A-8, A-9](../prd/PRD.md#7-assumptions), [spec 03](../specs/03-resource-state-machine.md), [database](../database/database.md)

## Context

Resources differ: CPU, RAM and disk are divisible; IPs and GPUs are discrete; overcommit and sales limits differ per product. New products and resources arrive regularly.

## Decision

- A **resource class** (VCPU, RAM_MB, DISK_GB, IPV4, GPU) has a base unit and a scope (`ZONE`, `REGION`, `HOST`), defined in configuration.
- A **pool** is a unit of capacity in a region, optionally in a zone or on a host.
- An **inventory** row per (pool, class) holds `total`, `reserved`, `allocation_ratio`, `min_unit`, `step_size`, `max_unit`, and the counters `held` and `used`.
- Sellable capacity = `floor((total − reserved) × allocation_ratio)`.
- Discrete resources are counted, not identified: the service decides that one IP is available, IPAM decides which one.
- Divisible classes at zone level with a 10% `reserved` margin; IPs per region; GPUs per host.

## Options considered

| Level | Option | Verdict |
| --- | --- | --- |
| 1 | Fixed columns such as `cpu_free`, `ram_free` on a product table | Rejected. Every new resource is a migration; overcommit and discrete units do not fit. |
| 2 | Generic class + inventory counters, integer base units | **Chosen.** Same model OpenStack Placement has used at large scale. |
| 3 | Per-unit inventory with identity, traits, affinity and NUMA | Rejected. Millions of rows for ~100k VMs and a scheduler that is out of scope. |

## Consequences

- A new resource class is configuration; a new pool or product is data.
- Integer base units (millicores, MB) remove rounding errors.
- With 3 regions, 2 zones each and ~30 GPU hosts, the inventory table stays under 100 rows.
- Fragmentation is invisible; the margin and release after failed provisioning mitigate it.

## Revisit when

Same trigger as [ADR-0001](0001-system-boundary.md): fragmentation-driven provisioning failures above ~1%.
