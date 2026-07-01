namespace LinkTracker.Shared.Infrastructure.Telemetry;

public sealed class PushgatewayOptions
{
    public const string SectionName = "Telemetry:Pushgateway";

    public bool Enabled { get; set; } = true;

    public string Endpoint { get; set; } = "http://pushgateway:9091/metrics";

    public string Job { get; set; } = "app";

    public int IntervalMilliseconds { get; set; } = 5000;
}