namespace LinkTracker.AiAgent.Application.Abstractions;

public interface ILinkUpdateSummarizer
{
    Task<string> SummarizeAsync(string text, CancellationToken ct);
}