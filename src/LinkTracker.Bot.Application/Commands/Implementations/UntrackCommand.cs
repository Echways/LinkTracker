using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using Microsoft.Extensions.Localization;
using LinkTracker.Shared.Links;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class UntrackCommand(IScrapperClient scrapperClient, IStringLocalizer<Messages> localizer) : ICommandDescriptor, ICommandHandler
{
    public string Name => "untrack";
    public string Description => localizer[Texts.UntrackDescription];
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
                localizer[Texts.UntrackUsage]);
        }

        if (!TrackedLinkUrl.TryParse(args, out var uri))
        {
            return new OutgoingMessage(
                chatId,
                localizer[Texts.UntrackInvalidUrl]);
        }

        return await ScrapperCommandHelper.ExecuteAsync(
            chatId,
            async token =>
            {
                await scrapperClient.RemoveLinkAsync(chatId, uri, token);
                return localizer[Texts.UntrackSuccess, uri];
            },
            localizer,
            Texts.UntrackScrapperFailed,
            ct);
    }
}