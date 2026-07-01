using System.Net;
using LinkTracker.Shared.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace LinkTracker.Tests.Shared.Unit.Infrastructure.Resilience;

[Trait("Module", "Shared")]
[Trait("Category", "Unit")]
public sealed class HttpClientBuilderResilienceExtensionsTests
{
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task SendAsync_WhenResponseStatusIsRetryable_RetriesRequest(HttpStatusCode statusCode)
    {
        var handler = new TestHttpMessageHandler();
        handler.Enqueue(statusCode);
        handler.Enqueue(HttpStatusCode.OK);

        await using var provider = CreateProvider(handler, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.BackoffMilliseconds = 1;
            options.Retry.RetryableStatusCodes = [(int)statusCode];
        });

        var client = CreateClient(provider);

        using var response = await client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SendAsync_WhenHttpRequestExceptionOccurs_RetriesRequest()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("transient failure"));
        handler.Enqueue(HttpStatusCode.OK);

        await using var provider = CreateProvider(handler, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.BackoffMilliseconds = 1;
        });

        var client = CreateClient(provider);

        using var response = await client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SendAsync_WhenTimeoutIsRetryable_RetriesRequest()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueNeverCompletingResponse();
        handler.Enqueue(HttpStatusCode.OK);

        await using var provider = CreateProvider(handler, options =>
        {
            options.TimeoutMilliseconds = 10;
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.BackoffMilliseconds = 1;
        });

        var client = CreateClient(provider);

        using var response = await client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task SendAsync_WhenResponseStatusIsNotRetryable_DoesNotRetryRequest(HttpStatusCode statusCode)
    {
        var handler = new TestHttpMessageHandler();
        handler.Enqueue(statusCode);
        handler.Enqueue(HttpStatusCode.OK);

        await using var provider = CreateProvider(handler, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.BackoffMilliseconds = 1;
            options.Retry.RetryableStatusCodes = [(int)HttpStatusCode.InternalServerError];
        });

        var client = CreateClient(provider);

        using var response = await client.GetAsync("/test");

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task SendAsync_WhenRetryIsDisabled_DoesNotRetryRetryableResponse()
    {
        var handler = new TestHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.InternalServerError);
        handler.Enqueue(HttpStatusCode.OK);

        await using var provider = CreateProvider(handler, options =>
        {
            options.Retry.MaxRetryAttempts = 0;
            options.Retry.RetryableStatusCodes = [(int)HttpStatusCode.InternalServerError];
        });

        var client = CreateClient(provider);

        using var response = await client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData(HttpRetryBackoffStrategy.Constant)]
    [InlineData(HttpRetryBackoffStrategy.Exponential)]
    public async Task SendAsync_WhenBackoffStrategyConfigured_RetriesRequest(HttpRetryBackoffStrategy strategy)
    {
        var handler = new TestHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.ServiceUnavailable);
        handler.Enqueue(HttpStatusCode.OK);

        await using var provider = CreateProvider(handler, options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.BackoffMilliseconds = 1;
            options.Retry.BackoffStrategy = strategy;
            options.Retry.RetryableStatusCodes = [(int)HttpStatusCode.ServiceUnavailable];
        });

        var client = CreateClient(provider);

        using var response = await client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SendAsync_WhenResponseTakesLongerThanTimeout_ThrowsTimeoutRejectedException()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueNeverCompletingResponse();

        await using var provider = CreateProvider(handler, options =>
        {
            options.TimeoutMilliseconds = 10;
            options.Retry.MaxRetryAttempts = 0;
        });

        var client = CreateClient(provider);

        await Assert.ThrowsAsync<TimeoutRejectedException>(() =>
            client.GetAsync("/test"));

        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task SendAsync_WhenFailureThresholdReached_OpensCircuitBreaker()
    {
        var handler = new TestHttpMessageHandler();
        handler.Enqueue(HttpStatusCode.InternalServerError);
        handler.Enqueue(HttpStatusCode.InternalServerError);
        handler.Enqueue(HttpStatusCode.OK);

        await using var provider = CreateProvider(handler, ConfigureFastCircuitBreaker);
        var client = CreateClient(provider);

        using var firstResponse = await client.GetAsync("/test");
        using var secondResponse = await client.GetAsync("/test");

        Assert.Equal(HttpStatusCode.InternalServerError, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, secondResponse.StatusCode);

        await Assert.ThrowsAnyAsync<BrokenCircuitException>(() =>
            client.GetAsync("/test"));

        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task SendAsync_WhenHttpRequestExceptionsReachFailureThreshold_OpensCircuitBreaker()
    {
        var handler = new TestHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("first transient failure"));
        handler.EnqueueException(new HttpRequestException("second transient failure"));
        handler.Enqueue(HttpStatusCode.OK);

        await using var provider = CreateProvider(handler, ConfigureFastCircuitBreaker);
        var client = CreateClient(provider);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("/test"));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.GetAsync("/test"));

        await Assert.ThrowsAnyAsync<BrokenCircuitException>(() =>
            client.GetAsync("/test"));

        Assert.Equal(2, handler.RequestCount);
    }

    private static ServiceProvider CreateProvider(
        HttpMessageHandler handler,
        Action<HttpResilienceOptions>? configureOptions = null)
    {
        var options = CreateDefaultOptions();
        configureOptions?.Invoke(options);

        var services = new ServiceCollection();

        services
            .AddHttpClient("test", client =>
            {
                client.BaseAddress = new Uri("https://example.com");
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddConfiguredHttpResilience($"test-{Guid.NewGuid():N}", options);

        return services.BuildServiceProvider();
    }

    private static HttpClient CreateClient(IServiceProvider provider)
    {
        return provider
            .GetRequiredService<IHttpClientFactory>()
            .CreateClient("test");
    }

    private static HttpResilienceOptions CreateDefaultOptions()
    {
        return new HttpResilienceOptions
        {
            TimeoutMilliseconds = 1000,
            Retry = new HttpRetryOptions { MaxRetryAttempts = 0, BackoffMilliseconds = 1, BackoffStrategy = HttpRetryBackoffStrategy.Constant, RetryableStatusCodes = [(int)HttpStatusCode.InternalServerError] },
            CircuitBreaker = new HttpCircuitBreakerOptions { FailureRateThreshold = 100, SamplingDurationSeconds = 10, MinimumThroughput = 100, WaitDurationInOpenStateMilliseconds = 1000 }
        };
    }

    private static void ConfigureFastCircuitBreaker(HttpResilienceOptions options)
    {
        options.Retry.MaxRetryAttempts = 0;
        options.Retry.RetryableStatusCodes = [(int)HttpStatusCode.InternalServerError];

        options.CircuitBreaker.FailureRateThreshold = 100;
        options.CircuitBreaker.MinimumThroughput = 2;
        options.CircuitBreaker.SamplingDurationSeconds = 10;
        options.CircuitBreaker.WaitDurationInOpenStateMilliseconds = 1000;
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new();
        private int _requestCount;

        public int RequestCount => _requestCount;

        public void Enqueue(HttpStatusCode statusCode)
        {
            _responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(statusCode)));
        }

        public void EnqueueException(Exception exception)
        {
            _responses.Enqueue(_ => Task.FromException<HttpResponseMessage>(exception));
        }

        public void EnqueueNeverCompletingResponse()
        {
            _responses.Enqueue(async ct =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);

            Func<CancellationToken, Task<HttpResponseMessage>> responseFactory = _responses.Count == 0
                ? _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))
                : _responses.Dequeue();

            return CreateResponseAsync(responseFactory, request, cancellationToken);
        }

        private static async Task<HttpResponseMessage> CreateResponseAsync(
            Func<CancellationToken, Task<HttpResponseMessage>> responseFactory,
            HttpRequestMessage request,
            CancellationToken ct)
        {
            var response = await responseFactory(ct);
            response.RequestMessage = request;

            return response;
        }
    }
}