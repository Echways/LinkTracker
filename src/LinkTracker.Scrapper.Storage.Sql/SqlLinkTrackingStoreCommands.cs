namespace LinkTracker.Scrapper.Storage.Sql;

internal static class SqlLinkTrackingStoreCommands
{
    public const string TryRegisterChat =
        """
        INSERT INTO chats (id)
        VALUES (@chatId)
        ON CONFLICT DO NOTHING;
        """;

    public const string TryDeleteChat =
        """
        DELETE FROM chats
        WHERE id = @chatId;
        """;

    public const string ChatExists =
        """
        SELECT EXISTS (
            SELECT 1
            FROM chats
            WHERE id = @chatId
        );
        """;

    public const string GetTrackedLinkRows =
        """
        SELECT
            l.id AS Id,
            l.url AS Url,
            l.last_updated_at AS LastUpdatedAt,
            l.last_event_key AS LastEventKey,
            COALESCE(
                ARRAY_AGG(DISTINCT t.name) FILTER (WHERE t.name IS NOT NULL),
                '{}'
            ) AS Tags
        FROM subscriptions s
        JOIN links l ON l.id = s.link_id
        LEFT JOIN subscription_tags st ON st.subscription_id = s.id
        LEFT JOIN tags t ON t.id = st.tag_id
        WHERE s.chat_id = @chatId
        GROUP BY
            l.id,
            l.url,
            l.last_updated_at,
            l.last_event_key
        ORDER BY l.id;
        """;

    public const string SetCursor =
        """
        UPDATE links
        SET
            last_updated_at = @lastUpdatedAt,
            last_event_key = @lastEventKey
        WHERE id = @linkId;
        """;

    public const string GetOrCreateLink =
        """
        INSERT INTO links (url, normalized_url)
        VALUES (@url, @normalizedUrl)
        ON CONFLICT (normalized_url)
        DO UPDATE SET url = links.url
        RETURNING id;
        """;

    public const string CreateSubscription =
        """
        INSERT INTO subscriptions (chat_id, link_id)
        VALUES (@chatId, @linkId)
        ON CONFLICT (chat_id, link_id) DO NOTHING
        RETURNING id;
        """;

    public const string GetTrackedLinkForSubscriptionRemoval =
        """
        SELECT
            s.id AS SubscriptionId,
            l.id AS LinkId,
            l.url AS Url,
            l.last_updated_at AS LastUpdatedAt,
            l.last_event_key AS LastEventKey,
            COALESCE(
                ARRAY_AGG(DISTINCT t.name) FILTER (WHERE t.name IS NOT NULL),
                '{}'
            ) AS Tags
        FROM subscriptions s
        JOIN links l ON l.id = s.link_id
        LEFT JOIN subscription_tags st ON st.subscription_id = s.id
        LEFT JOIN tags t ON t.id = st.tag_id
        WHERE s.chat_id = @chatId
          AND l.normalized_url = @normalizedUrl
        GROUP BY
            s.id,
            l.id,
            l.url,
            l.last_updated_at,
            l.last_event_key;
        """;

    public const string DeleteSubscription =
        """
        DELETE FROM subscriptions
        WHERE id = @subscriptionId;
        """;

    public const string LinkHasSubscriptions =
        """
        SELECT EXISTS (
            SELECT 1
            FROM subscriptions
            WHERE link_id = @linkId
        );
        """;

    public const string DeleteLink =
        """
        DELETE FROM links
        WHERE id = @linkId;
        """;

    public const string TryCreateTag =
        """
        INSERT INTO tags (name)
        VALUES (@name)
        ON CONFLICT (name) DO NOTHING
        RETURNING id;
        """;

    public const string GetTagRows =
        """
        SELECT DISTINCT t.name AS Name
        FROM subscriptions s
        JOIN subscription_tags st ON st.subscription_id = s.id
        JOIN tags t ON t.id = st.tag_id
        WHERE s.chat_id = @chatId
        ORDER BY t.name;
        """;

