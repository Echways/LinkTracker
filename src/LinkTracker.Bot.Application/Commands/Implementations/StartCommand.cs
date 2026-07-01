using LinkTracker.Bot.Application.Clients.Scrapper;
using LinkTracker.Bot.Application.Commands.Helpers;
using LinkTracker.Bot.Application.Models;

namespace LinkTracker.Bot.Application.Commands.Implementations;

public sealed class StartCommand(IScrapperClient scrapperClient) : ICommandDescriptor, ICommandHandler
{
    public string Name => "start";
    public string Description => "Зарегистрировать чат и начать работу";
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
                return "Привет! Я помогу отслеживать обновления по ссылкам. \nДоступные команды ты можешь посмотреть в справке /help";
            },
            "Не удалось выполнить команду из-за ошибки scrapper. Попробуй позже.",
            ct);
    }
}