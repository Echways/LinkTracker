namespace LinkTracker.Shared.Links;

public static class TrackedLinkUrl
{
    public static bool TryParse(string raw, out Uri uri)
    {
        uri = default!;

        if (!Uri.TryCreate(raw?.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    public static string Normalize(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Normalize(uri);
        }

        var trimmed = url.Trim();
        return trimmed.EndsWith('/') ? trimmed.TrimEnd('/') : trimmed;
    }

    public static string Normalize(Uri uri)
    {
        var builder = new UriBuilder(uri) { Fragment = string.Empty };

        if ((builder.Scheme == Uri.UriSchemeHttp && builder.Port == 80) ||
            (builder.Scheme == Uri.UriSchemeHttps && builder.Port == 443))
        {
            builder.Port = -1;
        }

        var normalized = builder.Uri.AbsoluteUri.Trim();

        if (normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    public static bool Equals(string left, string right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool Equals(string left, Uri right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }
}