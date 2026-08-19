using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LinkTracker.Shared.Infrastructure.Telemetry;

public static class HttpRouteLabel
{
    public const string Unmatched = "unmatched";

    public static string Resolve(HttpContext context)
    {
        if (context.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: { Length: > 0 } rawText })
        {
            return rawText.StartsWith('/') ? rawText : $"/{rawText}";
        }

        return Unmatched;
    }
}
