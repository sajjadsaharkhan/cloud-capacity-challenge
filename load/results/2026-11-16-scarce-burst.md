# Scarce burst — 2026-11-16

| | |
| --- | --- |
| Scenario | `load/k6/scarce-burst.js`: 300 iterations/s for 2 min against one IPv4 pool with 200 units (each iteration: one quota `PUT`, one submit) |
| Hardware | Laptop, Docker Desktop (macOS): PostgreSQL 17, one `api`, one `sweeper`, one `relay`, RabbitMQ and k6 all on the same VM |
| Stack | `docker compose --profile events up` as in `make up` |

## Result

| Measure | Value |
| --- | --- |
| Holds granted | **200** (exactly the pool's capacity) |
| Rejections stored | 6,625 `CapacityTemporarilyUnavailable` |
| Oversell | **0** |
| Reconciliation after the run | `driftRows: 0`, unsettled 0, outbox backlog 0 |
| Sustained throughput | ~58 iterations/s (~115 requests/s); k6 dropped 28,294 of the 300/s it was asked to start |
| Latency | p50 6.3 s, p99 15.3 s (**does not meet the 150 ms target at this load**) |
| 5xx | 882 of 7,707 submits (11%): 503 `service.unavailable`, lock timeouts on the one hot inventory row, as designed (clients retry with the same key) |

## Reading

- Correctness held under overload: the invariant (no oversell, ledger equals counters) is not
  load-sensitive.
- Latency is not. Every submit for the pool queues on one inventory row, and the row lock is held
  for the rest of the transaction (ledger rows, idempotency response, commit). On this setup that
  caps the single-row rate far below the load model's design target of 500 holds/s per cell, and the
  overload turns into 503s after the 500 ms lock timeout rather than into unbounded queueing.
- This is the signal [ADR-0004](../../docs/adr/0004-concurrency-control.md) describes: when one row
  is this hot, split the pool (data change only) before tuning code. The ceiling measured here is
  for a laptop VM and one hot row; the numbers on real hardware, and with the pool split across
  clusters, still have to be measured, and the load model updated if they differ.

Not run yet: `hot-row.js`, `expiry-storm.js`, `mixed.js` (they parse and are wired into `make load`).
