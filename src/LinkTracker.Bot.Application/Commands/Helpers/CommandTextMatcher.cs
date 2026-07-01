namespace LinkTracker.Bot.Application.Commands.Helpers;

public static class CommandTextMatcher
{
    public static bool Matches(string text, string commandName)
    {
        var normalized = text.TrimStart();
        var command = "/" + commandName;

        return normalized.Equals(command, StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(command + " ", StringComparison.OrdinalIgnoreCase);
    }
}