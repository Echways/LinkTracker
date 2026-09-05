using LinkTracker.Scrapper.Application.Clients.Reddit;
using LinkTracker.Scrapper.Application.Clients.Reddit.Contracts;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Application.Services.Helpers;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Application.Services.Updates.Clients;

public sealed class RedditLinkUpdateHandler(
    IRedditClient redditClient,
    ILogger<RedditLinkUpdateHandler> logger) : LinkUpdateHandlerBase(logger)
{
    public override bool CanHandle(Uri url)
    {
        return TryParseSubreddit(url, out _);
    }

    protected override async Task<LinkCheckResult> InitializeStateAsync(
        TrackedLinkSubscription subscription,
        CancellationToken ct)
    {
        TryParseSubreddit(subscription.Url, out var subreddit);

        var posts = await redditClient.GetNewPostsAsync(subreddit, ct);

        if (posts.Count == 0)
        {
            return LinkUpdateResultBuilder.NoChanges();
        }

        var newest = posts.MaxBy(x => x.CreatedAt)!;

        return LinkUpdateResultBuilder.InitialState(newest.CreatedAt, BuildEventKey(newest));
    }

    protected override async Task<IReadOnlyList<LinkEvent>> GetNewEventsAsync(
        TrackedLinkSubscription subscription,
        DateTimeOffset lastSeenAt,
        string? lastEventKey,
        CancellationToken ct)
    {
        TryParseSubreddit(subscription.Url, out var subreddit);

        var posts = await redditClient.GetNewPostsAsync(subreddit, ct);

        return posts
            .Select(x => MapPostToEvent(x, subscription.Url))
            .Where(x => IsAfterCursor(x, lastSeenAt, lastEventKey))
            .ToArray();
    }

    private static bool TryParseSubreddit(Uri url, out string subreddit)
    {
        subreddit = string.Empty;

        if (!UriParsingHelper.IsHost(url, "reddit.com"))
        {
            return false;
        }

        var segments = UriParsingHelper.GetPathSegments(url);
        if (segments.Length != 2)
        {
            return false;
        }

        if (!string.Equals(segments[0], "r", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        subreddit = segments[1];

        return !string.IsNullOrWhiteSpace(subreddit);
    }

    private static LinkEvent MapPostToEvent(RedditPostResponse post, Uri subredditUrl)
    {
        return new LinkEvent
        {
            SourceKind = LinkSourceKind.Reddit,
            EventKind = LinkEventKind.Post,
            Title = post.Title,
            UserName = post.Author,
            CreatedAt = post.CreatedAt,
            EventKey = BuildEventKey(post),
            Body = post.Selftext,
            ResourceUrl = BuildResourceUrl(post, subredditUrl)
        };
    }

    private static string BuildEventKey(RedditPostResponse post)
    {
        return $"post:{post.Id}";
    }

    private static Uri BuildResourceUrl(RedditPostResponse post, Uri subredditUrl)
    {
        return Uri.TryCreate(subredditUrl, post.Permalink, out var resourceUrl)
            ? resourceUrl
            : subredditUrl;
    }
}
