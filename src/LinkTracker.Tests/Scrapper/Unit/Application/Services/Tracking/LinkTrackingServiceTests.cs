using System.Net;
using LinkTracker.Scrapper.Application.Abstractions.Updates;
using LinkTracker.Scrapper.Application.Errors;
using LinkTracker.Scrapper.Application.Services.Tracking;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using NSubstitute;

namespace LinkTracker.Tests.Scrapper.Unit.Application.Services.Tracking;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class LinkTrackingServiceTests
{
    private readonly ILinkUpdateHandler _handler = Substitute.For<ILinkUpdateHandler>();
    private readonly ILinkTrackingStore _store = Substitute.For<ILinkTrackingStore>();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AddLinkAsync_WhenChatIdIsInvalid_ThrowsInvalidChatId(long chatId)
    {
        var sut = CreateSut();
        Uri link = new("https://github.com/test/repo");

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AddLinkAsync(chatId, link, []));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("invalid_chat_id", exception.Code);

        await _store.DidNotReceive()
            .ChatExistsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("/relative", "invalid_link")]
    [InlineData("ftp://github.com/test/repo", "invalid_link_scheme")]
    public async Task AddLinkAsync_WhenLinkIsInvalid_ThrowsValidationError(string rawLink, string expectedCode)
    {
        var sut = CreateSut();
        var link = rawLink.StartsWith("/", StringComparison.Ordinal)
            ? new Uri(rawLink, UriKind.Relative)
            : new Uri(rawLink);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AddLinkAsync(1, link, []));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(expectedCode, exception.Code);

        await _store.DidNotReceive()
            .ChatExistsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddLinkAsync_WhenLinkIsUnsupported_ThrowsUnsupportedLink()
    {
        var sut = CreateSut();
        Uri link = new("https://example.com/page");
        _handler.CanHandle(link).Returns(false);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AddLinkAsync(1, link, []));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("unsupported_link", exception.Code);

        await _store.DidNotReceive()
            .ChatExistsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddLinkAsync_WhenChatDoesNotExist_ThrowsChatNotFound()
    {
        var sut = CreateSut();
        Uri link = new("https://github.com/test/repo");

        _handler.CanHandle(link).Returns(true);
        _store.ChatExistsAsync(1, Arg.Any<CancellationToken>()).Returns(false);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AddLinkAsync(1, link, ["tag"]));

        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        Assert.Equal("chat_not_found", exception.Code);

        await _store.DidNotReceive()
            .TryAddAsync(
                Arg.Any<long>(),
                Arg.Any<Uri>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddLinkAsync_WhenLinkAlreadyExists_ThrowsLinkAlreadyExists()
    {
        var sut = CreateSut();
        Uri link = new("https://github.com/test/repo");

        _handler.CanHandle(link).Returns(true);
        _store.ChatExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        _store.TryAddAsync(
                1,
                link,
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns((TrackedLinkRecord?)null);

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.AddLinkAsync(1, link, ["tag"]));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("link_already_exists", exception.Code);
    }

    [Fact]
    public async Task AddLinkAsync_WhenRequestIsValid_ReturnsTrackedLinkRecord()
    {
        var sut = CreateSut();
        Uri link = new("https://github.com/test/repo");
        string[] tags = ["tag1", "tag2"];

        TrackedLinkRecord expected = new()
        {
            Id = 10,
            Url = link,
            Tags = tags,
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        _handler.CanHandle(link).Returns(true);
        _store.ChatExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        _store.TryAddAsync(
                1,
                link,
                Arg.Is<IReadOnlyList<string>>(value => value.SequenceEqual(tags)),
                Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await sut.AddLinkAsync(1, link, tags);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task AddLinkAsync_WhenRequestIsValid_PassesTagsToStore()
    {
        var sut = CreateSut();
        Uri link = new("https://github.com/test/repo");
        string[] tags = ["tag1", "tag2"];

        _handler.CanHandle(link).Returns(true);
        _store.ChatExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);
        _store.TryAddAsync(
                Arg.Any<long>(),
                Arg.Any<Uri>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(new TrackedLinkRecord
            {
                Id = 1,
                Url = link,
                Tags = tags,
                LastUpdatedAt = DateTimeOffset.UtcNow
            });

        await sut.AddLinkAsync(1, link, tags);

        await _store.Received(1).TryAddAsync(
            1,
            link,
            Arg.Is<IReadOnlyList<string>>(value => value.SequenceEqual(tags)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveLinkAsync_WhenSchemeIsInvalid_ThrowsInvalidLinkScheme()
    {
        var sut = CreateSut();
        Uri link = new("ftp://github.com/test/repo");

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            sut.RemoveLinkAsync(1, link));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("invalid_link_scheme", exception.Code);

        await _store.DidNotReceive()
            .ChatExistsAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
    }

    private LinkTrackingService CreateSut()
    {
        return new LinkTrackingService(_store, [_handler]);
    }
}