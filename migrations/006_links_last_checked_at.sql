ALTER TABLE links
    ADD COLUMN IF NOT EXISTS last_checked_at TIMESTAMPTZ NOT NULL DEFAULT '0001-01-01 00:00:00+00';

CREATE INDEX IF NOT EXISTS ix_links_last_checked_at ON links (last_checked_at, id);
