using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LinkTracker.Shared.Infrastructure.Authentication;

public sealed class ServiceTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string CallerName = "internal-service";

    private readonly byte[] _secret;

    public ServiceTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IOptions<ServiceAuthOptions> serviceAuthOptions)
        : base(options, loggerFactory, encoder)
    {
        _secret = Encoding.UTF8.GetBytes(serviceAuthOptions.Value.Secret);
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ServiceAuthDefaults.HeaderName, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!IsKnownSecret(values.ToString()))
        {
            return Task.FromResult(AuthenticateResult.Fail("Передан неизвестный сервисный токен."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, CallerName)],
            ServiceAuthDefaults.AuthenticationScheme);

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            ServiceAuthDefaults.AuthenticationScheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private bool IsKnownSecret(string token)
    {
        return _secret.Length > 0
               && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), _secret);
    }
}
