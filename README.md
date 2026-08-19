# LinkTracker


Проект сделан в рамках курса Академия Бэкенда.
Приложение для отслеживания обновлений контента по ссылкам.
При появлении новых событий отправляется уведомление в Telegram.
Проект состоит из 3-х приложений:

Bot
Scrapper
AI-Agent (исходно отсутствует в шаблоне)


Описание сервисов

Bot Service
Отвечает за взаимодействие с пользователями через Telegram Bot API:

Регистрация и авторизация пользователей
Обработка команд (/track, /untrack, /list и др.)
Управление подписками через взаимодействие со Scrapper Service
Отправка уведомлений пользователям
Хранение данных о пользователях и их настройках


Scrapper Service
Осуществляет мониторинг контента:

Периодическая проверка отслеживаемых URL на наличие изменений
Парсинг контента с различных источников (GitHub, Stack Overflow, Reddit и др.)
Определение изменений (diff detection)
Отправка уведомлений в Bot Service при обнаружении обновлений
Хранение информации о подписках и состоянии контента


Методы коммуникации:

REST API для синхронной коммуникации
Apache Kafka для асинхронной обработки


AI Agent Service
Обрабатывает контент перед отправкой уведомлений:

Суммаризация длинных обновлений
Фильтрация по стоп-словам и авторам
Приоритизация обновлений
Группировка связанных обновлений
Работает как промежуточное звено между Scrapper и Bot

---
## Инструкция для запуска бота

1. Создать копию [.env.template](src/LinkTracker.Bot.Api/.env.template) с именем .env в каталоге бота.
```
cp src/LinkTracker.Bot.Api/.env.template src/LinkTracker.Bot.Api/.env
```
2. Поставить действительные параметры в .env.
3. Создать копию [.env.template](src/LinkTracker.Scrapper.Api/.env.template) с именем .env в каталоге скреппера.
```
cp src/LinkTracker.Scrapper.Api/.env.template src/LinkTracker.Scrapper.Api/.env
```
4. Поставить действительные параметры в .env.
5. Создать копию [.env.template](src/LinkTracker.AiAgent.Api/.env.template) с именем .env в каталоге ии-агента.
```
cp src/LinkTracker.AiAgent.Api/.env.template src/LinkTracker.AiAgent.Api/.env
```
6. Поставить действительные параметры в .env.
7. Задать общий сервисный секрет. Bot и Scrapper аутентифицируют друг друга по нему,
   поэтому значение должно совпадать в обоих сервисах.
```
cp .env.template .env
```
8. Поставить в корневой .env своё значение `SERVICE_AUTH_SECRET`, а в
   `src/LinkTracker.Bot.Api/.env` и `src/LinkTracker.Scrapper.Api/.env` — то же самое
   значение в `ServiceAuth__Secret` (нужно для локального запуска без Docker).

### Для локального запуска
9. В [bot.Api.appsettings](src/LinkTracker.Bot.Api/appsettings.json), [scrapper.Api.appsettings](src/LinkTracker.Scrapper.Api/appsettings.json) и [aiagent.Api.appsettings](src/LinkTracker.AiAgent.Api/appsettings.json) в ветках Scrapper, Bot и AiAgent соответственно выбрать валидные параметры.
10. Выполнить поочердено запуск сначала [docker-compose](docker-compose.yml), [Scrapper](src/LinkTracker.Scrapper.Api/Program.cs), [Bot](src/LinkTracker.Bot.Api/Program.cs) и [AiAgent](src/LinkTracker.AiAgent.Api/Program.cs) с помощью команд.
```
docker compose -f docker-compose.yml up
dotnet run --project src/LinkTracker.Scrapper.Api
dotnet run --project src/LinkTracker.Bot.Api
dotnet run --project src/LinkTracker.AiAgent.Api
```

### Для запуска в контейнерах
9. В [bot.Docker.appesettings](src/LinkTracker.Bot.Api/appsettings.Docker.json), [scrapper.Docker.appsettings](src/LinkTracker.Scrapper.Api/appsettings.Docker.json) и [aiagent.Docker.appesettings](src/LinkTracker.AiAgent.Api/appsettings.Docker.json) в ветках Scrapper, Bot и AiAgent соответственно выбрать валидные параметры.
10. Выполнить поочердено запуск сначала [docker-compose](docker-compose.yml), [Scrapper](src/LinkTracker.Scrapper.Api/Program.cs), а затем [Bot](src/LinkTracker.Bot.Api/Program.cs) с помощью команд.
```
docker compose -f docker-compose.yml -f docker-compose.apps.yml up 
```


---
## Инструкция для запуска тестов

1. Выполнить запуск команды из корня
```
dotnet test
```

---
## Итоги нагрузочного тестирования

Нагрузочное тестирование выполнялось на данных объёмом 100 000 ссылок, 1 000 пользователей и примерно 100 ссылок на пользователя.

Основной эффект Valkey-кэша заметен на `GET /links`.

| Метрика `GET /links` | Без кэша | С Valkey | Изменение |
|---|---:|---:|---:|
| RPS | 70.85 | 76.83 | +8.44% |
| Avg latency | 50.44 ms | 5.30 ms | -89.49% |
| p50 | 19.52 ms | 2.95 ms | -84.88% |
| p99 | 283.45 ms | 19.76 ms | -93.03% |
| Успешные ответы 200 | 25 507 | 29 482 | +3 975 |
| Ошибки 500/502/504 | 0 | 0 | без изменений |
