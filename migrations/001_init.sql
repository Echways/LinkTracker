CREATE TABLE IF NOT EXISTS chats
(
    id BIGINT PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS links
(
    id BIGSERIAL PRIMARY KEY,
    url TEXT NOT NULL,
    normalized_url TEXT NOT NULL UNIQUE,
    last_updated_at TIMESTAMPTZ NULL,
    last_event_key TEXT NULL
);

CREATE TABLE IF NOT EXISTS subscriptions
(
    id BIGSERIAL PRIMARY KEY,
    chat_id BIGINT NOT NULL REFERENCES chats(id) ON DELETE CASCADE,
    link_id BIGINT NOT NULL REFERENCES links(id) ON DELETE CASCADE,
    UNIQUE (chat_id, link_id)
    );

CREATE TABLE IF NOT EXISTS tags
(
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS filters
(
    id BIGSERIAL PRIMARY KEY,
    value TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS subscription_tags
(
    subscription_id BIGINT NOT NULL REFERENCES subscriptions(id) ON DELETE CASCADE,
    tag_id BIGINT NOT NULL REFERENCES tags(id) ON DELETE CASCADE,
    PRIMARY KEY (subscription_id, tag_id)
);

CREATE TABLE IF NOT EXISTS subscription_filters
(
    subscription_id BIGINT NOT NULL REFERENCES subscriptions(id) ON DELETE CASCADE,
    filter_id BIGINT NOT NULL REFERENCES filters(id) ON DELETE CASCADE,
    PRIMARY KEY (subscription_id, filter_id)
);