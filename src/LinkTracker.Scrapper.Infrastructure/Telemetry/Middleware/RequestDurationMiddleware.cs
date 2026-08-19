using System.Diagnostics;
using LinkTracker.Shared.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Http;

namespace LinkTracker.Scrapper.Infrastructure.Telemetry.Middleware;

public sealed class RequestDurationMiddleware(
    RequestDelegate next,
    ScrapperMetrics metrics)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var route = HttpRouteLabel.Resolve(context);

        try
        {
            await next(context);
        }
        catch
        {
            metrics.Errors.Add(
                1,
                new KeyValuePair<string, object?>("scope", "http_api"),
                new KeyValuePair<string, object?>("scope_type", route),
                new KeyValuePair<string, object?>("reason", "exception"));
            throw;
        }
        finally
        {
            sw.Stop();

            metrics.RequestDuration.Record(
                sw.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>("scope", "http_api"),
                new KeyValuePair<string, object?>("scope_type", route));

            if (context.Response.StatusCode >= 500)
            {
                metrics.Errors.Add(
                    1,
                    new KeyValuePair<string, object?>("scope", "http_api"),
                    new KeyValuePair<string, object?>("scope_type", route),
                    new KeyValuePair<string, object?>("reason", "5xx"));
            }
        }
    }
}
