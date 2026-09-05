using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Bot.Infrastructure.Clients.Kafka;

internal sealed class KafkaLinkUpdateMessageParser
{
    public bool TryValidate(LinkUpdate? update, out string? error)
    {
        error = null;

        if (update is null)
        {
            error = "Failed to deserialize the message.";
            return false;
        }

        if (update.Id < 0)
        {
            error = "Field 'id' must not be negative.";
            return false;
        }

        if (update.Url is null || !update.Url.IsAbsoluteUri)
        {
            error = "Field 'url' must contain an absolute URI.";
            return false;
        }

        if (update.TgChatIds.Count != 0)
        {
            return true;
        }

        error = "Field 'tgChatIds' must contain at least one chat id.";
        return false;
    }
}