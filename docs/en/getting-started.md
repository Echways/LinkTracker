# Getting started

[English](getting-started.md) · [Русский](../ru/getting-started.md) · [Documentation](README.md)

This page takes a clean machine to a running bot that tracks links.

## Requirements

- Docker with Compose.
- The .NET 9 SDK, only to run the services outside containers.
- A Telegram bot token from [@BotFather](https://t.me/BotFather).
- Optional tokens: GitHub, Reddit, Yandex AI. See below.

## Tokens

| Variable | Where to get it | Without it |
| -------- | --------------- | ---------- |
| `Bot__Token` | [@BotFather](https://t.me/BotFather) | The bot does not start |
| `GitHub__Token` | A personal access token on GitHub | GitHub requests hit the anonymous rate limit |
| `Reddit__ClientId`, `Reddit__ClientSecret` | An app of type `script` at https://www.reddit.com/prefs/apps | Reddit requests fail |
| `YandexAi__ApiKey`, `YandexAi__FolderId` | Yandex Cloud | Summaries fall back to truncated text |
| `ServiceAuth__Secret` | Any long random string | Bot and Scrapper reject each other |

## Environment files

```bash
cp deploy/.env.template deploy/.env
cp src/LinkTracker.Bot.Api/.env.template src/LinkTracker.Bot.Api/.env
cp src/LinkTracker.Scrapper.Api/.env.template src/LinkTracker.Scrapper.Api/.env
cp src/LinkTracker.AiAgent.Api/.env.template src/LinkTracker.AiAgent.Api/.env
```

Fill in the tokens. Bot and Scrapper authenticate each other with a shared secret, so `SERVICE_AUTH_SECRET` in `deploy/.env` and `ServiceAuth__Secret` in both service files must be identical. The `.env` files are ignored by git.

## Run in containers

```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.apps.yml up
```

This starts PostgreSQL, a three-node Kafka cluster with topic initialization, Schema Registry, a three-node Valkey cluster, Prometheus, Grafana and the three services. Start order comes from `depends_on` and health checks. Container settings live in the `appsettings.Docker.json` file of each service.

## Run locally

Start the infrastructure, then the services in this order:

```bash
docker compose -f deploy/docker-compose.yml up -d
dotnet run --project src/LinkTracker.Scrapper.Api
dotnet run --project src/LinkTracker.Bot.Api
dotnet run --project src/LinkTracker.AiAgent.Api
```

Local settings are in `appsettings.json` of each service. Migrations are applied by the Scrapper on start.

## Check that it works

1. Open the bot in Telegram and send `/start`.
2. Send `/track` and a link such as `https://github.com/dotnet/runtime`.
3. Send `/list`. The link is there.

Ports on the host:

| Address | What |
| ------- | ---- |
| http://localhost:9090 | Prometheus |
| http://localhost:3000 | Grafana, `admin` / `admin` |
| localhost:5434 | PostgreSQL |
| localhost:9094–9096 | Kafka |
| localhost:7379–7381 | Valkey |

Next: [Bot](bot.md), [Configuration](configuration.md), [Observability](observability.md).
