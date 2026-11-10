using System.Collections.Concurrent;
using LinkTracker.Bot.Application.Localization;

namespace LinkTracker.Bot.Infrastructure.Storage.InMemory;

public sealed class InMemoryLanguageStore : ILanguageStore
{
    private readonly ConcurrentDictionary<long, string> _languages = new();

    public Task<string?> GetAsync(long chatId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(_languages.GetValueOrDefault(chatId));
    }

    public Task SetAsync(long chatId, string languageCode, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _languages[chatId] = languageCode;

        return Task.CompletedTask;
    }
}
