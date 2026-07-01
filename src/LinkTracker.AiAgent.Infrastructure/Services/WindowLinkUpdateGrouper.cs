using LinkTracker.AiAgent.Application.Abstractions;
using LinkTracker.Shared.Contracts.AiAgent;

namespace LinkTracker.AiAgent.Infrastructure.Services;

internal sealed class WindowLinkUpdateGrouper : ILinkUpdateGrouper
{
    public IReadOnlyList<ProcessedLinkUpdate> Group(IReadOnlyList<ProcessedLinkUpdate> updates)
    {
        if (updates.Count == 1)
        {
            return updates;
        }

        var maxPriority = updates.Max(u => u.Priority);

        var description = string.Join(
            "\n",
            updates.Select((u, i) => $"{i + 1}. [{u.Url}] {u.Description}"));

        return
        [
            new ProcessedLinkUpdate
            {
                Id = updates[0].Id,
                Url = updates[0].Url,
                Description = description,
                TgChatIds = updates[0].TgChatIds,
                Priority = maxPriority
            }
        ];
    }
}