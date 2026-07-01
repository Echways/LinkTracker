namespace LinkTracker.Shared.Contracts.Common;

public static class ScrapperErrorCodes
{
    public const string MissingHeader = "missing_header";
    public const string InvalidRequest = "invalid_request";
    public const string InvalidChatId = "invalid_chat_id";
    public const string ChatAlreadyExists = "chat_already_exists";
    public const string ChatNotFound = "chat_not_found";
    public const string LinkAlreadyExists = "link_already_exists";
    public const string LinkNotFound = "link_not_found";
    public const string InvalidLink = "invalid_link";
    public const string InvalidLinkScheme = "invalid_link_scheme";
    public const string UnsupportedLink = "unsupported_link";
    public const string ScrapperServiceUnavailable = "scrapper_service_unavailable";
}