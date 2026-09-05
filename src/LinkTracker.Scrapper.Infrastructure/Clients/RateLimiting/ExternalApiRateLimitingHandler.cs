using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace LinkTracker.Scrapper.Infrastructure.Clients.RateLimiting;

public sealed class ExternalApiRateLimitingHandler(
    ExternalApiRateLimiter rateLimiter,
    TimeProvider timeProvider,
    ScrapperMetrics metrics,
    ILogger<ExternalApiRateLimitingHandler> logger) : DelegatingHandler
{
    private const string RemainingHeader = "X-RateLimit-Remaining";
    private const string ResetHeader = "X-RateLimit-Reset";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await WaitForCooldownAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(rateLimiter.AcquireTimeout);

        using var lease = await AcquireAsync(timeout.Token, cancellationToken);

        if (!lease.IsAcquired)
        {
            throw new ExternalApiRateLimitedException(rateLimiter.ApiName);
        }

        var response = await base.SendAsync(request, cancellationToken);

        ApplyThrottlingHints(response);

        return response;
    }

    private async Task<RateLimitLease> AcquireAsync(
        CancellationToken acquireToken,
        CancellationToken requestToken)
    {
        try
        {
            return await rateLimiter.AcquireAsync(acquireToken);
        }
        catch (OperationCanceledException) when (!requestToken.IsCancellationRequested)
        {
            throw new ExternalApiRateLimitedException(rateLimiter.ApiName);
        }
    }

    private async Task WaitForCooldownAsync(CancellationToken ct)
    {
        var remaining = rateLimiter.RemainingCooldown;

        if (remaining <= TimeSpan.Zero)
        {
            return;
        }

        if (remaining > rateLimiter.AcquireTimeout)
        {
            throw new ExternalApiRateLimitedException(rateLimiter.ApiName);
        }

        logger.LogWarning(
            "Waiting for external API rate limit reset. Api={Api}, DelaySeconds={DelaySeconds}",
            rateLimiter.ApiName,
            remaining.TotalSeconds);

        await Task.Delay(remaining, timeProvider, ct);
    }

    private void ApplyThrottlingHints(HttpResponseMessage response)
    {
        var cooldownUntil = TryGetResetAt(response) ?? TryGetRetryAfter(response);

        if (cooldownUntil is null)
        {
            return;
        }

        rateLimiter.Cooldown(cooldownUntil.Value);

        metrics.Errors.Add(
            1,
            new KeyValuePair<string, object?>("scope", "external_source"),
            new KeyValuePair<string, object?>("scope_type", rateLimiter.ApiName),
            new KeyValuePair<string, object?>("reason", "rate_limited"));

        logger.LogWarning(
            "External API reported rate limit exhaustion. Api={Api}, Status={Status}, CooldownUntil={CooldownUntil}",
            rateLimiter.ApiName,
            (int)response.StatusCode,
            cooldownUntil.Value);
    }

    private DateTimeOffset? TryGetResetAt(HttpResponseMessage response)
    {
        if (!TryGetHeaderValue(response, RemainingHeader, out var remainingValue) ||
            !long.TryParse(remainingValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var remaining) ||
            remaining > 0)
        {
            return null;
        }

        if (!TryGetHeaderValue(response, ResetHeader, out var resetValue) ||
            !long.TryParse(resetValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resetAtUnixSeconds))
        {
            return null;
        }

        return DateTimeOffset.FromUnixTimeSeconds(resetAtUnixSeconds);
    }

    private DateTimeOffset? TryGetRetryAfter(HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden))
        {
            return null;
        }

        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta)
        {
            return timeProvider.GetUtcNow() + delta;
        }

        return retryAfter?.Date;
    }

    private static bool TryGetHeaderValue(HttpResponseMessage response, string name, out string? value)
    {
        if (response.Headers.TryGetValues(name, out var values))
        {
            value = values.FirstOrDefault();
            return value is not null;
        }

        value = null;
        return false;
    }
}
