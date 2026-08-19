using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Memory;

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

        var source = new MemoryConfigurationSource { InitialData = pairs };
        var environmentVariablesIndex = IndexOfEnvironmentVariables(builder.Sources);

        if (environmentVariablesIndex < 0)
        {
            builder.Sources.Add(source);
        }
        else
        {
            builder.Sources.Insert(environmentVariablesIndex, source);
        }

        return builder;
    }

    private static int IndexOfEnvironmentVariables(IList<IConfigurationSource> sources)
    {
        for (var index = 0; index < sources.Count; index++)
        {
            if (sources[index] is EnvironmentVariablesConfigurationSource)
            {
                return index;
            }
        }

        return -1;
    }
}
