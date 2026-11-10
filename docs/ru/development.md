# Разработка

[English](../en/development.md) · [Русский](development.md) · [Документация](README.md)

## Репозиторий

| Путь | Содержимое |
| ---- | ---------- |
| [src/](../../src/) | Сервисы, общие библиотеки и тесты |
| [migrations/](../../migrations/) | Схема PostgreSQL, применяется DbUp при старте Scrapper |
| [deploy/](../../deploy/) | Compose-файлы, конфигурация Prometheus и Grafana, шаблон корневого `.env` |
| [docs/](../) | Эта документация и [примеры PromQL](../example-pql.txt) |

Решение `linktracker.sln` нацелено на .NET 9. Версии пакетов централизованы в `src/Directory.Packages.props`; настройки сборки, включая nullable и предупреждения как ошибки, — в `src/Directory.Build.props`.

## Сборка и запуск

```bash
dotnet build
dotnet run --project src/LinkTracker.Scrapper.Api
```

Инфраструктура и порядок запуска сервисов описаны в [Начале работы](getting-started.md).

## Тесты

```bash
dotnet test
```

Тесты лежат в `src/LinkTracker.Tests` и размечены двумя трейтами: `Module` (`Bot`, `Scrapper`, `AiAgent`, `Shared`) и `Category` (`Unit`, `Integration`). Интеграционные тесты поднимают PostgreSQL, Kafka и Valkey через Testcontainers, поэтому нужен Docker.

```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Module=Scrapper&Category=Integration"
```

## Форматирование

Стиль задаёт `.editorconfig`, а CI отклоняет неотформатированный код:

```bash
dotnet format --verify-no-changes
```

## Непрерывная интеграция

`.github/workflows/ci.yml` запускается на каждый pull request и на push в `main`:

| Задача | Что делает |
| ------ | ---------- |
| `build` | Восстанавливает и собирает в образе .NET 9 SDK |
| `linter` | `dotnet format --verify-no-changes` |
| `test` | Unit-тесты по модулям и общие интеграционные тесты, параллельно |
| `docker-test` | Интеграционные тесты Bot, Scrapper и AI Agent на раннере с Docker |

## Добавление источника

1. Реализуйте клиент и разбор источника в Scrapper.
2. Зарегистрируйте его в модуле клиентов Scrapper вместе с секцией лимитов.
3. Добавьте формат ссылки в распознавание ссылок в `LinkTracker.Shared/Links`.
4. Добавьте unit-тесты и, если у источника свой API, интеграционный тест.
