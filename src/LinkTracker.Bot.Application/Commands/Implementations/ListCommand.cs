using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class ListCommand(IScrapperClient scrapperClient, IStringLocalizer<Messages> localizer) : ICommandDescriptor, ICommandHandler
{
    public string Name => "list";
    public string Description => localizer[Texts.ListDescription];
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
                            ? localizer[Texts.ListEmpty]
                            : localizer[Texts.ListEmptyForTag, tag]);
                }

                var lines = items.Select((x, i) =>
                {
                    var tagsText = x.Tags.Count == 0 ? "—" : string.Join(", ", x.Tags);
                    return localizer[Texts.ListItem, i + 1, x.Url, tagsText].Value;
                });

                var header = string.IsNullOrEmpty(tag)
                    ? localizer[Texts.ListHeader].Value
                    : localizer[Texts.ListHeaderForTag, tag].Value;

                var reply = header + string.Join("\n\n", lines);
                return new OutgoingMessage(chatId, reply);
            },
            localizer,
            Texts.CommandScrapperFailed,
            ct);
    }
}