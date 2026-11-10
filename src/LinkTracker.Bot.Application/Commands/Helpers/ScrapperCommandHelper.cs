using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Localization;
using LinkTracker.Bot.Application.Models;
using Microsoft.Extensions.Localization;

namespace LinkTracker.Bot.Application.Commands.Helpers;

public static class ScrapperCommandHelper
{
    public static async Task<OutgoingMessage> ExecuteAsync(
        long chatId,
        Func<CancellationToken, Task<string>> action,
        IStringLocalizer<Messages> localizer,
        string fallbackKey,
        CancellationToken ct = default)
    {
        return await ExecuteAsync(
            chatId,
            async token => new OutgoingMessage(chatId, await action(token)),
            localizer,
            fallbackKey,
            ct);
    }

    public static async Task<OutgoingMessage> ExecuteAsync(
        long chatId,
        Func<CancellationToken, Task<OutgoingMessage>> action,
        IStringLocalizer<Messages> localizer,
        string fallbackKey,
        CancellationToken ct = default)
    {
        try
        {
            return await action(ct);
        }
        catch (ScrapperClientException ex) when (ScrapperErrorMessageMapper.TryMap(ex, localizer, out var message))
        {
            return new OutgoingMessage(chatId, message);
        }
        catch (ScrapperClientException)
        {
            return new OutgoingMessage(chatId, localizer[fallbackKey]);
        }
    }
}
