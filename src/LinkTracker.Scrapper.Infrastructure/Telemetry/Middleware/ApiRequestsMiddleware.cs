using LinkTracker.Shared.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Http;

namespace LinkTracker.Scrapper.Infrastructure.Telemetry.Middleware;

public sealed class ApiRequestsMiddleware(
    RequestDelegate next,
    ScrapperMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        metrics.ApiRequests.Add(
            1,
            new KeyValuePair<string, object?>("source", HttpRouteLabel.Resolve(context)));

        await next(context);
    }
}
