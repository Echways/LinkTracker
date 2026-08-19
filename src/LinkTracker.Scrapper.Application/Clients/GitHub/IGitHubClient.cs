using LinkTracker.Scrapper.Application.Clients.GitHub.Contracts;

namespace LinkTracker.Scrapper.Application.Clients.GitHub;

public interface IGitHubClient
{
    Task<GitHubRepositoryResponse> GetRepositoryAsync(string owner, string repository, CancellationToken ct = default);

    Task<IReadOnlyList<GitHubIssueResponse>> GetIssuesAsync(
        string owner,
        string repository,
        DateTimeOffset? since = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<GitHubPullRequestResponse>> GetPullRequestsAsync(
        string owner,
        string repository,
        CancellationToken ct = default);
}
