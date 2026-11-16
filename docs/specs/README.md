# Specs

One concern per file. Each spec is precise enough to implement and test without reading the others, and links to the ADRs that justify it.

| # | Spec | Covers |
| --- | --- | --- |
| 01 | [API](01-api.md) | Customer and service endpoints, headers, payloads, errors |
| 02 | [Request state machine](02-request-state-machine.md) | Request DFA: states, events, guards, effects |
| 03 | [Resource state machine](03-resource-state-machine.md) | Capacity-unit DFA and how it maps to counters |
| 04 | [Capacity allocation](04-capacity-allocation.md) | Hold algorithm, pool selection, SQL, shortage classification |
| 05 | [Expiry and effective state](05-expiry-and-effective-state.md) | Effective state on read, sweeper, waiting promotion |
| 06 | [Idempotency](06-idempotency.md) | Keys, hashing, replay, retention |
| 07 | [Quotas and fairness](07-quotas-and-fairness.md) | Quotas, hold limits, pool share cap, cooldown, rate limits |
| 08 | [Events](08-events.md) | Event types, envelope, relay, ordering |
| 09 | [Admin API](09-admin-api.md) | Pools, inventories, products, quotas, reconciliation |
| 10 | [Observability](10-observability.md) | Metrics, traces, logs, alerts |
| 11 | [Testing](11-testing.md) | Test layers, concurrency and recovery tests, load test |
| 12 | [Configuration and deployment](12-configuration-and-deployment.md) | Config, roles, Docker Compose, Make targets |
