using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Helpers;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Shared.Contracts.Common;
using LinkTracker.Shared.Links;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;

public sealed class TrackConfirmNode(IScrapperClient scrapperClient, IStringLocalizer<Messages> localizer) : IDialogNode
{
    public const string NodeId = "track_confirm";
    public string Id => NodeId;

    public async Task<DialogNodeResult> HandleAsync(DialogContext ctx, BotRequest request, CancellationToken ct)
    {
        if (!DialogText.TryGetText(request, out var raw))
        {
            return new DialogNodeResult(localizer[Texts.ConfirmYesNo]);
        }

        if (DialogText.IsYes(raw))
        {
            return await ConfirmAsync(ctx, ct);
        }

        if (!DialogText.IsNo(raw))
        {
            return new DialogNodeResult(localizer[Texts.ConfirmYesNo]);
        }

        ctx.ClearTrackState();

        return new DialogNodeResult(
            localizer[Texts.ConfirmRejected],
            AskUrlNode.NodeId);
    }

    private async Task<DialogNodeResult> ConfirmAsync(DialogContext ctx, CancellationToken ct)
    {
        var pendingUrl = ctx.GetPendingUrl();
        if (string.IsNullOrWhiteSpace(pendingUrl))
        {
            return new DialogNodeResult(
                localizer[Texts.ConfirmNothingPending],
                EndDialog: true);
        }

        if (!TrackedLinkUrl.TryParse(pendingUrl, out var uri))
        {
            return new DialogNodeResult(
                localizer[Texts.ConfirmUrlParseFailed],
                EndDialog: true);
        }

        var tags = TagsCsvSplitter.SplitCommaSeparated(ctx.GetTagsCsv());
        var tagsText = tags.Length == 0 ? "—" : string.Join(", ", tags);

        try
        {
            await scrapperClient.AddLinkAsync(ctx.ChatId, uri, tags, ct);

            return new DialogNodeResult(
                localizer[Texts.ConfirmSuccess, pendingUrl, tagsText],
                EndDialog: true);
        }
        catch (ScrapperClientException ex) when (ScrapperErrorMessageMapper.TryMap(ex, localizer, out var message))
        {
            var shouldRetryUrl =
                ex.HasCode(ScrapperErrorCodes.UnsupportedLink) ||
                ex.HasCode(ScrapperErrorCodes.InvalidLink) ||
                ex.HasCode(ScrapperErrorCodes.InvalidLinkScheme);

            if (shouldRetryUrl)
            {
                return new DialogNodeResult(
                    localizer[Texts.ConfirmRetryUrl, message],
                    AskUrlNode.NodeId);
            }

            return new DialogNodeResult(
                message,
                EndDialog: true);
        }
        catch (ScrapperClientException)
        {
            return new DialogNodeResult(
                localizer[Texts.TrackScrapperFailed],
                EndDialog: true);
        }
    }
}