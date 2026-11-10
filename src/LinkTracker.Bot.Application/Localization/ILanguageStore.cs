namespace LinkTracker.Bot.Application.Localization;

public interface ILanguageStore
{
    Task<string?> GetAsync(long chatId, CancellationToken ct);
    Task SetAsync(long chatId, string languageCode, CancellationToken ct);
}
