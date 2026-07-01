using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.Shared.Contracts.AiAgent;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Services;

internal sealed class KeywordLinkUpdatePrioritizer(IOptions<AiAgentOptions> options) : ILinkUpdatePrioritizer
{
    public LinkUpdatePriority Prioritize(string description)
    {
        var opts = options.Value.Prioritization;

        if (opts.HighKeywords.Any(kw => description.Contains(kw, StringComparison.OrdinalIgnoreCase)))
        {
            return LinkUpdatePriority.High;
        }

        if (opts.LowKeywords.Any(kw => description.Contains(kw, StringComparison.OrdinalIgnoreCase)))
        {
            return LinkUpdatePriority.Low;
        }

        return LinkUpdatePriority.Medium;
    }
}