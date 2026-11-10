using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class StartCommand(IScrapperClient scrapperClient, IStringLocalizer<Messages> localizer) : ICommandDescriptor, ICommandHandler
{
    public string Name => "start";
    public string Description => localizer[Texts.StartDescription];
    public bool ShowInHelp => true;
    public bool ShowInTelegramMenu => true;

    public bool CanHandle(string text)
    {
        return CommandTextMatcher.Matches(text, Name);
    }

    public async Task<OutgoingMessage> ExecuteAsync(long chatId, string text, CancellationToken ct = default)
    {
        return await ScrapperCommandHelper.ExecuteAsync(
            chatId,
            async token =>
            {
                await scrapperClient.RegisterChatAsync(chatId, token);
                return localizer[Texts.StartGreeting];
            },
            localizer,
            Texts.CommandScrapperFailed,
            ct);
    }
}