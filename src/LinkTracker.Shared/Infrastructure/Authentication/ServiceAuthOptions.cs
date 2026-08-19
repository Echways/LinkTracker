namespace LinkTracker.Shared.Infrastructure.Authentication;

public sealed class ServiceAuthOptions
{
    public const string SectionName = "ServiceAuth";

    public string Secret { get; set; } = string.Empty;
}
