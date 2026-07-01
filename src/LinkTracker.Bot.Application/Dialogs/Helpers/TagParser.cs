using System.Text.RegularExpressions;

namespace LinkTracker.Bot.Application.Dialogs.Helpers;

public static class TagParser
{
    private static readonly Regex Valid = new(@"^[\p{L}\p{N}_-]{1,24}$",
        RegexOptions.Compiled);

    public static (List<string> tags, List<string> invalid) ParseCommaSeparated(
        string input,
        int maxTags = 10)
    {
        var tags = new List<string>();
        var invalid = new List<string>();

        foreach (var raw in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var t = raw.Trim();
            t = t.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(t))
            {
                continue;
            }

            if (!Valid.IsMatch(t))
            {
                invalid.Add(t);
                continue;
            }

            if (!tags.Contains(t))
            {
                tags.Add(t);
            }

            if (tags.Count >= maxTags)
            {
                break;
            }
        }

        return (tags, invalid);
    }
}