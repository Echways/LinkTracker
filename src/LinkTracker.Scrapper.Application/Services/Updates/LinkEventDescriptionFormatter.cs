using LinkTracker.Scrapper.Application.Models.Updates;

namespace LinkTracker.Scrapper.Application.Services.Updates;

internal static class LinkEventDescriptionFormatter
{
    public static string Format(LinkEvent linkEvent)
    {
        var lines = new List<string> { $"Источник: {FormatSourceKind(linkEvent.SourceKind)}", $"Тип: {FormatEventKind(linkEvent.EventKind)}", $"Заголовок: {linkEvent.Title}", $"Создано: {linkEvent.CreatedAt:O}" };

        if (!string.IsNullOrWhiteSpace(linkEvent.UserName))
        {
            lines.Add($"Пользователь: {linkEvent.UserName}");
        }

        if (!string.IsNullOrWhiteSpace(linkEvent.Body))
        {
            lines.Add($"Фрагмент: {Trim(linkEvent.Body, 200)}");
        }

        if (linkEvent.ResourceUrl is not null)
        {
            lines.Add($"Ссылка: {linkEvent.ResourceUrl}");
        }

        return string.Join('\n', lines);
    }

    private static string Trim(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }

    private static string FormatEventKind(LinkEventKind eventKind)
    {
        return eventKind switch
        {
            LinkEventKind.Issue => "issue",
            LinkEventKind.PullRequest => "pull-request",
            LinkEventKind.QuestionActivity => "question-activity",
            LinkEventKind.Answer => "answer",
            LinkEventKind.Comment => "comment",
            LinkEventKind.Post => "post",
            _ => eventKind.ToString()
        };
    }

    private static string FormatSourceKind(LinkSourceKind sourceKind)
    {
        return sourceKind switch
        {
            LinkSourceKind.GitHub => "GitHub",
            LinkSourceKind.StackOverflow => "Stack Overflow",
            LinkSourceKind.Reddit => "Reddit",
            _ => sourceKind.ToString()
        };
    }
}