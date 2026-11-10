# Bot

[English](bot.md) · [Русский](../ru/bot.md) · [Documentation](README.md)

The Bot is the only part users see. It receives Telegram updates, runs commands and dialogs, and delivers notifications.

## Commands

| Command | Action |
| ------- | ------ |
| `/start` | Registers the chat in the Scrapper and greets the user |
| `/help` | Lists the commands |
| `/track` | Starts the tracking dialog |
| `/untrack <url>` | Stops tracking the link |
| `/list` | Shows tracked links with their tags |
| `/language` | Offers a choice of language with buttons |
| `/cancel` | Cancels the current dialog |

The command menu in Telegram is updated to the language of the chat.

## Tracking dialog

`/track` is a short state machine:

1. **Link.** The bot asks for a URL and checks that it points to a supported source.
2. **Tags.** The bot asks for tags separated by commas, up to ten, duplicates removed. Invalid tags are reported.
3. **Confirmation.** The link is sent to the Scrapper and the dialog ends.

`/cancel` leaves the dialog at any step. Dialog state is stored in Valkey for `DialogTtlSeconds`, so it survives a restart.

## Languages

English is the default. `/language` stores a choice per chat, and every text, error and menu entry follows it. Texts live in resource files:

| File | Language |
| ---- | -------- |
| `Messages.resx` | English |
| `Messages.ru.resx` | Russian |

To add a language, add a `Messages.<culture>.resx` file, a value in `BotLanguage` and a button in the language callback.

## Notifications

The Bot consumes `link.processed-updates` from Kafka and sends each message to its chat. A message that fails after the retries goes to `link.processed-updates-dlq`. The Bot also accepts updates directly on `POST /updates` and over gRPC when the Scrapper runs with `Http` or `Grpc` transport.

## Errors

Errors of the Scrapper are mapped to short messages in the chat language: chat already registered or not registered, link already tracked, link not found, invalid or unsupported link, Scrapper unavailable.
