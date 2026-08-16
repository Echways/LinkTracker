namespace LinkTracker.Scrapper.Infrastructure.Outbox.Sql;

internal static class OutboxMessageCommands
{
    public const string Add =
        """
        INSERT INTO outbox_messages (payload)
        VALUES (@payload::jsonb);
        """;

    public const string SetCursor =
        """
        UPDATE links
        SET
            last_updated_at = @lastUpdatedAt,
            last_event_key = @lastEventKey
        WHERE id = @linkId;
        """;

    public const string ClaimUnprocessedBatch =
        """
        UPDATE outbox_messages AS o
        SET locked_until = now() + make_interval(secs => @lockSeconds)
        FROM (
            SELECT id
            FROM outbox_messages
            WHERE processed_at IS NULL
              AND retry_count < @maxRetryCount
              AND (locked_until IS NULL OR locked_until <= now())
            ORDER BY created_at, id
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
        ) AS claimed
        WHERE o.id = claimed.id
        RETURNING o.id,
                  o.payload::text,
                  o.created_at,
                  o.processed_at,
                  o.error,
                  o.retry_count;
        """;

    public const string MarkProcessed =
        """
        UPDATE outbox_messages
        SET processed_at = now(),
            error = NULL,
            locked_until = NULL
        WHERE id = @id;
        """;

    public const string MarkFailed =
        """
        UPDATE outbox_messages
        SET error = @error,
            retry_count = retry_count + 1,
            locked_until = NULL
        WHERE id = @id;
        """;
}