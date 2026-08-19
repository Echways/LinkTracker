using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace LinkTracker.Shared.Infrastructure.Logging;

public static class SerilogHostExtensions
{
    public static IHostApplicationBuilder AddSharedSerilog(
        this IHostApplicationBuilder builder,
        string serviceName)
    {
        builder.Services.AddSerilog(configuration => SerilogConfig.Configure(configuration, serviceName));

        return builder;
    }
}
