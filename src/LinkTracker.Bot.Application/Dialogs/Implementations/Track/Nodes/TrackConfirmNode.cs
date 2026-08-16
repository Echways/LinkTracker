using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Helpers;
using LinkTracker.Shared.Contracts.Common;
using LinkTracker.Shared.Links;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;

public sealed class TrackConfirmNode(IScrapperClient scrapperClient) : IDialogNode
{
    public const string NodeId = "track_confirm";
    public string Id => NodeId;

    public async Task<DialogNodeResult> HandleAsync(DialogContext ctx, BotRequest request, CancellationToken ct)
    {
        if (!DialogText.TryGetText(request, out var raw))
        {
            return new DialogNodeResult("'да' / 'нет'.");
        }

        if (DialogText.IsYes(raw))
        {
            return await ConfirmAsync(ctx, ct);
        }

        if (!DialogText.IsNo(raw))
        {
            return new DialogNodeResult("'да' / 'нет'.");
        }

        ctx.ClearTrackState();

        return new DialogNodeResult(
            "Ок.\nПришли другую ссылку.",
            AskUrlNode.NodeId);
    }

    private async Task<DialogNodeResult> ConfirmAsync(DialogContext ctx, CancellationToken ct)
    {
        var pendingUrl = ctx.GetPendingUrl();
        if (string.IsNullOrWhiteSpace(pendingUrl))
        {
            return new DialogNodeResult(
                "Не удалось понять, что добавлять. Начни заново: /track",
                EndDialog: true);
        }

        if (!TrackedLinkUrl.TryParse(pendingUrl, out var uri))
        {
            return new DialogNodeResult(
                "Не удалось разобрать ссылку. Начни заново: /track",
                EndDialog: true);
        }

        var tags = TagsCsvSplitter.SplitCommaSeparated(ctx.GetTagsCsv());
        var tagsText = tags.Length == 0 ? "—" : string.Join(", ", tags);

        try
        {
            await scrapperClient.AddLinkAsync(ctx.ChatId, uri, tags, ct);

            return new DialogNodeResult(
                $"Начал отслеживать:\n{pendingUrl}\nТеги: {tagsText}",
                EndDialog: true);
        }
        catch (ScrapperClientException ex) when (ScrapperErrorMessageMapper.TryMap(ex, out var message))
        {
            var shouldRetryUrl =
                ex.HasCode(ScrapperErrorCodes.UnsupportedLink) ||
                ex.HasCode(ScrapperErrorCodes.InvalidLink) ||
                ex.HasCode(ScrapperErrorCodes.InvalidLinkScheme);

            if (shouldRetryUrl)
            {
                return new DialogNodeResult(
                    message + "\n\nПришли другую ссылку.",
                    AskUrlNode.NodeId);
            }

            return new DialogNodeResult(
                message,
                EndDialog: true);
        }
        catch (ScrapperClientException)
        {
            return new DialogNodeResult(
                "Не удалось добавить ссылку из-за ошибки scrapper. Попробуй ещё раз позже.",
                EndDialog: true);
        }
    }
}