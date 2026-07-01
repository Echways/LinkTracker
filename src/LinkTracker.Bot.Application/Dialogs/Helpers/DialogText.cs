using LinkTracker.Bot.Application.Dialogs.Abstractions;

namespace LinkTracker.Bot.Application.Dialogs.Helpers;

public static class DialogText
{
    public static bool TryGetText(BotRequest request, out string text)
    {
        text = string.Empty;

        if (request.Type != BotRequestType.Text || string.IsNullOrWhiteSpace(request.Text))
        {
            return false;
        }

        text = request.Text.Trim();
        return true;
    }

    public static bool IsYes(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        return t is "да" or "yes" or "y";
    }

    public static bool IsNo(string text)
    {
        var t = text.Trim().ToLowerInvariant();
        return t is "нет" or "no" or "n";
    }
}