namespace LinkTracker.Bot.Application.Localization;

public sealed class ChatLanguageService(ILanguageStore store)
{
    public async Task<BotLanguage> GetAsync(long chatId, CancellationToken ct)
    {
        var code = await store.GetAsync(chatId, ct);
        BotLanguage.TryFind(code, out var language);

        return language;
    }

    public Task SetAsync(long chatId, BotLanguage language, CancellationToken ct)
    {
        return store.SetAsync(chatId, language.Code, ct);
    }
}
