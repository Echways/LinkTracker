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

    public const string GetUnprocessedBatch =
        """
        SELECT id,
               payload::text,
               created_at,
               processed_at,
               error,
               retry_count
        FROM outbox_messages
        WHERE processed_at IS NULL
          AND retry_count < @maxRetryCount
        ORDER BY created_at, id
        LIMIT @batchSize
        FOR UPDATE SKIP LOCKED;
        """;

    public const string MarkProcessed =
        """
        UPDATE outbox_messages
        SET processed_at = now(),
            error = NULL
        WHERE id = @id;
        """;

    public const string MarkFailed =
        """
        UPDATE outbox_messages
        SET error = @error,
            retry_count = retry_count + 1
        WHERE id = @id;
        """;
}