using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Models;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class ListCommand(IScrapperClient scrapperClient) : ICommandDescriptor, ICommandHandler
{
    public string Name => "list";
    public string Description => "Вывести список отслеживаемых ссылок";
    public bool ShowInHelp => true;
    public bool ShowInTelegramMenu => true;

    public bool CanHandle(string text)
    {
        return CommandTextMatcher.Matches(text, Name);
    }

    public async Task<OutgoingMessage> ExecuteAsync(long chatId, string text, CancellationToken ct = default)
    {
        var args = ArgsExtractor.ExtractArgs(text);
        var tag = args;

        return await ScrapperCommandHelper.ExecuteAsync(
            chatId,
            async token =>
            {
                var response = await scrapperClient.GetLinksAsync(chatId, token);
                var items = response.Links;

                if (!string.IsNullOrEmpty(tag))
                {
                    items = items
                        .Where(x => x.Tags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
                        .ToList();
                }

                if (items.Count == 0)
                {
                    return new OutgoingMessage(
                        chatId,
                        string.IsNullOrEmpty(tag)
                            ? "Пока нет отслеживаемых ссылок. Добавь через /track"
                            : $"Не нашёл отслеживаемых ссылок с тегом «{tag}».");
                }

                var lines = items.Select((x, i) =>
                {
                    var tagsText = x.Tags.Count == 0 ? "—" : string.Join(", ", x.Tags);
                    return $"{i + 1}) {x.Url}\n   Теги: {tagsText}";
                });

                var header = string.IsNullOrEmpty(tag)
                    ? "Отслеживаемые ссылки:\n\n"
                    : $"Отслеживаемые ссылки (тег: {tag}):\n\n";

                var reply = header + string.Join("\n\n", lines);
                return new OutgoingMessage(chatId, reply);
            },
            "Не удалось выполнить команду из-за ошибки scrapper. Попробуй позже.",
            ct);
    }
}