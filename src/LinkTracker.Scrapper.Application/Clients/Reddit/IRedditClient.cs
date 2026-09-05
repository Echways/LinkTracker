using LinkTracker.Scrapper.Application.Clients.Reddit.Contracts;

namespace LinkTracker.Scrapper.Application.Clients.Reddit;

public interface IRedditClient
{
    Task<IReadOnlyList<RedditPostResponse>> GetNewPostsAsync(string subreddit, CancellationToken ct = default);
}
