using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace LinkTracker.Shared.Infrastructure.Resilience;

public static class HttpClientBuilderResilienceExtensions
{
    public static IHttpClientBuilder AddConfiguredHttpResilience(
        this IHttpClientBuilder builder,
        string pipelineName,
        HttpResilienceOptions options)
    {
        var handledStatusCodes = options.Retry.RetryableStatusCodes
            .Select(code => (HttpStatusCode)code)
            .ToHashSet();

        builder.AddResilienceHandler(pipelineName, resilience =>
        {
            if (options.Retry.MaxRetryAttempts > 0)
            {
                resilience.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.Retry.MaxRetryAttempts,
                    Delay = TimeSpan.FromMilliseconds(options.Retry.BackoffMilliseconds),
                    BackoffType = MapBackoffStrategy(options.Retry.BackoffStrategy),
                    UseJitter = false,
                    ShouldRetryAfterHeader = false,
                    ShouldHandle = args => ShouldHandleAsync(args.Outcome, handledStatusCodes)
                });
            }

            resilience.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = options.CircuitBreaker.FailureRateThreshold / 100d,
                SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreaker.SamplingDurationSeconds),
                MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
                BreakDuration = TimeSpan.FromMilliseconds(options.CircuitBreaker.WaitDurationInOpenStateMilliseconds),
                ShouldHandle = args => ShouldHandleAsync(args.Outcome, handledStatusCodes)
            });

            resilience.AddTimeout(TimeSpan.FromMilliseconds(options.TimeoutMilliseconds));
        });

        return builder;
    }

    private static ValueTask<bool> ShouldHandleAsync(
        Outcome<HttpResponseMessage> outcome,
        IReadOnlySet<HttpStatusCode> handledStatusCodes)
    {
        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
        {
            return ValueTask.FromResult(true);
        }

        if (outcome.Result is null)
        {
            return ValueTask.FromResult(false);
        }

        return ValueTask.FromResult(handledStatusCodes.Contains(outcome.Result.StatusCode));
    }

    private static DelayBackoffType MapBackoffStrategy(HttpRetryBackoffStrategy strategy)
    {
        return strategy switch
        {
            HttpRetryBackoffStrategy.Constant => DelayBackoffType.Constant,
            HttpRetryBackoffStrategy.Exponential => DelayBackoffType.Exponential,
            _ => throw new ArgumentOutOfRangeException(
                nameof(strategy),
                strategy,
                "Unsupported retry backoff strategy")
        };
    }
}