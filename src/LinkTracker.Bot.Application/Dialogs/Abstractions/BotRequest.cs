namespace LinkTracker.Bot.Application.Dialogs.Abstractions;

public enum BotRequestType
{
    Text = 0,
    Command = 1,
    Callback = 2,
    Contact = 3,
    Unknown = 99
}

public sealed record BotRequest(
    long ChatId,
    BotRequestType Type,
    string? Text = null,
    string? Command = null,
    string? CallbackData = null,
    string? Phone = null
);