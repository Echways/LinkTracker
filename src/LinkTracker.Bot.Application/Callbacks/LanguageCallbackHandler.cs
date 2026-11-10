using LinkTracker.Bot.Application.Dialogs.Abstractions;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Callbacks;

public sealed class LanguageCallbackHandler(
    ChatLanguageService languages,
    IChatMenuUpdater menuUpdater,
    IStringLocalizer<Messages> localizer) : ICallbackHandler
{
    public const string Prefix = "lang:";

    public bool CanHandle(string callbackData)
    {
        return callbackData.StartsWith(Prefix, StringComparison.Ordinal)
               && BotLanguage.TryFind(callbackData[Prefix.Length..], out _);
    }

    public async Task<OutgoingMessage> HandleAsync(BotRequest request, CancellationToken ct)
    {
        BotLanguage.TryFind(request.CallbackData![Prefix.Length..], out var language);

        await languages.SetAsync(request.ChatId, language, ct);
        await menuUpdater.UpdateAsync(request.ChatId, language, ct);

        using var _ = UiCulture.Use(language);

        return new OutgoingMessage(
            request.ChatId,
            localizer[Texts.LanguageChanged, language.DisplayName],
            EditMessageId: request.MessageId);
    }
}
