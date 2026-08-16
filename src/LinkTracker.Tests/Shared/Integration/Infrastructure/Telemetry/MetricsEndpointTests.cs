using LinkTracker.Bot.Infrastructure.Telemetry;
using LinkTracker.Scrapper.Infrastructure.Telemetry;
using LinkTracker.Shared.Infrastructure.RateLimiting;
using LinkTracker.Shared.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkTracker.Tests.Shared.Integration.Infrastructure.Telemetry;

[Trait("Module", "Shared")]
[Trait("Category", "Integration")]
public sealed class MetricsEndpointTests
{
    [Fact]
    public async Task GetMetrics_ExposesScrapperInstrumentNamesUsedByDashboard()
    {
        using var metrics = new ScrapperMetrics();

        using var server = CreateServer(ScrapperMetrics.MeterName);
        using var client = server.CreateClient();

        RecordScrapperSamples(metrics);

        var families = await GetMetricFamilyNamesAsync(client);

        Assert.Contains("api_requests_total", families);
        Assert.Contains("sent_updates_total", families);
        Assert.Contains("errors_total", families);
        Assert.Contains("db_queries_total", families);
        Assert.Contains("db_errors_total", families);
        Assert.Contains("kafka_produced_total", families);
        Assert.Contains("kafka_produce_errors_total", families);
        Assert.Contains("request_duration_ms_total", families);
        Assert.Contains("db_query_duration_ms_total", families);
        Assert.Contains("kafka_produce_duration_ms_total", families);
        Assert.Contains("links_on_track_total", families);
        Assert.Contains("process_memory_working_set_bytes", families);
        Assert.Contains("process_memory_managed_bytes", families);
    }

    [Fact]
    public async Task GetMetrics_ExposesBotInstrumentNamesUsedByDashboard()
    {
        using var metrics = new BotMetrics();

        using var server = CreateServer(BotMetrics.MeterName);
        using var client = server.CreateClient();

        RecordBotSamples(metrics);

        var families = await GetMetricFamilyNamesAsync(client);

        Assert.Contains("bot_requests_total", families);
        Assert.Contains("command_requests_total", families);
        Assert.Contains("sent_notification_total", families);
        Assert.Contains("errors_total", families);
        Assert.Contains("kafka_consumed_total", families);
        Assert.Contains("kafka_consume_errors_total", families);
        Assert.Contains("command_duration_ms_total", families);
        Assert.Contains("scrapper_call_duration_ms_total", families);
        Assert.Contains("kafka_consume_duration_ms_total", families);
        Assert.Contains("process_memory_working_set_bytes", families);
    }

    [Fact]
    public async Task GetMetrics_WhenRateLimiterIsEnabled_IsNotThrottled()
    {
        using var metrics = new ScrapperMetrics();

        using var server = CreateServer(ScrapperMetrics.MeterName);
        using var client = server.CreateClient();

        RecordScrapperSamples(metrics);

        using var first = await client.GetAsync("/metrics");
        using var second = await client.GetAsync("/metrics");

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
    }

    private static void RecordScrapperSamples(ScrapperMetrics metrics)
    {
        metrics.ApiRequests.Add(1, new KeyValuePair<string, object?>("source", "/links"));
        metrics.SentUpdates.Add(1);
        metrics.Errors.Add(
            1,
            new KeyValuePair<string, object?>("scope", "http_api"),
            new KeyValuePair<string, object?>("scope_type", "/links"),
            new KeyValuePair<string, object?>("reason", "5xx"));
        metrics.DbQueries.Add(1, new KeyValuePair<string, object?>("operation", "add"));
        metrics.DbErrors.Add(1, new KeyValuePair<string, object?>("operation", "add"));
        metrics.KafkaProduced.Add(1, new KeyValuePair<string, object?>("topic", "link.raw-updates"));
        metrics.KafkaProduceErrors.Add(1, new KeyValuePair<string, object?>("topic", "link.raw-updates"));

        metrics.RequestDuration.Record(
            1,
            new KeyValuePair<string, object?>("scope", "http_api"),
            new KeyValuePair<string, object?>("scope_type", "/links"));
        metrics.DbQueryDuration.Record(1, new KeyValuePair<string, object?>("operation", "add"));
        metrics.KafkaProduceDuration.Record(1, new KeyValuePair<string, object?>("topic", "link.raw-updates"));

        metrics.SetLinksOnTrack("github.com", 1);
    }

    private static void RecordBotSamples(BotMetrics metrics)
    {
        metrics.IncrementRequest("Command");
        metrics.IncrementCommand("start");
        metrics.IncrementSentNotifications();
        metrics.IncrementError("bot_command", "start", "exception");
        metrics.IncrementKafkaConsumed("link.processed-updates");
        metrics.IncrementKafkaConsumeError("link.processed-updates");

        metrics.ObserveCommandDuration("bot_command", "start", 1);
        metrics.ObserveScrapperCallDuration("scrapper_sync_api", "GetLinksAsync", 1);
        metrics.ObserveKafkaConsumeDuration("link.processed-updates", 1);
    }

    private static async Task<HashSet<string>> GetMetricFamilyNamesAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/metrics");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync();

        return payload
            .Split('\n')
            .Where(line => line.StartsWith("# TYPE ", StringComparison.Ordinal))
            .Select(line => line.Split(' ')[2])
            .ToHashSet(StringComparer.Ordinal);
    }

    private static TestServer CreateServer(string meterName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "1", ["RateLimiting:WindowSeconds"] = "60", ["RateLimiting:SegmentsPerWindow"] = "1", ["RateLimiting:QueueLimit"] = "0" })
            .Build();

        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddIpRateLimiting(configuration);
                services.AddOpenTelemetryMetrics("test", meterName);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseRateLimiter();
                app.UseEndpoints(endpoints => endpoints.MapMetricsEndpoint());
            }));
    }
}