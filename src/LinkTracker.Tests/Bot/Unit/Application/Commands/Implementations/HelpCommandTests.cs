using LinkTracker.Bot.Application.Commands;
using LinkTracker.Bot.Application.Commands.Implementations;
using NSubstitute;

namespace LinkTracker.Tests.Bot.Unit.Application.Commands.Implementations;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class HelpCommandTests
{
    [Theory]
    [InlineData("/help")]
    [InlineData("/Help")]
    [InlineData("   /help")]
    [InlineData("/help foo")]
    public void CanHandle_WhenTextStartsWithHelpCommand_ReturnsTrue(string text)
    {
        var sut = new HelpCommand(new Lazy<IEnumerable<ICommandDescriptor>>(Array.Empty<ICommandDescriptor>));

        Assert.True(sut.CanHandle(text));
    }

    [Theory]
    [InlineData("/start")]
    [InlineData("help")]
    [InlineData("/unknown")]
    [InlineData("")]
    public void CanHandle_WhenTextIsNotHelpCommand_ReturnsFalse(string text)
    {
        var sut = new HelpCommand(new Lazy<IEnumerable<ICommandDescriptor>>(Array.Empty<ICommandDescriptor>));

        Assert.False(sut.CanHandle(text));
    }

    [Fact]
    public async Task Execute_ReturnsExpectedOutcomingMessage()
    {
        var chatId = 123;

        var start = Substitute.For<ICommandDescriptor>();
        start.Name.Returns("start");
        start.Description.Returns("Запуск бота");
        start.ShowInHelp.Returns(true);

        var list = Substitute.For<ICommandDescriptor>();
        list.Name.Returns("list");
        list.Description.Returns("Список ссылок");
        list.ShowInHelp.Returns(true);

        var help = Substitute.For<ICommandDescriptor>();
        help.Name.Returns("help");
        help.Description.Returns("Показать справку");
        help.ShowInHelp.Returns(true);

        var sut = new HelpCommand(new Lazy<IEnumerable<ICommandDescriptor>>(() => [start, list, help]));

        var result = await sut.ExecuteAsync(chatId, "/help");

        Assert.Equal(chatId, result.ChatId);
        Assert.Contains("/start", result.Text);
        Assert.Contains("/list", result.Text);
        Assert.DoesNotContain("/help", result.Text);
        Assert.StartsWith("Доступные команды:", result.Text);
    }
}