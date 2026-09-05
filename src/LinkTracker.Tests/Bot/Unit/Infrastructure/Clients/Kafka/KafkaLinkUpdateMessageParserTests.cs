using LinkTracker.Bot.Infrastructure.Clients.Kafka;
using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Tests.Bot.Unit.Infrastructure.Clients.Kafka;

[Trait("Module", "Bot")]
[Trait("Category", "Unit")]
public sealed class KafkaLinkUpdateMessageParserTests
{
    private readonly KafkaLinkUpdateMessageParser _sut = new();

    [Fact]
    public void TryValidate_WhenUpdateIsValid_ReturnsTrue()
    {
        var update = CreateValidUpdate();

        var result = _sut.TryValidate(update, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_WhenIdIsZero_ReturnsTrue()
    {
        var update = CreateValidUpdate(0);

        var result = _sut.TryValidate(update, out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void TryValidate_WhenUpdateIsNull_ReturnsError()
    {
        var result = _sut.TryValidate(null, out var error);

        Assert.False(result);
        Assert.Equal("Failed to deserialize the message.", error);
    }

    [Fact]
    public void TryValidate_WhenIdIsNegative_ReturnsError()
    {
        var update = CreateValidUpdate(-1);

        var result = _sut.TryValidate(update, out var error);

        Assert.False(result);
        Assert.Equal("Field 'id' must not be negative.", error);
    }

    [Fact]
    public void TryValidate_WhenUrlIsRelative_ReturnsError()
    {
        var update = CreateValidUpdate(url: new Uri("/relative/path", UriKind.Relative));

        var result = _sut.TryValidate(update, out var error);

        Assert.False(result);
        Assert.Equal("Field 'url' must contain an absolute URI.", error);
    }

    [Fact]
    public void TryValidate_WhenTgChatIdsIsEmpty_ReturnsError()
    {
        var update = CreateValidUpdate(tgChatIds: []);

        var result = _sut.TryValidate(update, out var error);

        Assert.False(result);
        Assert.Equal("Field 'tgChatIds' must contain at least one chat id.", error);
    }

    private static LinkUpdate CreateValidUpdate(
        long id = 42,
        Uri? url = null,
        IReadOnlyList<long>? tgChatIds = null)
    {
        return new LinkUpdate { Id = id, Url = url ?? new Uri("https://github.com/user/repo"), Description = "Repository updated", TgChatIds = tgChatIds ?? [123, 456] };
    }
}