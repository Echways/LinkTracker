using LinkTracker.Bot.Application.Callbacks;
using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class LanguageCommand(
    ChatLanguageService languages,
    IStringLocalizer<Messages> localizer) : ICommandDescriptor, ICommandHandler
{
    public string Name => "language";
    public string Description => localizer[Texts.LanguageDescription];
    public bool ShowInHelp => true;
    public bool ShowInTelegramMenu => true;

    public bool CanHandle(string text)
    {
        return CommandTextMatcher.Matches(text, Name);
    }

    public async Task<OutgoingMessage> ExecuteAsync(long chatId, string text, CancellationToken ct = default)
    {
        var current = await languages.GetAsync(chatId, ct);

        var buttons = BotLanguage.All
            .Select(l => new MessageButton(
                l == current ? $"✅ {l.DisplayName}" : l.DisplayName,
                LanguageCallbackHandler.Prefix + l.Code))
            .ToList();

        return new OutgoingMessage(chatId, localizer[Texts.LanguagePrompt], buttons);
    }
}
