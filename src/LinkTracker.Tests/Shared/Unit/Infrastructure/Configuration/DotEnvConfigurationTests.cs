using LinkTracker.EnvReader;
using Microsoft.Extensions.Configuration;

namespace LinkTracker.Tests.Shared.Unit.Infrastructure.Configuration;

[Trait("Module", "Shared")]
[Trait("Category", "Unit")]
public sealed class DotEnvConfigurationTests : IDisposable
{
    private const string Key = "Scrapper:BaseUrl";
    private const string EnvironmentVariableName = "Scrapper__BaseUrl";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.env");

    [Fact]
    public void Build_WhenKeyIsAlsoSetAsEnvironmentVariable_PrefersEnvironmentVariable()
    {
        File.WriteAllText(_path, $"{EnvironmentVariableName}=http://from-dot-env");
        Environment.SetEnvironmentVariable(EnvironmentVariableName, "http://from-environment");

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddDotEnv(_path)
            .Build();

        Assert.Equal("http://from-environment", configuration[Key]);
    }

    [Fact]
    public void Build_WhenKeyIsOnlySetInDotEnv_UsesDotEnvValue()
    {
        File.WriteAllText(_path, $"{EnvironmentVariableName}=http://from-dot-env");

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddDotEnv(_path)
            .Build();

        Assert.Equal("http://from-dot-env", configuration[Key]);
    }

    [Fact]
    public void Build_WhenDotEnvIsAddedWithoutEnvironmentVariables_UsesDotEnvValue()
    {
        File.WriteAllText(_path, $"{EnvironmentVariableName}=http://from-dot-env");

        var configuration = new ConfigurationBuilder()
            .AddDotEnv(_path)
            .Build();

        Assert.Equal("http://from-dot-env", configuration[Key]);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvironmentVariableName, null);
        File.Delete(_path);
    }
}
