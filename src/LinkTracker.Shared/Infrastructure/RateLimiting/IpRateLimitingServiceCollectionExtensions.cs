using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Shared.Infrastructure.RateLimiting;

public static class IpRateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddIpRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(IpRateLimitingOptions.SectionName)
            .Get<IpRateLimitingOptions>() ?? new IpRateLimitingOptions();

        services
            .AddOptions<IpRateLimitingOptions>()
            .Bind(configuration.GetSection(IpRateLimitingOptions.SectionName))
            .Validate(o => o.PermitLimit > 0, "RateLimiting:PermitLimit must be positive")
            .Validate(o => o.WindowSeconds > 0, "RateLimiting:WindowSeconds must be positive")
            .Validate(o => o.SegmentsPerWindow > 0, "RateLimiting:SegmentsPerWindow must be positive")
            .Validate(o => o.QueueLimit >= 0, "RateLimiting:QueueLimit must not be negative")
            .ValidateOnStart();

        services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    ipAddress,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = options.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds),
                        SegmentsPerWindow = options.SegmentsPerWindow,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = options.QueueLimit
                    });
            });
        });

        return services;
    }
}