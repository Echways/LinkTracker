using LinkTracker.Scrapper.Application.Clients.GitHub;
using LinkTracker.Scrapper.Application.Clients.GitHub.Contracts;
using LinkTracker.Scrapper.Application.Models.Updates;
using LinkTracker.Scrapper.Application.Services.Updates.Clients;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace LinkTracker.Tests.Scrapper.Unit.Application.Services.Updates.Clients;

[Trait("Module", "Scrapper")]
[Trait("Category", "Unit")]
public sealed class GitHubLinkUpdateHandlerTests
{
    [Fact]
    public async Task CheckAsync_WhenLastUpdatedAtIsNull_ReturnsInitialStateWithoutEvents()
    {
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = Substitute.For<ILogger<GitHubLinkUpdateHandler>>();

        var repositoryUpdatedAt = new DateTimeOffset(2025, 3, 10, 12, 0, 0, TimeSpan.Zero);

        gitHubClient.GetRepositoryAsync("user", "repo", Arg.Any<CancellationToken>())
            .Returns(new GitHubRepositoryResponse { FullName = "user/repo", HtmlUrl = new Uri("https://github.com/user/repo"), UpdatedAt = repositoryUpdatedAt });

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://github.com/user/repo"), TgChatIds = [1001L], LastUpdatedAt = null };

        var sut = new GitHubLinkUpdateHandler(gitHubClient, logger);

        var result = await sut.CheckAsync(subscription);

        Assert.False(result.HasChanges);
        Assert.Empty(result.Events);
        Assert.Equal(repositoryUpdatedAt, result.NewLastUpdatedAt);

        await gitHubClient.Received(1)
            .GetRepositoryAsync("user", "repo", Arg.Any<CancellationToken>());

        await gitHubClient.DidNotReceive()
            .GetIssuesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>());

        await gitHubClient.DidNotReceive()
            .GetPullRequestsAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_WhenNoNewIssuesOrPullRequests_ReturnsNoChangesAndKeepsLastUpdatedAt()
    {
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = Substitute.For<ILogger<GitHubLinkUpdateHandler>>();

        var lastSeenAt = new DateTimeOffset(2025, 3, 9, 12, 0, 0, TimeSpan.Zero);

        gitHubClient.GetIssuesAsync("user", "repo", Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new GitHubIssueResponse
                {
                    Title = "Old issue",
                    Body = "Already seen",
                    CreatedAt = lastSeenAt.AddMinutes(-1),
                    HtmlUrl = new Uri("https://github.com/user/repo/issues/1"),
                    User = new GitHubUserResponse { Login = "octocat" }
                }
            ]);

        gitHubClient.GetPullRequestsAsync("user", "repo", Arg.Any<CancellationToken>())
            .Returns(
            [
                new GitHubPullRequestResponse
                {
                    Title = "Old pr",
                    Body = "Already seen",
                    CreatedAt = lastSeenAt.AddMinutes(-1),
                    HtmlUrl = new Uri("https://github.com/user/repo/pull/1"),
                    User = new GitHubUserResponse { Login = "octocat" }
                }
            ]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://github.com/user/repo"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt };

        var sut = new GitHubLinkUpdateHandler(gitHubClient, logger);

        var result = await sut.CheckAsync(subscription);

        Assert.False(result.HasChanges);
        Assert.Empty(result.Events);
        Assert.Equal(lastSeenAt, result.NewLastUpdatedAt);

        await gitHubClient.DidNotReceive()
            .GetRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(nameof(LinkEventKind.Issue))]
    [InlineData(nameof(LinkEventKind.PullRequest))]
    public async Task CheckAsync_WhenSingleNewEventExists_ReturnsMappedEvent(string eventKindName)
    {
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = Substitute.For<ILogger<GitHubLinkUpdateHandler>>();

        var lastSeenAt = new DateTimeOffset(2025, 3, 9, 12, 0, 0, TimeSpan.Zero);
        var createdAt = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.Zero);

        var expectedKind = Enum.Parse<LinkEventKind>(eventKindName);

