using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace LinkTracker.Tests.Scrapper.Integration.Http;

public sealed class WireMockContainerFixture : IAsyncLifetime
{
    private const int ContainerPort = 8080;
    private IContainer _container = default!;

    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _container = new ContainerBuilder("wiremock/wiremock:3.9.1")
            .WithPortBinding(ContainerPort, true)
            .WithWaitStrategy(
                Wait.ForUnixContainer()
                    .UntilInternalTcpPortIsAvailable(ContainerPort))
            .Build();

        await _container.StartAsync();

        var port = _container.GetMappedPublicPort(ContainerPort);
        BaseUrl = $"http://{_container.Hostname}:{port}";
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public async Task ResetAsync()
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(BaseUrl);
        using var response = await client.PostAsync("/__admin/reset", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task StubAsync(object mapping)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(BaseUrl);
        using var response = await client.PostAsJsonAsync("/__admin/mappings", mapping);
        response.EnsureSuccessStatusCode();
    }
}