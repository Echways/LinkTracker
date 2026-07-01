using LinkTracker.Bot.Application.Models;

namespace LinkTracker.Bot.Application.Commands;

public interface ICommandHandler
{
    bool CanHandle(string text);
    Task<OutgoingMessage> ExecuteAsync(long chatId, string text, CancellationToken ct = default);
}