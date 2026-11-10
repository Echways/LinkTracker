# Configuration

[English](configuration.md) · [Русский](../ru/configuration.md) · [Documentation](README.md)

Each service reads `appsettings.json`, then `appsettings.Docker.json` in containers, then environment variables and its `.env` file. Nested keys use a double underscore in the environment: `Kafka__Topic`.

## Secrets

| Variable | Service | Purpose |
| -------- | ------- | ------- |
| `Bot__Token` | Bot | Telegram bot token |
| `ServiceAuth__Secret` | Bot, Scrapper | Shared secret in the `x-service-token` header. Must match. |
| `GitHub__Token` | Scrapper | GitHub API token |
| `Reddit__ClientId`, `Reddit__ClientSecret` | Scrapper | Reddit app credentials |
| `YandexAi__ApiKey`, `YandexAi__FolderId` | AI Agent | Yandex AI credentials |

## Bot

| Key | Default | Meaning |
| --- | ------- | ------- |
| `Scrapper:BaseUrl` | `http://localhost:8082` | Scrapper address |
| `Scrapper:Transport` | `Grpc` | `Http` or `Grpc` |
| `Kafka:Topic` | `link.processed-updates` | Processed updates to consume |
| `Kafka:GroupId` | `linktracker-bot` | Consumer group |
| `Kafka:DeadLetterTopic` | `link.processed-updates-dlq` | Failed messages |
| `Kafka:RetryAttempts`, `RetryBackoffMilliseconds` | `3`, `500` | Retries before the DLQ |
| `Valkey:DialogTtlSeconds` | `3600` | Lifetime of dialog state |

## Scrapper

| Key | Default | Meaning |
| --- | ------- | ------- |
| `Bot:BaseUrl`, `Bot:Transport` | `http://localhost:8091`, `Kafka` | How updates reach the Bot: `Http`, `Grpc` or `Kafka` |
| `Scheduling:IntervalSeconds` | `300` | Pause between polling rounds |
| `Scheduling:BatchSize` | `100` | Links per round |
| `Scheduling:MaxDegreeOfParallelism` | `4` | Concurrent checks |
| `Database:AccessType` | `Orm` | `Sql` or `Orm` |
| `Database:RunMigrations`, `MigrationsPath` | `true`, `../../migrations` | Apply SQL migrations on start |
| `Kafka:Topic` | `link.raw-updates` | Raw updates to publish |
| `Outbox:Enabled` | `true` | Publish through the outbox |
| `Outbox:DispatchIntervalSeconds`, `BatchSize` | `10`, `100` | Dispatcher cadence |
| `Outbox:MaxRetryCount`, `LockSeconds` | `3`, `60` | Retries and lease length |
| `Valkey:Enabled` | `true` | Cache link lists |
| `Valkey:LinksTtlSeconds` | `60` | Cache lifetime |
| `Valkey:ClientSideCaching*` | on, `10` s, `1000` entries | Local cache in front of Valkey |
| `GitHub`, `StackOverflow`, `Reddit` `:RateLimit` | see below | Token bucket per source |
| `RateLimiting` | `10` per `60` s | Public API limit per chat |

Source rate limits:

| Source | `TokenLimit` | `TokensPerPeriod` | `ReplenishmentPeriodSeconds` |
| ------ | -----------: | ----------------: | ---------------------------: |
| GitHub | 200 | 80 | 60 |
| Stack Overflow | 30 | 12 | 3600 |
| Reddit | 100 | 100 | 60 |

## AI Agent

| Key | Default | Meaning |
| --- | ------- | ------- |
| `Kafka:Consumer:Topic` | `link.raw-updates` | Input |
| `Kafka:Consumer:GroupId` | `linktracker-ai-agent` | Consumer group |
| `Kafka:Consumer:DeadLetterTopic` | `link.raw-updates-dlq` | Failed messages |
| `Kafka:Producer:Topic` | `link.processed-updates` | Output |
| `AiAgent:Filtering:StopWords` | `spam`, `ads`, `promo` | Drop an update containing any of them |
| `AiAgent:Filtering:ExcludedAuthors` | `bot-user` | Drop updates by these authors |
| `AiAgent:Filtering:MinLength` | `20` | Drop shorter updates |
| `AiAgent:Summarization:Threshold` | `300` | Text length that triggers summarization |
| `AiAgent:Prioritization:HighKeywords` | `critical`, `urgent`, `breaking`, `security` | High priority |
| `AiAgent:Prioritization:LowKeywords` | `minor`, `typo`, `chore`, `docs` | Low priority |
| `AiAgent:Grouping:WindowMs` | `30000` | Grouping window per chat |

## Resilience

Every outgoing HTTP client of the Bot and Scrapper uses `Resilience:Http`: `TimeoutMilliseconds`, `Retry` (`MaxRetryAttempts`, `BackoffMilliseconds`, `BackoffStrategy`, `RetryableStatusCodes`) and `CircuitBreaker` (`FailureRateThreshold`, `SamplingDurationSeconds`, `MinimumThroughput`, `WaitDurationInOpenStateMilliseconds`). Container profiles use a longer timeout.
