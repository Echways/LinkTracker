using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Helpers;
using LinkTracker.Bot.Application.Localization;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;

public sealed class AskTagsNode(IStringLocalizer<Messages> localizer) : IDialogNode
{
    public const string NodeId = "ask_tags";
    public string Id => NodeId;

    public Task<DialogNodeResult> HandleAsync(DialogContext ctx, BotRequest request, CancellationToken ct)
    {
        if (!DialogText.TryGetText(request, out var raw))
        {
            return Task.FromResult(new DialogNodeResult(localizer[Texts.AskTagsNotText]));
        }

        if (ShouldSkip(raw))
        {
            ctx.ClearTagsCsv();
            return Task.FromResult(new DialogNodeResult(
                BuildConfirmText(ctx),
                TrackConfirmNode.NodeId));
        }

        var (tags, invalid) = TagParser.ParseCommaSeparated(raw);
        if (tags.Count == 0)
        {
            return Task.FromResult(new DialogNodeResult(
                BuildInvalidTagsMessage(invalid)));
        }

        ctx.SetTagsCsv(string.Join(",", tags));

        return Task.FromResult(new DialogNodeResult(
            BuildAcceptedTagsReply(ctx, tags, invalid),
            TrackConfirmNode.NodeId));
    }

    private static bool ShouldSkip(string raw)
    {
        return raw is "-" or "skip" or "пропустить";
    }

    private string BuildInvalidTagsMessage(IReadOnlyList<string> invalid)
    {
        var message = localizer[Texts.AskTagsInvalid].Value;

        return invalid.Count > 0
            ? message + localizer[Texts.AskTagsInvalidList, string.Join(", ", invalid.Take(5))]
            : message;
    }

    private string BuildAcceptedTagsReply(
        DialogContext ctx,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> invalid)
    {
        var ignored = invalid.Count > 0
            ? localizer[Texts.AskTagsIgnored, string.Join(", ", invalid.Take(5))].Value
            : string.Empty;

        return localizer[Texts.AskTagsAccepted, string.Join(", ", tags), ignored, BuildConfirmText(ctx)];
    }

    private string BuildConfirmText(DialogContext ctx)
    {
        var url = ctx.GetPendingUrl() ?? localizer[Texts.ConfirmUnknownUrl];
        var tagsCsv = ctx.GetTagsCsv();
        var tagsText = string.IsNullOrWhiteSpace(tagsCsv) ? "—" : string.Join(", ", tagsCsv.Split(','));

        return localizer[Texts.ConfirmPrompt, url, tagsText];
    }
}
