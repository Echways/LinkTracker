using System.Net;
using Microsoft.AspNetCore.Http;

namespace LinkTracker.Shared.Infrastructure.RateLimiting;

internal sealed class RateLimitPartitionKeyResolver
{
    internal const string TrustedPartitionKey = "trusted";

    private const string UnknownAddress = "unknown";

    private readonly string _partitionHeaderName;
    private readonly IPNetwork[] _trustedNetworks;

    public RateLimitPartitionKeyResolver(ApiRateLimitingOptions options)
    {
        _partitionHeaderName = options.PartitionHeaderName;
        _trustedNetworks = [.. options.TrustedNetworks.Select(Parse)];
    }

    public bool IsTrusted(HttpContext context)
    {
        var address = Normalize(context.Connection.RemoteIpAddress);

        return address is not null && Array.Exists(_trustedNetworks, network => network.Contains(address));
    }

    public string Resolve(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(_partitionHeaderName, out var values)
            && !string.IsNullOrWhiteSpace(values.ToString()))
        {
            return $"caller:{values.ToString().Trim()}";
        }

        return $"ip:{Normalize(context.Connection.RemoteIpAddress)?.ToString() ?? UnknownAddress}";
    }

    private static IPAddress? Normalize(IPAddress? address)
    {
        return address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;
    }

    private static IPNetwork Parse(string network)
    {
        return IPNetwork.TryParse(network, out var parsed)
            ? parsed
            : throw new InvalidOperationException(
                $"{ApiRateLimitingOptions.SectionName}:TrustedNetworks contains a value that is not CIDR notation: '{network}'.");
    }
}
