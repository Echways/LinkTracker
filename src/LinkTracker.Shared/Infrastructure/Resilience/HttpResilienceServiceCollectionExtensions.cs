using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Shared.Infrastructure.Resilience;

public static class HttpResilienceServiceCollectionExtensions
{
    public static IServiceCollection AddHttpResilienceOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<HttpResilienceOptions>()
            .Bind(configuration.GetSection(HttpResilienceOptions.SectionName))
            .Validate(o => o.TimeoutMilliseconds > 0, "Resilience:Http:TimeoutMilliseconds must be positive")
            .Validate(o => o.Retry.MaxRetryAttempts >= 0, "Resilience:Http:Retry:MaxRetryAttempts must not be negative")
            .Validate(o => o.Retry.BackoffMilliseconds >= 0, "Resilience:Http:Retry:BackoffMilliseconds must not be negative")
            .Validate(
                o => Enum.IsDefined(typeof(HttpRetryBackoffStrategy), o.Retry.BackoffStrategy),
                "Resilience:Http:Retry:BackoffStrategy must be Constant or Exponential")
            .Validate(
                o => o.Retry.RetryableStatusCodes.All(code => code is >= 100 and <= 599),
                "Resilience:Http:Retry:RetryableStatusCodes must contain valid HTTP status codes")
            .Validate(
                o => o.CircuitBreaker.FailureRateThreshold is > 0 and <= 100,
                "Resilience:Http:CircuitBreaker:FailureRateThreshold must be from 1 to 100")
            .Validate(
                o => o.CircuitBreaker.SamplingDurationSeconds > 0,
                "Resilience:Http:CircuitBreaker:SamplingDurationSeconds must be positive")
            .Validate(
                o => o.CircuitBreaker.MinimumThroughput >= 2,
                "Resilience:Http:CircuitBreaker:MinimumThroughput must be at least 2")
            .Validate(
                o => o.CircuitBreaker.WaitDurationInOpenStateMilliseconds >= 500,
                "Resilience:Http:CircuitBreaker:WaitDurationInOpenStateMilliseconds must be at least 500")
            .ValidateOnStart();

        return services;
    }

    public static HttpResilienceOptions GetHttpResilienceOptions(this IConfiguration configuration)
    {
        return configuration
            .GetSection(HttpResilienceOptions.SectionName)
            .Get<HttpResilienceOptions>() ?? new HttpResilienceOptions();
    }
}