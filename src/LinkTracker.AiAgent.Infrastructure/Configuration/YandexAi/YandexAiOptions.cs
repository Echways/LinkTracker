namespace LinkTracker.AiAgent.Infrastructure.Configuration.YandexAi;

public sealed class YandexAiOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string FolderId { get; set; } = string.Empty;
    public string ModelId { get; set; } = "aliceai-llm";
    public string BaseUrl { get; set; } = "https://ai.api.cloud.yandex.net";
    public int TimeoutSeconds { get; set; } = 120;
}