namespace LinkTracker.Bot.Application.Models;

public record OutgoingMessage(
    long ChatId,
    string Text,
    IReadOnlyList<MessageButton>? Buttons = null,
    int? EditMessageId = null);

public sealed record MessageButton(string Text, string CallbackData);
