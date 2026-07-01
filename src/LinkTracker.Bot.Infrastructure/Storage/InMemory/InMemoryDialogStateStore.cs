using System.Collections.Concurrent;
using LinkTracker.Bot.Application.Dialogs.Abstractions;

namespace LinkTracker.Bot.Infrastructure.Storage.InMemory;

public sealed class InMemoryDialogStateStore : IDialogStateStore
{
    private readonly ConcurrentDictionary<long, DialogContext> _states = new();

    public Task<DialogContext> GetOrCreateAsync(long chatId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var context = _states.GetOrAdd(chatId, static id => new DialogContext { ChatId = id });

        return Task.FromResult(context);
    }

    public Task SaveAsync(DialogContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(ctx);

        _states[ctx.ChatId] = ctx;
        return Task.CompletedTask;
    }

    public Task ResetAsync(long chatId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _states.TryRemove(chatId, out _);
        return Task.CompletedTask;
    }
}