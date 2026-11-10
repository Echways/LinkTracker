# Scrapper

[English](../en/scrapper.md) · [Русский](scrapper.md) · [Документация](README.md)

Scrapper хранит подписки, по расписанию опрашивает источники, находит изменения и передаёт обновления дальше.

## Источники

| Источник | Ссылка | Обновления | Учётные данные |
| -------- | ------ | ---------- | -------------- |
| GitHub | `https://github.com/owner/repo` | Новые issue и pull request'ы | `GitHub__Token` |
| Stack Overflow | `https://stackoverflow.com/questions/12345/...` | Новые ответы и комментарии | не нужны |
| Reddit | `https://reddit.com/r/dotnet` | Новые посты | `Reddit__ClientId`, `Reddit__ClientSecret` |

У каждого источника свой клиент с token-bucket ограничителем, см. [Конфигурация](configuration.md#scrapper).

## Расписание

Каждые `Scheduling:IntervalSeconds` Scrapper берёт до `BatchSize` ссылок, которые проверялись раньше всех, проверяет до `MaxDegreeOfParallelism` из них одновременно и сохраняет время проверки в `links.last_checked_at`. Если у источника есть что-то новее сохранённого состояния, создаётся обновление для каждого чата, отслеживающего ссылку.

## REST API

Вызовы приходят от Bot и несут `x-service-token`. Маршруты чата принимают id чата в заголовке `Tg-Chat-Id`.

| Метод | Маршрут | Действие |
| ----- | ------- | -------- |
| `POST` | `/tg-chat/{id}` | Зарегистрировать чат |
| `DELETE` | `/tg-chat/{id}` | Удалить чат |
| `GET` | `/links` | Список отслеживаемых ссылок |
| `POST` | `/links` | Отслеживать ссылку, с тегами |
| `DELETE` | `/links` | Перестать отслеживать ссылку |

Те же операции доступны по gRPC на порту `8082`. Контракты лежат в `LinkTracker.Shared/Protos`. Ответы `GET /links` кэшируются в Valkey и сбрасываются при изменениях.

## Outbox

При включённом `Outbox:Enabled` обновление пишется в таблицу `outbox_messages` в той же транзакции, что фиксирует изменение. Диспетчер читает пачки каждые `DispatchIntervalSeconds`, берёт lease на `LockSeconds`, чтобы несколько экземпляров не отправили одно сообщение, публикует его и помечает отправленным. Ошибки повторяются до `MaxRetryCount` раз. `sent_updates_total` считает реально доставленные сообщения, `outbox_enqueued_updates_total` — поставленные в очередь.

## Хранилище

Две взаимозаменяемые реализации одних абстракций: `Storage.Sql` (чистый SQL) и `Storage.Orm`. Переключаются через `Database:AccessType`. Схема берётся из [migrations/](../../migrations/) и применяется при старте, если включён `RunMigrations`.
