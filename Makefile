# Requires Docker with Compose v2. The .NET SDK is only needed to run tests outside containers.
COMPOSE = docker compose --profile events

.PHONY: up test demo load down observability

up: ## PostgreSQL, RabbitMQ, migrations + demo data, api, sweeper, relay
	RABBITMQ_URI=amqp://guest:guest@rabbitmq:5672 $(COMPOSE) up -d --build --wait
	@echo "api: http://localhost:8080   rabbitmq: http://localhost:15672   metrics: http://localhost:8080/metrics"

observability: ## up, plus OpenTelemetry Collector, Prometheus (:9090) and Grafana (:3000)
	OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317 RABBITMQ_URI=amqp://guest:guest@rabbitmq:5672 \
		docker compose --profile events --profile observability up -d --build --wait

test: ## unit, integration (Testcontainers) and architecture tests
	dotnet test src/Arvan.Capacity.slnx

demo: ## walk through every outcome with curl against `make up`
	./scripts/demo.sh

load: ## k6 scenarios against the running stack
	for s in hot-row scarce-burst expiry-storm mixed; do \
		docker run --rm -i --add-host=host.docker.internal:host-gateway -v $$PWD/load/k6:/scripts \
			-e BASE_URL=http://host.docker.internal:8080 grafana/k6 run /scripts/$$s.js || exit 1; done

down: ## stop and remove containers and volumes
	docker compose --profile events --profile observability down -v
