using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using LinkTracker.Scrapper.Infrastructure.Clients.Reddit.Contracts;
using LinkTracker.Scrapper.Infrastructure.Configuration.Clients;
using Microsoft.Extensions.Options;

namespace LinkTracker.Scrapper.Infrastructure.Clients.Reddit;

internal sealed class RedditAccessTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<RedditOptions> options,
    TimeProvider timeProvider) : IRedditAccessTokenProvider
{
    public const string HttpClientName = "reddit-token";

    private static readonly TimeSpan ExpirationSafetyMargin = TimeSpan.FromMinutes(1);

    private readonly RedditOptions _options = options.Value;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        if (TryGetCachedToken(out var cached))
        {
            return cached;
        }

        await _semaphore.WaitAsync(ct);

        try
        {
            if (TryGetCachedToken(out cached))
            {
                return cached;
            }

            var response = await RequestAccessTokenAsync(ct);
            var accessToken = response.AccessToken;

            _accessToken = accessToken;
            _expiresAt = timeProvider.GetUtcNow().AddSeconds(response.ExpiresInSeconds) - ExpirationSafetyMargin;

            return accessToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private bool TryGetCachedToken(out string accessToken)
    {
        accessToken = _accessToken ?? string.Empty;

        return accessToken.Length > 0 && timeProvider.GetUtcNow() < _expiresAt;
    }

    private async Task<RedditAccessTokenResponse> RequestAccessTokenAsync(CancellationToken ct)
    {
        using var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            ])
        };

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}")));

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<RedditAccessTokenResponse>(ct);

        return body is null || body.AccessToken.Length == 0
            ? throw new InvalidOperationException("Reddit returned an empty access token response.")
            : body;
    }
}
