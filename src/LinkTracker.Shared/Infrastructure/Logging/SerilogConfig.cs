using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace LinkTracker.Shared.Infrastructure.Logging;

public static class SerilogConfig
{
    public static LoggerConfiguration Configure(LoggerConfiguration configuration, string serviceName)
    {
        return configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", serviceName)
            .WriteTo.Console(new CompactJsonFormatter());
    }
}
