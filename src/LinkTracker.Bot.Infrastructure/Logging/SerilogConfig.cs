using Serilog;
using Serilog.Formatting.Compact;

namespace LinkTracker.Bot.Infrastructure.Logging;

public static class SerilogConfig
{
    public static LoggerConfiguration Configure(LoggerConfiguration lc)
    {
        return lc.Enrich.FromLogContext()
            .WriteTo.Console(new CompactJsonFormatter());
    }
}