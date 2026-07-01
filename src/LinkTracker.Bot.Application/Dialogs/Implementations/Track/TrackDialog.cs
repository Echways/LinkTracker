using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track;

public sealed class TrackDialog(AskUrlNode askUrl, TrackConfirmNode trackConfirm, AskTagsNode askTags) : IDialog
{
    public const string DialogId = "track";

    public string Id => DialogId;
    public string StartNodeId => AskUrlNode.NodeId;

    public IReadOnlyDictionary<string, IDialogNode> Nodes { get; } = new Dictionary<string, IDialogNode>(StringComparer.OrdinalIgnoreCase) { [askUrl.Id] = askUrl, [askTags.Id] = askTags, [trackConfirm.Id] = trackConfirm };

    public Task<string> OnStartAsync(DialogContext ctx, CancellationToken ct)
    {
        return Task.FromResult("Пришли ссылку, которую нужно отслеживать. (/cancel чтобы отменить)");
    }
}