    public const string GetOrCreateTag =
        """
        INSERT INTO tags (name)
        VALUES (@name)
        ON CONFLICT (name)
        DO UPDATE SET name = EXCLUDED.name
        RETURNING id;
        """;

    public const string AttachTag =
        """
        INSERT INTO subscription_tags (subscription_id, tag_id)
        VALUES (@subscriptionId, @tagId)
        ON CONFLICT DO NOTHING;
        """;

    public const string GetTrackedLinkForTagUpdate =
        """
        SELECT
            s.id AS SubscriptionId,
            l.id AS LinkId,
            l.url AS Url,
            l.last_updated_at AS LastUpdatedAt,
            l.last_event_key AS LastEventKey,
            COALESCE(
                ARRAY_AGG(DISTINCT t.name) FILTER (WHERE t.name IS NOT NULL),
                '{}'
            ) AS Tags
        FROM subscriptions s
        JOIN links l ON l.id = s.link_id
        LEFT JOIN subscription_tags st ON st.subscription_id = s.id
        LEFT JOIN tags t ON t.id = st.tag_id
        WHERE s.chat_id = @chatId
          AND l.normalized_url = @normalizedUrl
        GROUP BY
            s.id,
            l.id,
            l.url,
            l.last_updated_at,
            l.last_event_key;
        """;

    public const string GetSubscriptionIdForTagUpdate =
        """
        SELECT s.id
        FROM subscriptions s
        JOIN links l ON l.id = s.link_id
        WHERE s.chat_id = @chatId
          AND l.normalized_url = @normalizedUrl;
        """;

    public const string RenameTagLinks =
        """
        WITH target_subscriptions AS (
            SELECT s.id AS subscription_id
            FROM subscriptions s
            JOIN subscription_tags st ON st.subscription_id = s.id
            JOIN tags t ON t.id = st.tag_id
            WHERE s.chat_id = @chatId
              AND t.name = @tag
        )
        INSERT INTO subscription_tags (subscription_id, tag_id)
        SELECT ts.subscription_id, @newTagId
        FROM target_subscriptions ts
        ON CONFLICT DO NOTHING;
        """;

    public const string DeleteTagLinks =
        """
        DELETE FROM subscription_tags st
        USING subscriptions s, tags t
        WHERE st.subscription_id = s.id
          AND st.tag_id = t.id
          AND s.chat_id = @chatId
          AND t.name = @tag;
        """;

    public const string TagUsageExistsForChat =
        """
        SELECT EXISTS (
            SELECT 1
            FROM subscriptions s
            JOIN subscription_tags st ON st.subscription_id = s.id
            JOIN tags t ON t.id = st.tag_id
            WHERE s.chat_id = @chatId
              AND t.name = @tag
        );
        """;

    public const string DeleteOrphanTagByName =
        """
        DELETE FROM tags t
        WHERE t.name = @name
          AND NOT EXISTS (
              SELECT 1
              FROM subscription_tags st
              WHERE st.tag_id = t.id
          );
        """;

    public const string GetSubscriptionsDueForCheckRows =
        """
        WITH target_links AS (
            SELECT
                l.id AS Id,
                l.url AS Url,
                l.last_updated_at AS LastUpdatedAt,
                l.last_event_key AS LastEventKey,
                l.last_checked_at AS LastCheckedAt
            FROM links l
            WHERE l.last_checked_at < @checkedBefore
              AND EXISTS (
                  SELECT 1
                  FROM subscriptions s
                  WHERE s.link_id = l.id
              )
            ORDER BY l.last_checked_at, l.id
            LIMIT @batchSize
        )
        SELECT
            tl.Id,
            tl.Url,
            tl.LastUpdatedAt,
            tl.LastEventKey,
            s.chat_id AS ChatId
        FROM target_links tl
        JOIN subscriptions s ON s.link_id = tl.Id
        ORDER BY tl.LastCheckedAt, tl.Id, s.chat_id;
        """;

    public const string MarkChecked =
        """
        UPDATE links
        SET last_checked_at = @checkedAt
        WHERE id = ANY(@linkIds);
        """;
}