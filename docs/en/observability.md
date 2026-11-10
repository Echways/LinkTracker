# Observability

[English](observability.md) · [Русский](../ru/observability.md) · [Documentation](README.md)

## Collection

Metrics are built on **OpenTelemetry** and exposed in Prometheus format at `/metrics` of each service. Prometheus pulls them directly.

| Service | Target |
| ------- | ------ |
| Scrapper | `http://scrapper:8081/metrics` |
| Bot | `http://bot:8011/metrics` (separate Kestrel endpoint) |
| AI Agent | `http://aiagent:8102/metrics` (separate Kestrel endpoint) |

Application ports are not published on the host: Prometheus scrapes from inside the compose network.

The rate limiter is applied with the `public-api` policy to application routes only, so `/metrics` and gRPC services are never limited. Partitioning is by `Tg-Chat-Id`, not by IP: in Docker all bot traffic arrives from one address, and an IP limit would cut off legitimate requests.

The series names Prometheus sees are pinned by the `MetricsEndpointTests` test. The exporter renames instruments, and the Grafana panels depend on the final names.

## Application metrics

### Scrapper

| Metric | Type | Labels | Description |
| ------ | ---- | ------ | ----------- |
| `links_on_track_total` | Gauge | `tracked_source` | Links being monitored |
| `api_requests_total` | Counter | `source` | API requests, labelled by route template, not raw path |
| `request_duration_ms_total` | Histogram | `scope`, `scope_type` | Operation duration, ms (RED: Duration) |
| `errors_total` | Counter | `scope`, `scope_type`, `reason` | **Errors (RED: Errors)** |
| `sent_updates_total` | Counter | none | Updates actually sent to the Bot; with the outbox it grows on a successful send |
| `outbox_enqueued_updates_total` | Counter | none | Updates written to the transactional outbox |
| `db_queries_total` | Counter | `operation` | Database queries |
| `db_errors_total` | Counter | `operation` | Database errors |
| `db_query_duration_ms_total` | Histogram | `operation` | Query duration, ms |
| `kafka_produced_total` | Counter | `topic` | Messages published to Kafka |
| `kafka_produce_errors_total` | Counter | `topic` | Publish errors |
| `kafka_produce_duration_ms_total` | Histogram | `topic` | Publish duration, ms |
| `process_memory_working_set_bytes` | Gauge | none | **RAM (working set), used by the alert** |
| `process_memory_managed_bytes` | Gauge | none | Managed (GC) memory |

### Bot

| Metric | Type | Labels | Description |
| ------ | ---- | ------ | ----------- |
| `bot_requests_total` | Counter | `request_type` | Incoming requests |
| `command_requests_total` | Counter | `command` | Command calls |
| `command_duration_ms_total` | Histogram | `scope`, `scope_type` | Command duration, ms |
| `scrapper_call_duration_ms_total` | Histogram | `scope`, `scope_type` | Calls to the Scrapper, ms |
| `sent_notification_total` | Counter | none | Notifications sent |
| `errors_total` | Counter | `scope`, `scope_type`, `reason` | **Errors (RED: Errors)** |
| `kafka_consumed_total` | Counter | `topic` | Messages consumed |
| `kafka_consume_errors_total` | Counter | `topic` | Consume errors |
| `kafka_consume_duration_ms_total` | Histogram | `topic` | Consume duration, ms |
| `process_memory_working_set_bytes` | Gauge | none | **RAM (working set), used by the alert** |
| `process_memory_managed_bytes` | Gauge | none | Managed (GC) memory |

### AI Agent

| Metric | Type | Labels | Description |
| ------ | ---- | ------ | ----------- |
| `kafka_consumed_total` | Counter | `topic` | Messages consumed |
| `kafka_consume_errors_total` | Counter | `topic` | Consume errors |
| `kafka_consume_duration_ms_total` | Histogram | `topic` | Consume duration, ms |
| `kafka_dead_letter_total` | Counter | `topic` | Messages sent to a DLQ |
| `kafka_dead_letter_errors_total` | Counter | `topic` | Failed DLQ sends |
| `summarizations_total` | Counter | none | Successful Yandex AI summaries |
| `summarization_fallbacks_total` | Counter | `reason` | **Summaries that degraded to truncation** |
| `process_memory_working_set_bytes` | Gauge | none | **RAM (working set), used by the alert** |
| `process_memory_managed_bytes` | Gauge | none | Managed (GC) memory |

> A histogram yields three series in Prometheus: `<name>_bucket`, `<name>_sum` and `<name>_count`.

## Prometheus

Scraping is configured in `deploy/monitoring/prometheus.yml`. The applications need no telemetry settings. The `job` label comes from the scrape job (`scrapper`, `bot`, `aiagent`), `instance` from the target address.

| Job | Target | Interval |
| --- | ------ | -------- |
| `scrapper` | `scrapper:8081` | 15 s (`global.scrape_interval`) |
| `bot` | `bot:8011` | 15 s |
| `aiagent` | `aiagent:8102` | 15 s |

## Grafana

The dashboard and alerts are provisioned from `deploy/monitoring/grafana/provisioning`:

- **Data source:** Prometheus (`uid: prometheus`).
- **Dashboard:** `LinkTracker Observability` with RED, database, Kafka, RAM and business panels.
- **Alert:** `High process memory usage` on `process_memory_working_set_bytes`. It fires when a service (`scrapper` or `bot`) holds more than 500 MB for over two minutes.

## PromQL

Ready queries for the panels are in [example-pql.txt](../example-pql.txt).

## Run

```bash
docker compose -f deploy/docker-compose.yml up -d
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.apps.yml up -d
```

| Address | What |
| ------- | ---- |
| http://localhost:9090 | Prometheus |
| http://localhost:3000 | Grafana, `admin` / `admin` |
