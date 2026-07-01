namespace LinkTracker.Bot.Application.Dialogs.Abstractions;

public interface IDialogStateStore
{
    Task<DialogContext> GetOrCreateAsync(long chatId, CancellationToken ct);
    Task SaveAsync(DialogContext ctx, CancellationToken ct);
    Task ResetAsync(long chatId, CancellationToken ct);
}