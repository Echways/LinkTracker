using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.AiAgent.Infrastructure.Services;
using LinkTracker.Shared.Contracts.AiAgent;
using Microsoft.Extensions.Options;

namespace LinkTracker.Tests.AiAgent.Unit.Application.Services;

[Trait("Module", "AiAgent")]
[Trait("Category", "Unit")]
public sealed class LinkUpdatePrioritizerTests
{
    private static KeywordLinkUpdatePrioritizer CreatePrioritizer(
        IReadOnlyList<string>? highKeywords = null,
        IReadOnlyList<string>? lowKeywords = null)
    {
        var options = Options.Create(new AiAgentOptions { Prioritization = new PrioritizationOptions { HighKeywords = highKeywords ?? ["critical", "urgent", "breaking", "security"], LowKeywords = lowKeywords ?? ["minor", "typo", "chore", "docs"] } });

        return new KeywordLinkUpdatePrioritizer(options);
    }

    [Theory]
    [InlineData("critical bug fix in production", LinkUpdatePriority.High)]
    [InlineData("new feature added to the dashboard", LinkUpdatePriority.Medium)]
    [InlineData("fix typo in readme", LinkUpdatePriority.Low)]
    [InlineData("critical typo fix in docs", LinkUpdatePriority.High)]
    [InlineData("CRITICAL issue in payment service", LinkUpdatePriority.High)]
    public void Prioritize_ReturnsExpectedPriority(string description, LinkUpdatePriority expected)
    {
        var prioritizer = CreatePrioritizer();

        var result = prioritizer.Prioritize(description);

        Assert.Equal(expected, result);
    }
}