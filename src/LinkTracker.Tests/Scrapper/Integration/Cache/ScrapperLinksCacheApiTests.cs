using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LinkTracker.Scrapper.Application.Abstractions.Cache;
using LinkTracker.Scrapper.Application.Abstractions.Tracking;
using LinkTracker.Scrapper.Contracts.Requests;
using LinkTracker.Scrapper.Contracts.Responses;
using LinkTracker.Scrapper.Infrastructure.Cache.Helpers;
using LinkTracker.Scrapper.Infrastructure.Cache.Implementation;
using LinkTracker.Scrapper.Infrastructure.Configuration.Valkey;
using LinkTracker.Scrapper.Presentation.Endpoints;
using LinkTracker.Scrapper.Storage.Abstractions.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LinkTracker.Tests.Scrapper.Integration.Cache;

[Trait("Module", "Scrapper")]
[Trait("Category", "Integration")]
[Collection("Valkey collection")]
public sealed class ScrapperLinksCacheApiTests(ValkeyTestContainerFixture fixture)
{
    private const string InstanceName = "linktracker-api-tests";

    [Fact]
    public async Task GetLinks_WhenCacheMiss_CallsServiceAndStoresResponseInValkey()
    {
        await fixture.ResetAsync();

        const long chatId = 10_001;
        var service = Substitute.For<ILinkTrackingService>();

        service.GetLinksAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TrackedLinkRecord>>(
            [
                CreateRecord(42, "https://github.com/user/repo", ["backend", "dotnet"])
            ]));

        await using var app = await CreateAppAsync(service, CreateLinksCache());
        var client = app.GetTestClient();

        var response = await GetLinksAsync(client, chatId);

        Assert.Equal(1, response.Size);

        var link = Assert.Single(response.Links);

        Assert.Equal(42, link.Id);
        Assert.Equal(new Uri("https://github.com/user/repo"), link.Url);
        Assert.Equal(["backend", "dotnet"], link.Tags);

        var rawCacheValue = await fixture.GetStringAsync(BuildLinksCacheKey(chatId));

        Assert.False(string.IsNullOrWhiteSpace(rawCacheValue));

        using var json = JsonDocument.Parse(rawCacheValue!);

        Assert.True(json.RootElement.TryGetProperty("links", out var links));
        Assert.True(json.RootElement.TryGetProperty("size", out var size));
        Assert.Equal(1, size.GetInt32());
        Assert.Equal(42, links[0].GetProperty("id").GetInt64());
        Assert.Equal("https://github.com/user/repo", links[0].GetProperty("url").GetString());

