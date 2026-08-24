PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS players (
    unity_player_id TEXT PRIMARY KEY NOT NULL,
    public_id TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL DEFAULT '',
    normalized_name TEXT NOT NULL DEFAULT '',
    equipped_icon_id TEXT NOT NULL DEFAULT '',
    blocked INTEGER NOT NULL DEFAULT 0 CHECK (blocked IN (0, 1)),
    block_message TEXT NOT NULL DEFAULT '',
    first_seen_utc INTEGER NOT NULL,
    last_seen_utc INTEGER NOT NULL,
    last_build_version TEXT NOT NULL DEFAULT '',
    last_platform TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS idx_players_normalized_name
    ON players(normalized_name);
CREATE INDEX IF NOT EXISTS idx_players_last_seen
    ON players(last_seen_utc DESC);

CREATE TABLE IF NOT EXISTS player_features (
    unity_player_id TEXT NOT NULL,
    feature_key TEXT NOT NULL,
    granted_utc INTEGER NOT NULL,
    PRIMARY KEY (unity_player_id, feature_key),
    FOREIGN KEY (unity_player_id) REFERENCES players(unity_player_id)
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS player_capability_blocks (
    unity_player_id TEXT NOT NULL,
    capability_key TEXT NOT NULL,
    blocked_utc INTEGER NOT NULL,
    PRIMARY KEY (unity_player_id, capability_key),
    FOREIGN KEY (unity_player_id) REFERENCES players(unity_player_id)
        ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS player_sessions (
    session_id TEXT PRIMARY KEY NOT NULL,
    unity_player_id TEXT NOT NULL,
    opened_utc INTEGER NOT NULL,
    last_heartbeat_utc INTEGER NOT NULL,
    build_version TEXT NOT NULL DEFAULT '',
    platform TEXT NOT NULL DEFAULT '',
    FOREIGN KEY (unity_player_id) REFERENCES players(unity_player_id)
        ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_sessions_player
    ON player_sessions(unity_player_id, last_heartbeat_utc DESC);

CREATE TABLE IF NOT EXISTS player_audit_log (
    audit_id INTEGER PRIMARY KEY AUTOINCREMENT,
    unity_player_id TEXT NOT NULL,
    action TEXT NOT NULL,
    detail TEXT NOT NULL DEFAULT '',
    created_utc INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_audit_player_created
    ON player_audit_log(unity_player_id, created_utc DESC);