        if (expectedKind == LinkEventKind.Issue)
        {
            gitHubClient.GetIssuesAsync("user", "repo", Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
                .Returns(
                [
                    new GitHubIssueResponse
                    {
                        Title = "New issue",
                        Body = "Issue body text that should appear in preview",
                        CreatedAt = createdAt,
                        HtmlUrl = new Uri("https://github.com/user/repo/issues/1"),
                        User = new GitHubUserResponse { Login = "octocat" }
                    }
                ]);

            gitHubClient.GetPullRequestsAsync("user", "repo", Arg.Any<CancellationToken>())
                .Returns(Array.Empty<GitHubPullRequestResponse>());
        }
        else
        {
            gitHubClient.GetIssuesAsync("user", "repo", Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<GitHubIssueResponse>());

            gitHubClient.GetPullRequestsAsync("user", "repo", Arg.Any<CancellationToken>())
                .Returns(
                [
                    new GitHubPullRequestResponse
                    {
                        Title = "New pull request",
                        Body = "Pull request body text that should appear in preview",
                        CreatedAt = createdAt,
                        HtmlUrl = new Uri("https://github.com/user/repo/pull/1"),
                        User = new GitHubUserResponse { Login = "octocat" }
                    }
                ]);
        }

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://github.com/user/repo"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt };

        var sut = new GitHubLinkUpdateHandler(gitHubClient, logger);

        var result = await sut.CheckAsync(subscription);

        Assert.True(result.HasChanges);

        var linkEvent = Assert.Single(result.Events);
        Assert.Equal(expectedKind, linkEvent.EventKind);
        Assert.Equal(LinkSourceKind.GitHub, linkEvent.SourceKind);
        Assert.Equal("octocat", linkEvent.UserName);
        Assert.Equal(createdAt, linkEvent.CreatedAt);
        Assert.Equal(createdAt, result.NewLastUpdatedAt);

        if (expectedKind == LinkEventKind.Issue)
        {
            Assert.Equal("New issue", linkEvent.Title);
            Assert.Equal("Issue body text that should appear in preview", linkEvent.Body);
            Assert.Equal(new Uri("https://github.com/user/repo/issues/1"), linkEvent.ResourceUrl);
        }
        else
        {
            Assert.Equal("New pull request", linkEvent.Title);
            Assert.Equal("Pull request body text that should appear in preview", linkEvent.Body);
            Assert.Equal(new Uri("https://github.com/user/repo/pull/1"), linkEvent.ResourceUrl);
        }

        await gitHubClient.DidNotReceive()
            .GetRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_WhenIssueAndPullRequestExist_ReturnsOrderedEventsWithoutDuplicatingPullRequestsFromIssues()
    {
        var gitHubClient = Substitute.For<IGitHubClient>();
        var logger = Substitute.For<ILogger<GitHubLinkUpdateHandler>>();

        var lastSeenAt = new DateTimeOffset(2025, 3, 9, 12, 0, 0, TimeSpan.Zero);

        var issueCreatedAt = new DateTimeOffset(2025, 3, 10, 10, 0, 0, TimeSpan.Zero);
        var prCreatedAt = new DateTimeOffset(2025, 3, 10, 11, 0, 0, TimeSpan.Zero);

        gitHubClient.GetIssuesAsync("user", "repo", Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                new GitHubIssueResponse
                {
                    Title = "Real issue",
                    Body = "Issue body",
                    CreatedAt = issueCreatedAt,
                    HtmlUrl = new Uri("https://github.com/user/repo/issues/1"),
                    User = new GitHubUserResponse { Login = "octocat" },
                    PullRequest = null
                },
                new GitHubIssueResponse
                {
                    Title = "PR mirrored in issues endpoint",
                    Body = "Should be ignored as issue",
                    CreatedAt = prCreatedAt,
                    HtmlUrl = new Uri("https://github.com/user/repo/issues/2"),
                    User = new GitHubUserResponse { Login = "octocat" },
                    PullRequest = new object()
                }
            ]);

        gitHubClient.GetPullRequestsAsync("user", "repo", Arg.Any<CancellationToken>())
            .Returns(
            [
                new GitHubPullRequestResponse
                {
                    Title = "Real pull request",
                    Body = "PR body",
                    CreatedAt = prCreatedAt,
                    HtmlUrl = new Uri("https://github.com/user/repo/pull/2"),
                    User = new GitHubUserResponse { Login = "octocat" }
                }
            ]);

        var subscription = new TrackedLinkSubscription { Id = 1, Url = new Uri("https://github.com/user/repo"), TgChatIds = [1001L], LastUpdatedAt = lastSeenAt };

        var sut = new GitHubLinkUpdateHandler(gitHubClient, logger);

        var result = await sut.CheckAsync(subscription);

        Assert.True(result.HasChanges);
        Assert.Equal(2, result.Events.Count);

        Assert.Equal(LinkEventKind.Issue, result.Events[0].EventKind);
        Assert.Equal("Real issue", result.Events[0].Title);

        Assert.Equal(LinkEventKind.PullRequest, result.Events[1].EventKind);
        Assert.Equal("Real pull request", result.Events[1].Title);

        Assert.Equal(prCreatedAt, result.NewLastUpdatedAt);

        await gitHubClient.DidNotReceive()
            .GetRepositoryAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}