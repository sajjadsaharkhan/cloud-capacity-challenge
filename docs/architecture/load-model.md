# Load Model

The challenge gives no real figures, so this is a hypothetical but internally consistent scenario. Every threshold in the ADRs and specs is derived from it. The k6 load test in the repository measures the real ceilings on real hardware; where they disagree with this table, the measurement wins.

## Anchors

- Three compute regions: Tehran, Tabriz, Ahvaz.
- A previous public incident report from the provider put one data centre at roughly 2,500 customers and 7,000+ cloud servers, about 10% of all customers at the time. That implies roughly 25k customers and about 3 VMs per IaaS customer. These are approximations, used only to size the scenario.

## Scenario

| Quantity | Estimate | Basis |
| --- | --- | --- |
| Active IaaS customers | 30k (range 15k–60k) | Growth from ~25k total customers |
| Live VMs | ~100k | ~3 per customer |
| Reservation requests per day | ~50k | 5% daily VM churn, ×2 for other products, ×4 for automation |
| Average rate | 0.6 / s | 50k / 86,400 |
| Hourly peak | ~6 / s | 10× average |
| Burst on one scarce product | 200–300 / s for 1–2 min | New GPU launch or automation retry storm |
| Resource lines per request | avg 4, max 16 | CPU, RAM, disk, IP |
| Concurrent active holds | ~5,400 at peak; 450k at design target | Rate × 15-minute TTL |
| Capacity reads | ~100 / s peak; design 5,000 / s | 10–20× writes, cacheable |
| Yearly data | 18M requests, ~200M ledger rows, 60–100 GB | ~3 ledger rows per line over a lifecycle |

## Design targets

| Target | Value | Ratio to estimate |
| --- | --- | --- |
| Sustained holds per regional cell | 500 / s | ~80× hourly peak |
| Availability of hold / confirm path | 99.95% | ~22 min downtime per month |
| Hold latency | p99 < 150 ms | |
| RPO for confirmed reservations | 0 | Synchronous replication |
| RTO | < 60 s | Automatic failover |
| Expired-hold release lag | < 5 s | |

## The real bottleneck: one hot row

The ceiling is set by the lock on one inventory row, not by PostgreSQL's total throughput. Every VM created in Tehran locks the CPU row of the same pool.

- Lock hold time with one round trip and a synchronous commit: ~3–5 ms.
- Theoretical ceiling per row: 200–300 holds / s. Safe working limit: ~150 / s.
- Hourly peak is 6 / s, so a single pool has ~25× headroom.
- With pools split per zone (2 zones per region), the safe limit per cell is ~300 / s. Reaching 500 / s requires splitting pools further (per cluster). Only data changes; see [ADR-0004](../adr/0004-concurrency-control.md).
- A scarce-product burst exceeds a single row's ceiling, but most of it is rejected. A 1-second negative cache on "sold out" and a per-customer rate limit absorb it before the database.
