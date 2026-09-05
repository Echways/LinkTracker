using LinkTracker.Scrapper.Application.Clients.Reddit;
using LinkTracker.Scrapper.Application.Clients.Reddit.Contracts;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Application.Services.Updates;
using LinkTracker.Scrapper.Application.Services.Updates.Clients;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LinkTracker.Tests.Scrapper.Unit.Application.Services.Updates.Clients;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class RedditLinkUpdateHandlerTests
{
    private readonly IRedditClient _redditClient = Substitute.For<IRedditClient>();
    private readonly ILogger<RedditLinkUpdateHandler> _logger = Substitute.For<ILogger<RedditLinkUpdateHandler>>();

    [Theory]
    [InlineData("https://reddit.com/r/dotnet", true)]
    [InlineData("https://www.reddit.com/r/dotnet", true)]
    [InlineData("https://www.reddit.com/r/dotnet/", true)]
    [InlineData("https://www.reddit.com/r/dotnet/comments/abc123/title", false)]
    [InlineData("https://www.reddit.com/r/", false)]
    [InlineData("https://www.reddit.com/user/alice", false)]
    [InlineData("https://github.com/dotnet/runtime", false)]
    public void CanHandle_ReturnsExpectedResult(string rawUrl, bool expected)
    {
        var sut = new RedditLinkUpdateHandler(_redditClient, _logger);

        Assert.Equal(expected, sut.CanHandle(new Uri(rawUrl)));
    }

    [Fact]
    public async Task CheckAsync_WhenLastUpdatedAtIsNull_ReturnsInitialStateWithNewestPostCursor()
    {
        var newestCreatedAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        _redditClient.GetNewPostsAsync("dotnet", Arg.Any<CancellationToken>())
            .Returns([
                CreatePost("older", newestCreatedAt.AddHours(-1)),
                CreatePost("newest", newestCreatedAt)
            ]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://www.reddit.com/r/dotnet"), TgChatIds = [1001L], LastUpdatedAt = null };

        var sut = new RedditLinkUpdateHandler(_redditClient, _logger);

        var result = await sut.CheckAsync(subscription);

        Assert.False(result.HasChanges);
        Assert.Empty(result.Events);
        Assert.Equal(newestCreatedAt, result.NewLastUpdatedAt);
        Assert.Equal("post:newest", result.NewLastEventKey);
    }

    [Fact]
    public async Task CheckAsync_WhenSubredditHasNoPosts_ReturnsNoChangesWithoutCursor()
    {
        _redditClient.GetNewPostsAsync("dotnet", Arg.Any<CancellationToken>())
            .Returns([]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://www.reddit.com/r/dotnet"), TgChatIds = [1001L], LastUpdatedAt = null };

        var sut = new RedditLinkUpdateHandler(_redditClient, _logger);

        var result = await sut.CheckAsync(subscription);

        Assert.False(result.HasChanges);
        Assert.Null(result.NewLastUpdatedAt);
    }

    [Fact]
    public async Task CheckAsync_WhenAllPostsAreNotAfterCursor_ReturnsNoChangesAndKeepsCursor()
    {
        var lastSeenAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        _redditClient.GetNewPostsAsync("dotnet", Arg.Any<CancellationToken>())
            .Returns([
                CreatePost("seen", lastSeenAt),
                CreatePost("older", lastSeenAt.AddMinutes(-5))
            ]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://www.reddit.com/r/dotnet"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt, LastEventKey = "post:seen" };

        var sut = new RedditLinkUpdateHandler(_redditClient, _logger);

        var result = await sut.CheckAsync(subscription);

        Assert.False(result.HasChanges);
        Assert.Equal(lastSeenAt, result.NewLastUpdatedAt);
        Assert.Equal("post:seen", result.NewLastEventKey);
    }

    [Fact]
    public async Task CheckAsync_WhenNewPostsExist_ReturnsMappedEventsOrderedByCreatedAt()
    {
        var lastSeenAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        _redditClient.GetNewPostsAsync("dotnet", Arg.Any<CancellationToken>())
            .Returns([
                CreatePost("second", lastSeenAt.AddMinutes(10)),
                CreatePost("first", lastSeenAt.AddMinutes(5)),
                CreatePost("seen", lastSeenAt.AddMinutes(-5))
            ]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://www.reddit.com/r/dotnet"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt, LastEventKey = "post:old" };

        var sut = new RedditLinkUpdateHandler(_redditClient, _logger);

        var result = await sut.CheckAsync(subscription);

        Assert.True(result.HasChanges);
        Assert.Equal(2, result.Events.Count);

        Assert.Equal("post:first", result.Events[0].EventKey);
        Assert.Equal("post:second", result.Events[1].EventKey);

        Assert.Equal(lastSeenAt.AddMinutes(10), result.NewLastUpdatedAt);
        Assert.Equal("post:second", result.NewLastEventKey);

        var linkEvent = result.Events[0];

        Assert.Equal(LinkSourceKind.Reddit, linkEvent.SourceKind);
        Assert.Equal(LinkEventKind.Post, linkEvent.EventKind);
        Assert.Equal("Title of first", linkEvent.Title);
        Assert.Equal("author-first", linkEvent.UserName);
        Assert.Equal("Body of first", linkEvent.Body);
        Assert.Equal(new Uri("https://www.reddit.com/r/dotnet/comments/first/title/"), linkEvent.ResourceUrl);
    }

    [Fact]
    public async Task CheckAsync_ProducedEvent_IsFormattedAsRedditPostNotification()
    {
        var lastSeenAt = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        _redditClient.GetNewPostsAsync("dotnet", Arg.Any<CancellationToken>())
            .Returns([CreatePost("first", lastSeenAt.AddMinutes(5))]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://www.reddit.com/r/dotnet"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt };

        var sut = new RedditLinkUpdateHandler(_redditClient, _logger);

        var result = await sut.CheckAsync(subscription);

        var update = LinkUpdatePayloadMapper.ToBotUpdate(subscription, result.Events[0]);

        Assert.Contains("Источник: Reddit", update.Description, StringComparison.Ordinal);
        Assert.Contains("Тип: post", update.Description, StringComparison.Ordinal);
        Assert.Contains(
            "Ссылка: https://www.reddit.com/r/dotnet/comments/first/title/",
            update.Description,
            StringComparison.Ordinal);
        Assert.Equal("author-first", update.Author);
    }

    private static RedditPostResponse CreatePost(string id, DateTimeOffset createdAt)
    {
        return new RedditPostResponse
        {
            Id = id,
            Title = $"Title of {id}",
            Selftext = $"Body of {id}",
            Author = $"author-{id}",
            Permalink = $"/r/dotnet/comments/{id}/title/",
            CreatedUtcSeconds = createdAt.ToUnixTimeSeconds()
        };
    }
}
