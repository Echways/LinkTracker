namespace LinkTracker.Bot.Application.Commands.Helpers;

public static class ArgsExtractor
{
    public static string ExtractArgs(string text)
    {
        var trimmed = text.Trim();

        var firstSpaceIndex = trimmed.IndexOf(' ');
        return firstSpaceIndex < 0 ? string.Empty : trimmed[(firstSpaceIndex + 1)..].Trim();
    }
}