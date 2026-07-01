using LinkTracker.Scrapper.Application.Clients.GitHub;
using LinkTracker.Scrapper.Application.Clients.GitHub.Contracts;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Application.Services.Helpers;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Application.Services.Updates.Clients;

public sealed class GitHubLinkUpdateHandler(
    IGitHubClient gitHubClient,
    ILogger<GitHubLinkUpdateHandler> logger) : LinkUpdateHandlerBase(logger)
{
    public override bool CanHandle(Uri url)
    {
        return TryParseRepository(url, out _, out _);
    }

    protected override async Task<LinkCheckResult> InitializeStateAsync(
        TrackedLinkSubscription subscription,
        CancellationToken ct)
    {
        TryParseRepository(subscription.Url, out var owner, out var repository);

        var repositoryResponse = await gitHubClient.GetRepositoryAsync(owner, repository, ct);
        return LinkUpdateResultBuilder.InitialState(repositoryResponse.UpdatedAt);
    }

    protected override async Task<IReadOnlyList<LinkEvent>> GetNewEventsAsync(
        TrackedLinkSubscription subscription,
        DateTimeOffset lastSeenAt,
        string? lastEventKey,
        CancellationToken ct)
    {
        TryParseRepository(subscription.Url, out var owner, out var repository);

        var issuesTask = gitHubClient.GetIssuesAsync(owner, repository, ct);
        var pullRequestsTask = gitHubClient.GetPullRequestsAsync(owner, repository, ct);

        await Task.WhenAll(issuesTask, pullRequestsTask);

        var issues = await issuesTask;
        var pullRequests = await pullRequestsTask;

        var events = new List<LinkEvent>();

        events.AddRange(
            issues
                .Where(x => x.PullRequest is null)
                .Select(MapIssueToEvent)
                .Where(x => IsAfterCursor(x, lastSeenAt, lastEventKey)));

        events.AddRange(
            pullRequests
                .Select(MapPullRequestToEvent)
                .Where(x => IsAfterCursor(x, lastSeenAt, lastEventKey)));

        return events;
    }

    private static bool TryParseRepository(Uri url, out string owner, out string repository)
    {
        owner = string.Empty;
        repository = string.Empty;

        if (!UriParsingHelper.IsHost(url, "github.com"))
        {
            return false;
        }

        var segments = UriParsingHelper.GetPathSegments(url);
        if (segments.Length != 2)
        {
            return false;
        }

        owner = segments[0];
        repository = segments[1];

        return !string.IsNullOrWhiteSpace(owner)
               && !string.IsNullOrWhiteSpace(repository);
    }

    private static LinkEvent MapIssueToEvent(GitHubIssueResponse issue)
    {
        return new LinkEvent
        {
            SourceKind = LinkSourceKind.GitHub,
            EventKind = LinkEventKind.Issue,
            Title = issue.Title,
            UserName = issue.User?.Login ?? string.Empty,
            CreatedAt = issue.CreatedAt,
            EventKey = $"issue:{issue.Id}",
            Body = issue.Body ?? string.Empty,
            ResourceUrl = issue.HtmlUrl
        };
    }

    private static LinkEvent MapPullRequestToEvent(GitHubPullRequestResponse pullRequest)
    {
        return new LinkEvent
        {
            SourceKind = LinkSourceKind.GitHub,
            EventKind = LinkEventKind.PullRequest,
            Title = pullRequest.Title,
            UserName = pullRequest.User?.Login ?? string.Empty,
            CreatedAt = pullRequest.CreatedAt,
            EventKey = $"pr:{pullRequest.Id}",
            Body = pullRequest.Body ?? string.Empty,
            ResourceUrl = pullRequest.HtmlUrl
        };
    }
}