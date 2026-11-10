namespace LinkTracker.Bot.Application.Localization;

public interface IChatMenuUpdater
{
    Task UpdateAsync(long chatId, BotLanguage language, CancellationToken ct);
}
