# Observability

## Архитектура сбора метрик

Метрики приложений построены на **OpenTelemetry** и доставляются в Prometheus
по **push-модели через Prometheus Pushgateway**.

## Метрики приложений

### Scrapper

| Метрика | Тип | Лейблы | Описание |
|---|---|---|---|
| `links_on_track_total` | Gauge | `tracked_source` | Количество ссылок в БД на мониторинге |
| `api_requests_total` | Counter | `source` | Счётчик запросов к API |
| `request_duration_ms_total` | Histogram | `scope`, `scope_type` | Длительность операции в мс (RED: Duration) |
| `errors_total` | Counter | `scope`, `scope_type`, `reason` | **Ошибки (RED: Errors)** |
| `sent_updates_total` | Counter | — | Количество обновлений, отправленных в Bot |
| `db_queries_total` | Counter | `operation` | Количество запросов к БД |
| `db_errors_total` | Counter | `operation` | Количество ошибок БД |
| `db_query_duration_ms_total` | Histogram | `operation` | Длительность запроса к БД в мс |
| `kafka_produced_total` | Counter | `topic` | Сообщений опубликовано в Kafka |
| `kafka_produce_errors_total` | Counter | `topic` | Ошибок публикации в Kafka |
| `kafka_produce_duration_ms_total` | Histogram | `topic` | Длительность публикации в Kafka в мс |
| `process_memory_working_set_bytes` | Gauge | — | **Потребление RAM (working set), метрика для алерта** |
| `process_memory_managed_bytes` | Gauge | — | Управляемая память (GC) |

### Bot

| Метрика | Тип | Лейблы | Описание |
|---|---|---|---|
| `bot_requests_total` | Counter | `request_type` | Количество входящих запросов к боту |
| `command_requests_total` | Counter | `command` | Количество вызовов команд |
| `command_duration_ms_total` | Histogram | `scope`, `scope_type` | Длительность выполнения команды в мс |
| `scrapper_call_duration_ms_total` | Histogram | `scope`, `scope_type` | Длительность вызовов к Scrapper в мс |
| `sent_notification_total` | Counter | — | Количество отправленных уведомлений |
| `errors_total` | Counter | `scope`, `scope_type`, `reason` | **Ошибки (RED: Errors)** |
| `kafka_consumed_total` | Counter | `topic` | Сообщений обработано из Kafka |
| `kafka_consume_errors_total` | Counter | `topic` | Ошибок обработки из Kafka |
| `kafka_consume_duration_ms_total` | Histogram | `topic` | Длительность обработки из Kafka в мс |
| `process_memory_working_set_bytes` | Gauge | — | **Потребление RAM (working set), метрика для алерта** |
| `process_memory_managed_bytes` | Gauge | — | Управляемая память (GC) |

> Histogram-метрики дают в Prometheus три серии: `<name>_bucket`, `<name>_sum`, `<name>_count`.

## Конфигурация push

Настройки секции `Telemetry:Pushgateway` (переопределяются переменными окружения
с разделителем `__`):

| Ключ | Env-переменная | По умолчанию |
|---|---|---|
| `Endpoint` | `Telemetry__Pushgateway__Endpoint` | `http://pushgateway:9091/metrics` |
| `Job` | `Telemetry__Pushgateway__Job` | `scrapper` / `bot` |
| `Enabled` | `Telemetry__Pushgateway__Enabled` | `true` |
| `IntervalMilliseconds` | `Telemetry__Pushgateway__IntervalMilliseconds` | `5000` |

`Instance` берётся из переменной `HOSTNAME` (в контейнере — id контейнера) либо из имени хоста.

## Grafana

Дашборд и алерты провижинятся автоматически из `monitoring/grafana/provisioning`:

- **Datasource**: Prometheus (`uid: prometheus`).
- **Dashboard**: `LinkTracker Observability` (`monitoring/.../dashboards/linktracker-dashboard.json`) —
  панели RED, БД, Kafka, RAM и бизнес-метрики.
- **Alert (требование задания)**: правило `High process memory usage`
  (`monitoring/.../alerting/rules.yaml`) на метрику `process_memory_working_set_bytes`.
  Срабатывает, когда RAM сервиса (`scrapper`/`bot`) превышает 500 MB дольше 2 минут.

## PromQL запросы

Примеры PromQL для визуализаций — в `example-pql.txt`.

## Запуск мониторинга

```bash
# Поднять инфраструктуру (Kafka, Postgres, Valkey, Pushgateway, Prometheus, Grafana)
docker compose -f docker-compose.yml up -d

# Поднять приложения
docker compose -f docker-compose.yml -f docker-compose.apps.yml up -d
```

После запуска:
- Prometheus: http://localhost:9090
- Pushgateway: http://localhost:9091
- Grafana: http://localhost:3000 (admin / admin)