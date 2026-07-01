using LinkTracker.Shared.Contracts.Bot;

namespace LinkTracker.Bot.Infrastructure.Clients.Kafka;

internal sealed class KafkaLinkUpdateMessageParser
{
    public bool TryValidate(LinkUpdate? update, out string? error)
    {
        error = null;

        if (update is null)
        {
            error = "Сообщение не удалось десериализовать.";
            return false;
        }

        if (update.Id < 0)
        {
            error = "Поле 'id' не может быть отрицательным.";
            return false;
        }

        if (update.Url is null || !update.Url.IsAbsoluteUri)
        {
            error = "Поле 'url' должно содержать абсолютный URI.";
            return false;
        }

        if (update.TgChatIds.Count != 0)
        {
            return true;
        }

        error = "Поле 'tgChatIds' должно содержать хотя бы один chat id.";
        return false;
    }
}