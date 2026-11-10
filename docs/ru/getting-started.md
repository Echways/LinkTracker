# Начало работы

[English](../en/getting-started.md) · [Русский](getting-started.md) · [Документация](README.md)

Эта страница доводит чистую машину до работающего бота, который отслеживает ссылки.

## Требования

- Docker с Compose.
- .NET 9 SDK, только для запуска сервисов вне контейнеров.
- Токен Telegram-бота от [@BotFather](https://t.me/BotFather).
- Необязательные токены: GitHub, Reddit, Yandex AI. См. ниже.

## Токены

| Переменная | Где взять | Без неё |
| ---------- | --------- | ------- |
| `Bot__Token` | [@BotFather](https://t.me/BotFather) | Бот не запускается |
| `GitHub__Token` | Personal access token на GitHub | Запросы к GitHub идут под анонимным лимитом |
| `Reddit__ClientId`, `Reddit__ClientSecret` | Приложение типа `script` на https://www.reddit.com/prefs/apps | Запросы к Reddit завершаются ошибкой |
| `YandexAi__ApiKey`, `YandexAi__FolderId` | Yandex Cloud | Суммаризация сводится к обрезке текста |
| `ServiceAuth__Secret` | Любая длинная случайная строка | Bot и Scrapper отклоняют друг друга |

## Env-файлы

```bash
cp deploy/.env.template deploy/.env
cp src/LinkTracker.Bot.Api/.env.template src/LinkTracker.Bot.Api/.env
cp src/LinkTracker.Scrapper.Api/.env.template src/LinkTracker.Scrapper.Api/.env
cp src/LinkTracker.AiAgent.Api/.env.template src/LinkTracker.AiAgent.Api/.env
```

Впишите токены. Bot и Scrapper аутентифицируют друг друга общим секретом, поэтому `SERVICE_AUTH_SECRET` в `deploy/.env` и `ServiceAuth__Secret` в файлах обоих сервисов должны совпадать. Файлы `.env` игнорируются git.

## Запуск в контейнерах

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.apps.yml up
```

Поднимутся PostgreSQL, трёхузловой кластер Kafka с инициализацией топиков, Schema Registry, трёхузловой кластер Valkey, Prometheus, Grafana и три сервиса. Порядок запуска задают `depends_on` и health-проверки. Настройки для контейнеров лежат в `appsettings.Docker.json` каждого сервиса.

## Локальный запуск

Поднимите инфраструктуру, затем сервисы в таком порядке:

```bash
docker compose -f deploy/docker-compose.yml up -d
dotnet run --project src/LinkTracker.Scrapper.Api
dotnet run --project src/LinkTracker.Bot.Api
dotnet run --project src/LinkTracker.AiAgent.Api
```

Локальные настройки — в `appsettings.json` каждого сервиса. Миграции Scrapper применяет при старте.

## Проверка

1. Откройте бота в Telegram и отправьте `/start`.
2. Отправьте `/track` и ссылку, например `https://github.com/dotnet/runtime`.
3. Отправьте `/list`. Ссылка в списке.

Порты на хосте:

| Адрес | Что |
| ----- | --- |
| http://localhost:9090 | Prometheus |
| http://localhost:3000 | Grafana, `admin` / `admin` |
| localhost:5434 | PostgreSQL |
| localhost:9094–9096 | Kafka |
| localhost:7379–7381 | Valkey |

Дальше: [Бот](bot.md), [Конфигурация](configuration.md), [Наблюдаемость](observability.md).
