namespace LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;

public sealed class AiAgentOptions
{
    public FilteringOptions? Filtering { get; set; }
    public SummarizationOptions? Summarization { get; set; }
    public PrioritizationOptions? Prioritization { get; set; }
    public GroupingOptions Grouping { get; set; } = new();
}

public sealed class FilteringOptions
{
    public IReadOnlyList<string> StopWords { get; set; } = [];
    public IReadOnlyList<string> ExcludedAuthors { get; set; } = [];
    public int MinLength { get; set; } = 0;
}

public sealed class SummarizationOptions
{
    public int Threshold { get; set; } = 500;
}

public sealed class PrioritizationOptions
{
    public IReadOnlyList<string> HighKeywords { get; set; } = [];
    public IReadOnlyList<string> LowKeywords { get; set; } = [];
}

public sealed class GroupingOptions
{
    public int WindowMs { get; set; } = 30000;
    public int FlushIntervalMs { get; set; } = 30000;
}