namespace LinkTracker.Scrapper.Application.Services.Helpers;

internal static class UriParsingHelper
{
    public static bool IsHost(Uri url, string expectedHost)
    {
        return string.Equals(url.Host, expectedHost, StringComparison.OrdinalIgnoreCase)
               || string.Equals(url.Host, $"www.{expectedHost}", StringComparison.OrdinalIgnoreCase);
    }

    public static string[] GetPathSegments(Uri url)
    {
        return url.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}