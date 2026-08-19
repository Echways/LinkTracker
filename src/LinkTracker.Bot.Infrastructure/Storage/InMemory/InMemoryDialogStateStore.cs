using System.Collections.Concurrent;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Infrastructure.Configuration.Valkey;
using Microsoft.Extensions.Options;

namespace LinkTracker.Bot.Infrastructure.Storage.InMemory;

public sealed class InMemoryDialogStateStore : IDialogStateStore
{
    private readonly ConcurrentDictionary<long, Entry> _states = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;

    public InMemoryDialogStateStore(IOptions<DialogStateStoreOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _ttl = TimeSpan.FromSeconds(options.Value.DialogTtlSeconds);
        _timeProvider = timeProvider;
    }

    public Task<DialogContext> GetOrCreateAsync(long chatId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        EvictExpired();

        var now = _timeProvider.GetUtcNow();

        if (_states.TryGetValue(chatId, out var entry) && entry.ExpiresAt > now)
        {
            return Task.FromResult(entry.Context);
        }

        return Task.FromResult(new DialogContext { ChatId = chatId });
    }

    public Task SaveAsync(DialogContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(ctx);

        _states[ctx.ChatId] = new Entry(ctx, _timeProvider.GetUtcNow() + _ttl);

        return Task.CompletedTask;
    }

    public Task ResetAsync(long chatId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _states.TryRemove(chatId, out _);

        return Task.CompletedTask;
    }

    private void EvictExpired()
    {
        var now = _timeProvider.GetUtcNow();

        foreach (var (chatId, entry) in _states)
        {
            if (entry.ExpiresAt <= now)
            {
                _states.TryRemove(new KeyValuePair<long, Entry>(chatId, entry));
            }
        }
    }

    private sealed record Entry(DialogContext Context, DateTimeOffset ExpiresAt);
}
