namespace LinkTracker.Bot.Application.Dialogs.Abstractions;

public sealed class DialogContext
{
    public long ChatId { get; init; }

    public string? ActiveDialogId { get; set; }
    public string? ActiveNodeId { get; set; }

    public Dictionary<string, string> Data { get; } = new();

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool HasActiveDialog =>
        !string.IsNullOrWhiteSpace(ActiveDialogId) && !string.IsNullOrWhiteSpace(ActiveNodeId);

    public void Reset()
    {
        ActiveDialogId = null;
        ActiveNodeId = null;
        Data.Clear();
    }

    public string? Get(string key)
    {
        return Data.TryGetValue(key, out var v) ? v : null;
    }

    public void Set(string key, string value)
    {
        Data[key] = value;
    }

    public void Remove(string key)
    {
        Data.Remove(key);
    }
}