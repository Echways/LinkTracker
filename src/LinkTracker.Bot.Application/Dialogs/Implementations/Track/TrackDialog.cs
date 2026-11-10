using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;
using LinkTracker.Bot.Application.Localization;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track;

public sealed class TrackDialog(
    AskUrlNode askUrl,
    TrackConfirmNode trackConfirm,
    AskTagsNode askTags,
    IStringLocalizer<Messages> localizer) : IDialog
{
    public const string DialogId = "track";

    public string Id => DialogId;
    public string StartNodeId => AskUrlNode.NodeId;

    public IReadOnlyDictionary<string, IDialogNode> Nodes { get; } = new Dictionary<string, IDialogNode>(StringComparer.OrdinalIgnoreCase) { [askUrl.Id] = askUrl, [askTags.Id] = askTags, [trackConfirm.Id] = trackConfirm };

    public Task<string> OnStartAsync(DialogContext ctx, CancellationToken ct)
    {
        return Task.FromResult(localizer[Texts.TrackStartPrompt].Value);
    }
}