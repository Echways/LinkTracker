using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Models;

namespace LinkTracker.Bot.Application.Commands.Helpers;

public static class ScrapperCommandHelper
{
    public static async Task<OutgoingMessage> ExecuteAsync(
        long chatId,
        Func<CancellationToken, Task<string>> action,
        string fallbackMessage,
        CancellationToken ct = default)
    {
        try
        {
            var text = await action(ct);
            return new OutgoingMessage(chatId, text);
        }
        catch (ScrapperClientException ex) when (ScrapperErrorMessageMapper.TryMap(ex, out var message))
        {
            return new OutgoingMessage(chatId, message);
        }
        catch (ScrapperClientException)
        {
            return new OutgoingMessage(chatId, fallbackMessage);
        }
    }

    public static async Task<OutgoingMessage> ExecuteAsync(
        long chatId,
        Func<CancellationToken, Task<OutgoingMessage>> action,
        string fallbackMessage,
        CancellationToken ct = default)
    {
        try
        {
            return await action(ct);
        }
        catch (ScrapperClientException ex) when (ScrapperErrorMessageMapper.TryMap(ex, out var message))
        {
            return new OutgoingMessage(chatId, message);
        }
        catch (ScrapperClientException)
        {
            return new OutgoingMessage(chatId, fallbackMessage);
        }
    }
}