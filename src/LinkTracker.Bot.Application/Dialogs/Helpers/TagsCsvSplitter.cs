namespace LinkTracker.Bot.Application.Dialogs.Helpers;

public static class TagsCsvSplitter
{
    public static string[] SplitCommaSeparated(string? tagsCsv)
    {
        return string.IsNullOrWhiteSpace(tagsCsv)
            ? []
            : tagsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();
    }
}