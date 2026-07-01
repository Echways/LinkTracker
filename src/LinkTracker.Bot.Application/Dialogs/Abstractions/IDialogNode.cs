namespace LinkTracker.Bot.Application.Dialogs.Abstractions;

public interface IDialogNode
{
    string Id { get; }

    Task<DialogNodeResult> HandleAsync(DialogContext ctx, BotRequest request, CancellationToken ct);
}

public sealed record DialogNodeResult(
    string ReplyText,
    string? NextNodeId = null,
    bool EndDialog = false
);