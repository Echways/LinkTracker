using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Models;
using LinkTracker.Shared.Links;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class UntrackCommand(IScrapperClient scrapperClient) : ICommandDescriptor, ICommandHandler
{
    public string Name => "untrack";
    public string Description => "Перестать отслеживать ссылку: /untrack <url>";
    public bool ShowInHelp => true;
    public bool ShowInTelegramMenu => true;

    public bool CanHandle(string text)
    {
        return CommandTextMatcher.Matches(text, Name);
    }

    public async Task<OutgoingMessage> ExecuteAsync(long chatId, string text, CancellationToken ct = default)
    {
        var args = ArgsExtractor.ExtractArgs(text);

        if (string.IsNullOrWhiteSpace(args))
        {
            return new OutgoingMessage(
                chatId,
                "Пришли команду в формате:\n/untrack <url>");
        }

        if (!TrackedLinkUrl.TryParse(args, out var uri))
        {
            return new OutgoingMessage(
                chatId,
                "Это не похоже на корректную ссылку.\nИспользуй формат:\n/untrack <url>");
        }

        return await ScrapperCommandHelper.ExecuteAsync(
            chatId,
            async token =>
            {
                await scrapperClient.RemoveLinkAsync(chatId, uri, token);
                return $"Больше не отслеживаю:\n{uri}";
            },
            "Не удалось удалить ссылку из-за ошибки scrapper. Попробуй позже.",
            ct);
    }
}