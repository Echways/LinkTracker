using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Dialogs.Helpers;

namespace LinkTracker.Bot.Application.Dialogs.Implementations.Track.Nodes;

public sealed class AskTagsNode : IDialogNode
{
    public const string NodeId = "ask_tags";
    public string Id => NodeId;

    public Task<DialogNodeResult> HandleAsync(DialogContext ctx, BotRequest request, CancellationToken ct)
    {
        if (!DialogText.TryGetText(request, out var raw))
        {
            return Task.FromResult(new DialogNodeResult(BuildValidationMessage()));
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

    private static string BuildValidationMessage()
    {
        return "Пришли теги через запятую (например: работа, баг, документация).\n" +
               "Чтобы пропустить — отправь '-'";
    }

    private static string BuildInvalidTagsMessage(IReadOnlyList<string> invalid)
    {
        var msg =
            "Не распознал ни одного корректного тега.\n" +
            "Разрешены буквы/цифры/_/-, длина 1–24. Пример: work, docs, refactor";

        if (invalid.Count > 0)
        {
            msg += $"\nПроблемные: {string.Join(", ", invalid.Take(5))}";
        }

        return msg;
    }

    private static string BuildAcceptedTagsReply(
        DialogContext ctx,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> invalid)
    {
        var warn = invalid.Count > 0
            ? $"\nИгнорирую некорректные: {string.Join(", ", invalid.Take(5))}"
            : "";

        return $"Ок.\nТеги: {string.Join(", ", tags)}{warn}\n\n{BuildConfirmText(ctx)}";
    }

    private static string BuildConfirmText(DialogContext ctx)
    {
        var url = ctx.GetPendingUrl() ?? "(unknown)";
        var tagsCsv = ctx.GetTagsCsv();
        var tagsText = string.IsNullOrWhiteSpace(tagsCsv) ? "—" : string.Join(", ", tagsCsv.Split(','));

        return $"Добавить в отслеживание?\nСсылка: {url}\nТеги: {tagsText}\nда / нет";
    }
}