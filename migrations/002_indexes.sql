CREATE INDEX IF NOT EXISTS ix_subscriptions_chat_id ON subscriptions(chat_id);
CREATE INDEX IF NOT EXISTS ix_subscriptions_link_id ON subscriptions(link_id);
CREATE INDEX IF NOT EXISTS ix_links_normalized_url ON links(normalized_url);
CREATE INDEX IF NOT EXISTS ix_tags_name ON tags(name);