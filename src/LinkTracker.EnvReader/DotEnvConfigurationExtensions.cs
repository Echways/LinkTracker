using Microsoft.Extensions.Configuration;

namespace LinkTracker.EnvReader;

public static class DotEnvConfigurationExtensions
{
    public static IConfigurationBuilder AddDotEnv(
        this IConfigurationBuilder builder,
        string path,
        bool optional = true)
    {
        var pairs = DotEnv.Load(path, optional);
        if (pairs.Count == 0)
        {
            return builder;
        }

        return builder.AddInMemoryCollection(pairs);
    }
}