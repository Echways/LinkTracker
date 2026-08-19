using Microsoft.Extensions.Options;

namespace LinkTracker.Shared.Infrastructure.Authentication;

public sealed class ServiceAuthHeaderHandler(IOptions<ServiceAuthOptions> options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        request.Headers.Remove(ServiceAuthDefaults.HeaderName);
        request.Headers.Add(ServiceAuthDefaults.HeaderName, options.Value.Secret);

        return base.SendAsync(request, cancellationToken);
    }
}
