# Observability

## Архитектура сбора метрик

Метрики приложений построены на **OpenTelemetry** и отдаются Prometheus
по **pull-модели**: каждый сервис публикует OpenTelemetry-метрики в формате Prometheus
на своём эндпоинте `/metrics`, а Prometheus скрейпит их напрямую.

- Scrapper: `http://scrapper:8081/metrics`
- Bot: `http://bot:8011/metrics` (отдельный Kestrel-эндпоинт)
- AI-Agent: `http://aiagent:8102/metrics` (отдельный Kestrel-эндпоинт)

Порты приложений не публикуются на хост — Prometheus скрейпит цели изнутри
сети compose.

Rate limiter применяется точечно, политикой `public-api` на прикладных маршрутах,
поэтому `/metrics` и gRPC-сервисы под лимит не попадают в принципе.
Партиционирование идёт по `Tg-Chat-Id`, а не по IP: в Docker весь трафик бота
приходит с одного адреса, и лимит по IP отсекал бы легитимные запросы.

Имена серий, которые видит Prometheus, зафиксированы тестом
`MetricsEndpointTests` — экспортёр переименовывает инструменты, и панели Grafana
завязаны именно на итоговые имена.

## Метрики приложений

### Scrapper

| Метрика | Тип | Лейблы | Описание |
|---|---|---|---|
| `links_on_track_total` | Gauge | `tracked_source` | Количество ссылок в БД на мониторинге |
| `api_requests_total` | Counter | `source` | Счётчик запросов к API (лейбл — шаблон маршрута, не сырой путь) |
| `request_duration_ms_total` | Histogram | `scope`, `scope_type` | Длительность операции в мс (RED: Duration) |
| `errors_total` | Counter | `scope`, `scope_type`, `reason` | **Ошибки (RED: Errors)** |
| `sent_updates_total` | Counter | — | Количество обновлений, фактически отправленных в Bot (в outbox-режиме инкрементируется при успешной отправке из outbox) |
| `outbox_enqueued_updates_total` | Counter | — | Количество обновлений, записанных в transactional outbox |
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

### AI Agent

| Метрика | Тип | Лейблы | Описание |
|---|---|---|---|
| `kafka_consumed_total` | Counter | `topic` | Сообщений обработано из Kafka |
| `kafka_consume_errors_total` | Counter | `topic` | Ошибок обработки из Kafka |
| `kafka_consume_duration_ms_total` | Histogram | `topic` | Длительность обработки из Kafka в мс |
| `kafka_dead_letter_total` | Counter | `topic` | Сообщений отправлено в DLQ |
| `kafka_dead_letter_errors_total` | Counter | `topic` | Неудачных отправок в DLQ |
| `summarizations_total` | Counter | — | Успешных суммаризаций через Yandex AI |
| `summarization_fallbacks_total` | Counter | `reason` | **Суммаризаций, деградировавших до обрезки текста** |
| `process_memory_working_set_bytes` | Gauge | — | **Потребление RAM (working set), метрика для алерта** |
| `process_memory_managed_bytes` | Gauge | — | Управляемая память (GC) |

> Histogram-метрики дают в Prometheus три серии: `<name>_bucket`, `<name>_sum`, `<name>_count`.

## Конфигурация сбора

Настраивается на стороне Prometheus в `deploy/monitoring/prometheus.yml` — приложениям
никакой конфигурации телеметрии не требуется. Лейбл `job` берётся из имени scrape-job
(`scrapper` / `bot` / `aiagent`), `instance` — из адреса цели.

| Job | Цель | Интервал |
|---|---|---|
| `scrapper` | `scrapper:8081` | 15s (`global.scrape_interval`) |
| `bot` | `bot:8011` | 15s (`global.scrape_interval`) |
| `aiagent` | `aiagent:8102` | 15s (`global.scrape_interval`) |

## Grafana

Дашборд и алерты провижинятся автоматически из `deploy/monitoring/grafana/provisioning`:

- **Datasource**: Prometheus (`uid: prometheus`).
- **Dashboard**: `LinkTracker Observability` (`deploy/monitoring/.../dashboards/linktracker-dashboard.json`) —
  панели RED, БД, Kafka, RAM и бизнес-метрики.
- **Alert (требование задания)**: правило `High process memory usage`
  (`deploy/monitoring/.../alerting/rules.yml`) на метрику `process_memory_working_set_bytes`.
  Срабатывает, когда RAM сервиса (`scrapper`/`bot`) превышает 500 MB дольше 2 минут.

## PromQL запросы

Примеры PromQL для визуализаций — в `docs/example-pql.txt`.

## Запуск мониторинга

```bash
# Поднять инфраструктуру (Kafka, Postgres, Valkey, Prometheus, Grafana)
docker compose -f deploy/docker-compose.yml up -d

# Поднять приложения
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.apps.yml up -d
```

После запуска:
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (admin / admin)