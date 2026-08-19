using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Shared.Infrastructure.RateLimiting;

public static class ApiRateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(ApiRateLimitingOptions.SectionName)
            .Get<ApiRateLimitingOptions>() ?? new ApiRateLimitingOptions();

        services
            .AddOptions<ApiRateLimitingOptions>()
            .Bind(configuration.GetSection(ApiRateLimitingOptions.SectionName))
            .Validate(o => o.PermitLimit > 0, "RateLimiting:PermitLimit must be positive")
            .Validate(o => o.WindowSeconds > 0, "RateLimiting:WindowSeconds must be positive")
            .Validate(o => o.SegmentsPerWindow > 0, "RateLimiting:SegmentsPerWindow must be positive")
            .Validate(o => o.QueueLimit >= 0, "RateLimiting:QueueLimit must not be negative")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.PartitionHeaderName),
                "RateLimiting:PartitionHeaderName must be set")
            .Validate(
                o => o.TrustedNetworks.All(network => IPNetwork.TryParse(network, out _)),
                "RateLimiting:TrustedNetworks must contain CIDR notation values")
            .ValidateOnStart();

        var partitionKeyResolver = new RateLimitPartitionKeyResolver(options);

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            rateLimiterOptions.AddPolicy(
                RateLimitingPolicies.PublicApi,
                context => partitionKeyResolver.IsTrusted(context)
                    ? RateLimitPartition.GetNoLimiter(RateLimitPartitionKeyResolver.TrustedPartitionKey)
                    : RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKeyResolver.Resolve(context),
                        _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = options.PermitLimit,
                            Window = TimeSpan.FromSeconds(options.WindowSeconds),
                            SegmentsPerWindow = options.SegmentsPerWindow,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = options.QueueLimit
                        }));
        });

        return services;
    }
}
