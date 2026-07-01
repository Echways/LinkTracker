using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.AiAgent.Infrastructure.Configuration.AiAgent;
using LinkTracker.Shared.Contracts.Bot;
using Microsoft.Extensions.Options;

namespace LinkTracker.AiAgent.Infrastructure.Services;

internal sealed class LinkUpdateFilter(IOptions<AiAgentOptions> options) : ILinkUpdateFilter
{
    public bool ShouldFilter(LinkUpdate update)
    {
        var filtering = options.Value.Filtering;

        if (update.Description.Length < filtering.MinLength)
        {
            return true;
        }

        if (filtering.ExcludedAuthors.Any(author => string.Equals(author, update.Author, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (filtering.StopWords.Any(word => update.Description.Contains(word, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}