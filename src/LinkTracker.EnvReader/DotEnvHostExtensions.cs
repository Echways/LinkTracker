using Microsoft.Extensions.Hosting;

namespace LinkTracker.EnvReader;

public static class DotEnvHostExtensions
{
    public static IHostApplicationBuilder AddLocalDotEnv(this IHostApplicationBuilder builder)
    {
        builder.Configuration.AddDotEnv(
            Path.Combine(builder.Environment.ContentRootPath, ".env"),
            true);

        return builder;
    }
}