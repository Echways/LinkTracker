using LinkTracker.Bot.Application.Telemetry.Abstractions;
using LinkTracker.Bot.Presentation.Telegram.Notifications;
using LinkTracker.Shared.Constants;
using LinkTracker.Shared.Contracts.AiAgent;
using LinkTracker.Shared.Contracts.Bot;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;

namespace LinkTracker.Tests.Bot.Unit.Presentation.Telegram.Notifications;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class LinkUpdateNotifierTests
{
    private static readonly Uri Url = new("https://github.com/dotnet/runtime");

    [Theory]
    [InlineData(LinkUpdatePriority.High, "‼️ Важное обновление по ссылке:")]
    [InlineData(LinkUpdatePriority.Medium, "Обновление по ссылке:")]
    [InlineData(LinkUpdatePriority.Low, "Незначительное обновление по ссылке:")]
    public async Task NotifyAsync_RendersHeaderMatchingPriority(
        LinkUpdatePriority priority,
        string expectedHeader)
    {
        var (botClient, sentTexts) = CreateBotClient();
        var sut = new LinkUpdateNotifier(botClient, Substitute.For<IBotMetrics>());

        await sut.NotifyAsync(
            new LinkUpdate { Id = 1, Url = Url, Description = "Новый issue", TgChatIds = [42], Priority = priority },
            CancellationToken.None);

        var text = Assert.Single(sentTexts);

        Assert.StartsWith(expectedHeader, text, StringComparison.Ordinal);
        Assert.Contains("Новый issue", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotifyAsync_WhenPriorityIsNotSet_UsesNeutralHeader()
    {
        var (botClient, sentTexts) = CreateBotClient();
        var sut = new LinkUpdateNotifier(botClient, Substitute.For<IBotMetrics>());

        // Сырой путь Scrapper -> Bot приоритет не проставляет.
        await sut.NotifyAsync(
            new LinkUpdate { Id = 1, Url = Url, Description = "Новый issue", TgChatIds = [42] },
            CancellationToken.None);

        Assert.StartsWith("Обновление по ссылке:", Assert.Single(sentTexts), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotifyAsync_WhenSystemReport_StripsMarkerAndSkipsPriorityHeader()
    {
        var (botClient, sentTexts) = CreateBotClient();
        var sut = new LinkUpdateNotifier(botClient, Substitute.For<IBotMetrics>());

        await sut.NotifyAsync(
            new LinkUpdate
            {
                Id = 0,
                Url = Url,
                Description = $"{SystemMessageMarkers.FailedLinkReport}Не удалось проверить часть ссылок",
                TgChatIds = [42],
                Priority = LinkUpdatePriority.High
            },
            CancellationToken.None);

        var text = Assert.Single(sentTexts);

        Assert.Equal("Не удалось проверить часть ссылок", text);
    }

    private static (ITelegramBotClient BotClient, List<string> SentTexts) CreateBotClient()
    {
        var botClient = Substitute.For<ITelegramBotClient>();
        var sentTexts = new List<string>();

        botClient
            .SendRequest(Arg.Any<SendMessageRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                sentTexts.Add(call.Arg<SendMessageRequest>().Text);
                return Task.FromResult(new Message());
            });

        return (botClient, sentTexts);
    }
}
