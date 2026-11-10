using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Helpers;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Shared.Links;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;

public sealed class AskUrlNode(IStringLocalizer<Messages> localizer) : IDialogNode
{
    public const string NodeId = "ask_url";
    public string Id => NodeId;

    public Task<DialogNodeResult> HandleAsync(DialogContext ctx, BotRequest request, CancellationToken ct)
    {
        if (!DialogText.TryGetText(request, out var raw))
        {
            return Task.FromResult(new DialogNodeResult(
                localizer[Texts.AskUrlNotText]));
        }

        if (!TrackedLinkUrl.TryParse(raw, out var uri))
        {
            return Task.FromResult(new DialogNodeResult(
                localizer[Texts.AskUrlInvalid]));
        }

        ctx.SetPendingUrl(uri.ToString());

        return Task.FromResult(new DialogNodeResult(
            BuildReply(ctx.GetPendingUrl()!),
            AskTagsNode.NodeId));
    }

    private string BuildReply(string url)
    {
        return localizer[Texts.AskUrlTagsPrompt, url];
    }
}