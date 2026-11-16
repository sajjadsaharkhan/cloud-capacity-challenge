# Hold latency, steps 1–3 — 2026-11-16

Setup as in the report (`docs/reports/hold-latency.html`): one hot inventory row, a fresh customer per
request, 15 s per rate step, one api replica, Postgres, k6 and workers on one Docker Desktop VM (8 cores).
Postgres tuned for local runs only (`max_connections=300`, `checkpoint_timeout=30min`). Each series was run
after a discarded warm-up run. Values are p99 in ms.

| Offered rate | Granted, before | Granted, after (2 runs) | Stored rejection, before | Stored rejection, after (1 run) |
| --- | --- | --- | --- | --- |
| 5/s   | 59  | 32 · 36   | 44    | 40  |
| 20/s  | 104 | 34 · 41   | 39    | 28  |
| 50/s  | 113 | 30 · 30   | 32    | 38  |
| 100/s | 125 | 20 · 103  | 188   | 278 |
| 200/s | 164 | 53 · 56   | 4,633 | 126 |
| 300/s | —   | 1,002 (one run, with 400/s: 3.7 s) | — | 245 |

Round trips per hold (from the service's own `capacity_transaction_round_trips` metric, asserted in
`RoundTripTests`): **28 → 9**, the same for 1 line and 4 lines. A refusal on a full pool: 10.

## What changed

1. Per-phase timing (`capacity_hold_phase_duration_seconds`), round trips per transaction, `pg_stat_statements`.
2. Writes queued in the session and sent as batches: the request, lines, event, quota, ledger and
   idempotency response go in one batch; the contended inventory `UPDATE` goes alone, last, then `COMMIT`.
3. A request whose snapshot already shows no capacity in any plan skips the hold attempt.
4. Connection settings: auto-prepare, no reset on close, pool cap, statement/lock timeouts as startup options.

## Reading

- Target met on one row up to 200 holds/s in these runs (p99 ≤ 103 ms), beyond the 150/s safe limit the load
  model gives. It breaks between 200 and 300/s.
- Rejections no longer collapse at 200/s. At 100/s and 300/s the tail is still above 150 ms and noisy; one run
  is not enough to say more.
- **A bug found on the way:** the first version of the connection settings did not apply at all
  (`ContainsKey` is true for every Npgsql keyword). With no timeouts, overload queued for tens of seconds
  instead of failing fast. `ConnectionSettingsTests` now checks the settings on a pooled connection.
- **A wrong fix found on the way:** the Retry-After query walked all held reservations; it is now bounded to
  leases ending within 60 s, which is equivalent because the answer is clamped to 1–60 s.
- **Unexplained:** during an overloaded rejection run a PostgreSQL server process exited with code 2 and the
  server recovered by itself. Not reproduced since; not investigated further.

## Not done

- Not run on Linux hardware, with several api replicas, with synchronous replication, or against the
  scarce-burst scenario at 300/s for 2 minutes. `expiry-storm.js` and `mixed.js` not run.
- Steps 5–9 of the plan (one decision read, server-side hold function, admission control, pool slicing,
  infrastructure) are not started. Whether they are needed depends on a baseline on target hardware.
