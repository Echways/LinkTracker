# Конфигурация

[English](../en/configuration.md) · [Русский](configuration.md) · [Документация](README.md)

Каждый сервис читает `appsettings.json`, в контейнерах затем `appsettings.Docker.json`, затем переменные окружения и свой `.env`. Вложенные ключи в окружении пишутся через двойное подчёркивание: `Kafka__Topic`.

## Секреты

| Переменная | Сервис | Назначение |
| ---------- | ------ | ---------- |
| `Bot__Token` | Bot | Токен Telegram-бота |
| `ServiceAuth__Secret` | Bot, Scrapper | Общий секрет в заголовке `x-service-token`. Должен совпадать. |
| `GitHub__Token` | Scrapper | Токен GitHub API |
| `Reddit__ClientId`, `Reddit__ClientSecret` | Scrapper | Данные приложения Reddit |
| `YandexAi__ApiKey`, `YandexAi__FolderId` | AI Agent | Данные Yandex AI |

## Bot

| Ключ | По умолчанию | Смысл |
| ---- | ------------ | ----- |
| `Scrapper:BaseUrl` | `http://localhost:8082` | Адрес Scrapper |
| `Scrapper:Transport` | `Grpc` | `Http` или `Grpc` |
| `Kafka:Topic` | `link.processed-updates` | Обработанные обновления |
| `Kafka:GroupId` | `linktracker-bot` | Группа потребителей |
| `Kafka:DeadLetterTopic` | `link.processed-updates-dlq` | Сообщения с ошибкой |
| `Kafka:RetryAttempts`, `RetryBackoffMilliseconds` | `3`, `500` | Повторы до DLQ |
| `Valkey:DialogTtlSeconds` | `3600` | Время жизни состояния диалога |

## Scrapper

| Ключ | По умолчанию | Смысл |
| ---- | ------------ | ----- |
| `Bot:BaseUrl`, `Bot:Transport` | `http://localhost:8091`, `Kafka` | Как обновления доходят до Bot: `Http`, `Grpc` или `Kafka` |
| `Scheduling:IntervalSeconds` | `300` | Пауза между раундами опроса |
| `Scheduling:BatchSize` | `100` | Ссылок за раунд |
| `Scheduling:MaxDegreeOfParallelism` | `4` | Параллельных проверок |
| `Database:AccessType` | `Orm` | `Sql` или `Orm` |
| `Database:RunMigrations`, `MigrationsPath` | `true`, `../../migrations` | Применять SQL-миграции при старте |
| `Kafka:Topic` | `link.raw-updates` | Топик сырых обновлений |
| `Outbox:Enabled` | `true` | Публиковать через outbox |
| `Outbox:DispatchIntervalSeconds`, `BatchSize` | `10`, `100` | Темп диспетчера |
| `Outbox:MaxRetryCount`, `LockSeconds` | `3`, `60` | Повторы и длина lease |
| `Valkey:Enabled` | `true` | Кэшировать списки ссылок |
| `Valkey:LinksTtlSeconds` | `60` | Время жизни кэша |
| `Valkey:ClientSideCaching*` | вкл., `10` с, `1000` записей | Локальный кэш перед Valkey |
| `GitHub`, `StackOverflow`, `Reddit` `:RateLimit` | см. ниже | Token bucket на источник |
| `RateLimiting` | `10` за `60` с | Лимит публичного API на чат |

Лимиты источников:

| Источник | `TokenLimit` | `TokensPerPeriod` | `ReplenishmentPeriodSeconds` |
| -------- | -----------: | ----------------: | ---------------------------: |
| GitHub | 200 | 80 | 60 |
| Stack Overflow | 30 | 12 | 3600 |
| Reddit | 100 | 100 | 60 |

## AI Agent

| Ключ | По умолчанию | Смысл |
| ---- | ------------ | ----- |
| `Kafka:Consumer:Topic` | `link.raw-updates` | Вход |
| `Kafka:Consumer:GroupId` | `linktracker-ai-agent` | Группа потребителей |
| `Kafka:Consumer:DeadLetterTopic` | `link.raw-updates-dlq` | Сообщения с ошибкой |
| `Kafka:Producer:Topic` | `link.processed-updates` | Выход |
| `AiAgent:Filtering:StopWords` | `spam`, `ads`, `promo` | Отбросить обновление с любым из слов |
| `AiAgent:Filtering:ExcludedAuthors` | `bot-user` | Отбросить обновления этих авторов |
| `AiAgent:Filtering:MinLength` | `20` | Отбросить более короткие обновления |
| `AiAgent:Summarization:Threshold` | `300` | Длина текста, с которой включается суммаризация |
| `AiAgent:Prioritization:HighKeywords` | `critical`, `urgent`, `breaking`, `security` | Высокий приоритет |
| `AiAgent:Prioritization:LowKeywords` | `minor`, `typo`, `chore`, `docs` | Низкий приоритет |
| `AiAgent:Grouping:WindowMs` | `30000` | Окно группировки на чат |

## Отказоустойчивость

Каждый исходящий HTTP-клиент Bot и Scrapper использует `Resilience:Http`: `TimeoutMilliseconds`, `Retry` (`MaxRetryAttempts`, `BackoffMilliseconds`, `BackoffStrategy`, `RetryableStatusCodes`) и `CircuitBreaker` (`FailureRateThreshold`, `SamplingDurationSeconds`, `MinimumThroughput`, `WaitDurationInOpenStateMilliseconds`). В контейнерных профилях таймаут больше.
