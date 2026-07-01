namespace LinkTracker.Bot.Application.Dialogs.Abstractions;

public interface IDialog
{
    string Id { get; }
    string StartNodeId { get; }
    IReadOnlyDictionary<string, IDialogNode> Nodes { get; }

    Task<string> OnStartAsync(DialogContext ctx, CancellationToken ct);
}