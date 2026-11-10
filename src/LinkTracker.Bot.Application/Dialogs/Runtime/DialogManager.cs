using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Localization;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Dialogs.Runtime;

public sealed class DialogManager(
    IDialogStateStore store,
    IEnumerable<IDialog> dialogs,
    IStringLocalizer<Messages> localizer)
{
    private readonly IReadOnlyDictionary<string, IDialog> _dialogs =
        dialogs.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

    public async Task<(bool handled, string replyText)> TryHandleAsync(BotRequest request, CancellationToken ct)
    {
        var ctx = await store.GetOrCreateAsync(request.ChatId, ct);

        if (!ctx.HasActiveDialog)
        {
            return (false, "");
        }

        if (!_dialogs.TryGetValue(ctx.ActiveDialogId!, out var dialog) || !dialog.Nodes.TryGetValue(ctx.ActiveNodeId!, out var node))
        {
            ctx.Reset();
            await store.SaveAsync(ctx, ct);
            return (true, localizer[Texts.DialogSessionCorrupted]);
        }

        var result = await node.HandleAsync(ctx, request, ct);

        if (result.EndDialog)
        {
            ctx.Reset();
        }
        else if (!string.IsNullOrWhiteSpace(result.NextNodeId))
        {
            ctx.ActiveNodeId = result.NextNodeId;
        }

        await store.SaveAsync(ctx, ct);
        return (true, result.ReplyText);
    }

    public async Task<string> StartAsync(string dialogId, long chatId, CancellationToken ct)
    {
        if (!_dialogs.TryGetValue(dialogId, out var dialog))
        {
            return localizer[Texts.DialogUnknown];
        }

        var ctx = await store.GetOrCreateAsync(chatId, ct);
        ctx.ActiveDialogId = dialog.Id;
        ctx.ActiveNodeId = dialog.StartNodeId;
        ctx.Data.Clear();

        await store.SaveAsync(ctx, ct);
        return await dialog.OnStartAsync(ctx, ct);
    }

    public async Task<string> CancelAsync(long chatId, CancellationToken ct)
    {
        await store.ResetAsync(chatId, ct);
        return localizer[Texts.DialogCancelled];
    }

    public async Task<bool> ResetAsync(long chatId, CancellationToken ct)
    {
        var ctx = await store.GetOrCreateAsync(chatId, ct);
        var hadActiveDialog = ctx.HasActiveDialog;

        await store.ResetAsync(chatId, ct);

        return hadActiveDialog;
    }
}