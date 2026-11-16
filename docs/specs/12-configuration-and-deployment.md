# Spec 12 — Configuration and Deployment

Setup must be simple and repeatable for the evaluator: one command brings up everything. Related: [ADR-0020](../adr/0020-delivery-and-testing.md), [ADR-0018](../adr/0018-postgresql-high-availability.md).

## 1. Make targets

| Target | Does |
| --- | --- |
| `make up` | `docker compose up -d`: PostgreSQL, RabbitMQ, migrator, api, sweeper, relay; seeds demo data |
| `make test` | All test projects; integration tests start their own containers |
| `make demo` | Script that walks through submit, confirm, cancel, expiry, wait, quota and shortage cases with `curl` and prints each response |
| `make load` | k6 scenarios against the running stack |
| `make down` | Stops and removes containers and volumes |

Requirements: Docker with Compose v2. The .NET SDK is only needed to run tests outside containers.

## 2. Compose services

| Service | Image | Notes |
| --- | --- | --- |
| `postgres` | `postgres:17` | One instance; HA is described in ADR-0018, not reproduced |
| `rabbitmq` | `rabbitmq:4-management` | Profile `events`; without it the relay logs events instead of publishing |
| `migrator` | Service image, `migrate` command | Runs DbUp, then exits; other services wait for it |
| `api` | Service image, `Capacity__Roles=api` | Port 8080 |
| `sweeper` | Service image, `Capacity__Roles=sweeper` | |
| `relay` | Service image, `Capacity__Roles=relay` | |
| `otel-collector`, `prometheus`, `grafana` | Upstream images | Profile `observability` |

## 3. Configuration

`appsettings.json`, overridable by environment variables (`Capacity__…`).

| Key | Default | Meaning |
| --- | --- | --- |
| `Capacity:Roles` | `api` | Comma-separated: `api`, `sweeper`, `relay` |
| `Capacity:Cell` | `ir-thr` | Region served by this deployment |
| `ConnectionStrings:Capacity` | — | PostgreSQL |
| `Capacity:Regions` | `ir-thr`, `ir-tbz`, `ir-ahv` with zones | Valid regions and zones |
| `Capacity:ResourceClasses` | See below | Base unit, scope and default quota per class |
| `Capacity:Sweeper:IntervalMs` | 1000 | |
| `Capacity:Sweeper:BatchSize` | 200 | |
| `Capacity:Relay:IntervalMs` | 200 | |
| `Capacity:Relay:BatchSize` | 500 | |
| `Capacity:Database:StatementTimeoutMs` | 2000 | |
| `Capacity:Database:LockTimeoutMs` | 500 | |
| `Capacity:Cache:CatalogSeconds` | 2 | |
| `Capacity:Cache:SoldOutSeconds` | 1 | |
| `RabbitMq:Uri` | — | Empty disables publishing |

Resource classes:

```json
"ResourceClasses": {
  "VCPU":    { "BaseUnit": "millicore", "Scope": "ZONE",   "DefaultQuota": 64000 },
  "RAM_MB":  { "BaseUnit": "MB",        "Scope": "ZONE",   "DefaultQuota": 131072 },
  "DISK_GB": { "BaseUnit": "GB",        "Scope": "ZONE",   "DefaultQuota": 2000 },
  "IPV4":    { "BaseUnit": "unit",      "Scope": "REGION", "DefaultQuota": 10 },
  "GPU":     { "BaseUnit": "unit",      "Scope": "HOST",   "DefaultQuota": 0 }
}
```

`DefaultQuota: 0` for GPU means a contract is required.

## 4. Seed data

Three regions with two zones each; zone pools for VCPU, RAM and disk; one IPv4 pool per region; four GPU host pools in `ir-thr-1` with 8 GPUs each; products `vm-standard`, `vm-gpu`, `public-ip`; three demo customers with different quotas. Small enough that the demo reaches every shortage code.

## 5. Production topology (design only)

Per region, on Kubernetes:

| Component | Shape |
| --- | --- |
| api | Deployment, 3+ replicas, HPA on CPU and request rate |
| sweeper | Deployment, 2 replicas |
| relay | Deployment, 2 replicas |
| PostgreSQL | CloudNativePG cluster: 1 primary, 2 standbys, one synchronous; PgBouncer pooler in transaction mode |
| RabbitMQ | Cluster Operator, 3 nodes, quorum queues |
| Migrations | Kubernetes Job before rollout |

The gateway routes by region to the right cell ([architecture §9](../architecture/backend-architecture.md#9-growth-path)).
