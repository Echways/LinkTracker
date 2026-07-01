CREATE TABLE IF NOT EXISTS outbox_messages
(
    id BIGSERIAL PRIMARY KEY,
    payload JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    processed_at TIMESTAMPTZ NULL,
    error TEXT NULL,
    retry_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_outbox_messages_unprocessed
    ON outbox_messages (created_at, id)
    WHERE processed_at IS NULL;