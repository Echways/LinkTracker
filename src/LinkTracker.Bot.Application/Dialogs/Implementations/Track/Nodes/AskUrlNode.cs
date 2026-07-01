using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Helpers;
using LinkTracker.Shared.Links;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;

public sealed class AskUrlNode : IDialogNode
{
    public const string NodeId = "ask_url";
    public string Id => NodeId;

    public Task<DialogNodeResult> HandleAsync(DialogContext ctx, BotRequest request, CancellationToken ct)
    {
        if (!DialogText.TryGetText(request, out var raw))
        {
            return Task.FromResult(new DialogNodeResult(
                "Пришли URL текстом (https://...)."));
        }

        if (!TrackedLinkUrl.TryParse(raw, out var uri))
        {
            return Task.FromResult(new DialogNodeResult(
                "Это не похоже на ссылку. Пришли URL целиком (https://...)."));
        }

        ctx.SetPendingUrl(uri.ToString());

        return Task.FromResult(new DialogNodeResult(
            BuildReply(ctx.GetPendingUrl()!),
            AskTagsNode.NodeId));
    }

    private static string BuildReply(string url)
    {
        return "Добавить теги? Введи через запятую (например: work, docs, refactor)" +
               "\nЧтобы пропустить — отправь '-'\n\n" +
               url;
    }
}