        await service.Received(1).GetLinksAsync(chatId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLinks_WhenCacheHit_DoesNotCallServiceAgain()
    {
        await fixture.ResetAsync();

        const long chatId = 10_002;
        var service = Substitute.For<ILinkTrackingService>();

        service.GetLinksAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TrackedLinkRecord>>(
            [
                CreateRecord(100, "https://github.com/user/cached-repo", ["cached"])
            ]));

        await using var app = await CreateAppAsync(service, CreateLinksCache());
        var client = app.GetTestClient();

        var first = await GetLinksAsync(client, chatId);
        var second = await GetLinksAsync(client, chatId);

        Assert.Equal(1, first.Size);
        Assert.Equal(1, second.Size);
        Assert.Equal(100, second.Links.Single().Id);

        await service.Received(1).GetLinksAsync(chatId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetLinks_WhenTtlExpired_CallsServiceAgainAndRebuildsResponse()
    {
        await fixture.ResetAsync();

        const long chatId = 10_003;
        var callNumber = 0;
        var service = Substitute.For<ILinkTrackingService>();

        service.GetLinksAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callNumber++;

                return Task.FromResult<IReadOnlyList<TrackedLinkRecord>>(
                [
                    CreateRecord(
                        callNumber,
                        $"https://github.com/user/repo-{callNumber}",
                        ["ttl"])
                ]);
            });

        await using var app = await CreateAppAsync(service, CreateLinksCache(1));
        var client = app.GetTestClient();

        var first = await GetLinksAsync(client, chatId);

        Assert.Equal(1, first.Links.Single().Id);

        await WaitUntilAsync(async () =>
            await fixture.GetStringAsync(BuildLinksCacheKey(chatId)) is null);

        var second = await GetLinksAsync(client, chatId);

        Assert.Equal(2, second.Links.Single().Id);

        await service.Received(2).GetLinksAsync(chatId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PostLinks_WhenUserLinksChanged_InvalidatesCachedList()
    {
        await fixture.ResetAsync();

        const long chatId = 10_004;
        var addedUrl = new Uri("https://github.com/user/new-repo");

        IReadOnlyList<TrackedLinkRecord> currentLinks =
        [
            CreateRecord(1, "https://github.com/user/existing-repo", ["old"])
        ];

        var service = Substitute.For<ILinkTrackingService>();

        service.GetLinksAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(currentLinks));

        service.AddLinkAsync(
                chatId,
                Arg.Any<Uri>(),
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var link = call.ArgAt<Uri>(1);
                var tags = call.ArgAt<IReadOnlyList<string>>(2);
                var record = CreateRecord(2, link.ToString(), tags);
                currentLinks = [.. currentLinks, record];

                return Task.FromResult(record);
            });

        await using var app = await CreateAppAsync(service, CreateLinksCache());
        var client = app.GetTestClient();

        var beforeMutation = await GetLinksAsync(client, chatId);

        Assert.Equal(1, beforeMutation.Size);
        Assert.NotNull(await fixture.GetStringAsync(BuildLinksCacheKey(chatId)));

        using var postRequest = new HttpRequestMessage(HttpMethod.Post, "/links");
        postRequest.Headers.Add("Tg-Chat-Id", chatId.ToString());
        postRequest.Content = JsonContent.Create(new AddLinkRequest { Link = addedUrl, Tags = ["new"] });

        var postResponse = await client.SendAsync(postRequest);

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        Assert.Null(await fixture.GetStringAsync(BuildLinksCacheKey(chatId)));

        var afterMutation = await GetLinksAsync(client, chatId);

        Assert.Equal(2, afterMutation.Size);
        Assert.Contains(afterMutation.Links, x => x.Url == addedUrl);

        await service.Received(2).GetLinksAsync(chatId, Arg.Any<CancellationToken>());
        await service.Received(1).AddLinkAsync(
            chatId,
            addedUrl,
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteLinks_WhenUserLinksChanged_InvalidatesCachedList()
    {
        await fixture.ResetAsync();

        const long chatId = 10_005;
        var removedUrl = new Uri("https://github.com/user/remove-me");

        IReadOnlyList<TrackedLinkRecord> currentLinks =
        [
            CreateRecord(1, removedUrl.ToString(), ["delete"])
        ];

        var service = Substitute.For<ILinkTrackingService>();

        service.GetLinksAsync(chatId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(currentLinks));

        service.RemoveLinkAsync(chatId, removedUrl, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var removed = currentLinks.Single();
                currentLinks = [];

                return Task.FromResult(removed);
            });

        await using var app = await CreateAppAsync(service, CreateLinksCache());
        var client = app.GetTestClient();

        var beforeMutation = await GetLinksAsync(client, chatId);

        Assert.Equal(1, beforeMutation.Size);
        Assert.NotNull(await fixture.GetStringAsync(BuildLinksCacheKey(chatId)));

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/links");
        deleteRequest.Headers.Add("Tg-Chat-Id", chatId.ToString());
        deleteRequest.Content = JsonContent.Create(new RemoveLinkRequest { Link = removedUrl });

        var deleteResponse = await client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        Assert.Null(await fixture.GetStringAsync(BuildLinksCacheKey(chatId)));

        var afterMutation = await GetLinksAsync(client, chatId);

        Assert.Equal(0, afterMutation.Size);
        Assert.Empty(afterMutation.Links);

        await service.Received(2).GetLinksAsync(chatId, Arg.Any<CancellationToken>());
        await service.Received(1).RemoveLinkAsync(chatId, removedUrl, Arg.Any<CancellationToken>());
    }

    private static async Task<WebApplication> CreateAppAsync(
        ILinkTrackingService service,
        ILinksResponseCache cache)
    {
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.UseTestServer();

        builder.Services.AddRouting();
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(cache);

        var app = builder.Build();

        app.MapScrapperEndpoints();

        await app.StartAsync();

        return app;
    }

    private ValkeyLinksResponseCache CreateLinksCache(int linksTtlSeconds = 60)
    {
        var keyBuilder = new LinksResponseCacheKeyBuilder(Options.Create(new ValkeyOptions { InstanceName = InstanceName }));

        return new ValkeyLinksResponseCache(
            CreateKeyValueCache(),
            keyBuilder,
            Options.Create(new ValkeyOptions { InstanceName = InstanceName, LinksTtlSeconds = linksTtlSeconds }),
            NullLogger<ValkeyLinksResponseCache>.Instance);
    }

    private ValkeyKeyValueCache CreateKeyValueCache()
    {
        var provider = new ValkeyConnectionProvider(
            Options.Create(new ValkeyOptions { ConnectionString = fixture.ConnectionString }),
            NullLogger<ValkeyConnectionProvider>.Instance);

        return new ValkeyKeyValueCache(
            provider,
            NullLogger<ValkeyKeyValueCache>.Instance);
    }

    private static async Task<ListLinksResponse> GetLinksAsync(HttpClient client, long chatId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/links");
        request.Headers.Add("Tg-Chat-Id", chatId.ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListLinksResponse>();

        return body ?? throw new InvalidOperationException("Empty response body.");
    }

    private static TrackedLinkRecord CreateRecord(
        long id,
        string url,
        IReadOnlyList<string>? tags = null)
    {
        return new TrackedLinkRecord { Id = id, Url = new Uri(url), Tags = tags ?? [] };
    }

    private static string BuildLinksCacheKey(long chatId)
    {
        return $"{InstanceName}:{{linktracker-links}}:links:chat:{chatId}";
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        const int maxAttempts = 30;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.True(await condition(), "Condition was not satisfied in time.");
    }
}