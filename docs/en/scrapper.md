# Scrapper

[English](scrapper.md) · [Русский](../ru/scrapper.md) · [Documentation](README.md)

The Scrapper stores subscriptions, polls sources on a schedule, finds what changed and hands updates on.

## Sources

| Source | Link | Updates | Credentials |
| ------ | ---- | ------- | ----------- |
| GitHub | `https://github.com/owner/repo` | New issues and pull requests | `GitHub__Token` |
| Stack Overflow | `https://stackoverflow.com/questions/12345/...` | New answers and comments | none |
| Reddit | `https://reddit.com/r/dotnet` | New posts | `Reddit__ClientId`, `Reddit__ClientSecret` |

Each source has its own client with a token-bucket rate limiter, see [Configuration](configuration.md#scrapper).

## Scheduling

Every `Scheduling:IntervalSeconds` the Scrapper takes up to `BatchSize` links ordered by the oldest check, checks up to `MaxDegreeOfParallelism` of them at once and stores the time of the check in `links.last_checked_at`. When a source has something newer than the stored state, an update is created for every chat that tracks the link.

## REST API

Calls come from the Bot and carry `x-service-token`. Chat routes take the chat id in the `Tg-Chat-Id` header.

| Method | Route | Action |
| ------ | ----- | ------ |
| `POST` | `/tg-chat/{id}` | Register a chat |
| `DELETE` | `/tg-chat/{id}` | Delete a chat |
| `GET` | `/links` | List tracked links |
| `POST` | `/links` | Track a link, with tags |
| `DELETE` | `/links` | Stop tracking a link |

The same operations are available over gRPC on port `8082`. The contracts are in `LinkTracker.Shared/Protos`. Responses of `GET /links` are cached in Valkey and invalidated by changes.

## Outbox

When `Outbox:Enabled` is on, an update is written to the `outbox_messages` table in the transaction that records the change. A dispatcher reads batches every `DispatchIntervalSeconds`, takes a lease for `LockSeconds` so that several instances do not send the same message, publishes it and marks it sent. Failures are retried up to `MaxRetryCount` times. `sent_updates_total` counts messages actually delivered, `outbox_enqueued_updates_total` counts messages queued.

## Storage

Two interchangeable implementations of the same abstractions: `Storage.Sql` (plain SQL) and `Storage.Orm`. Switch with `Database:AccessType`. The schema comes from [migrations/](../../migrations/), applied at start when `RunMigrations` is on.
