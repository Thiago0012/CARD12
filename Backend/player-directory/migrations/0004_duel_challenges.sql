CREATE TABLE IF NOT EXISTS duel_challenges (
    challenge_id TEXT PRIMARY KEY NOT NULL,
    client_request_id TEXT NOT NULL,
    sender_player_id TEXT NOT NULL,
    recipient_player_id TEXT NOT NULL,
    duel_mode TEXT NOT NULL CHECK (duel_mode IN ('casual', 'ranked')),
    status TEXT NOT NULL CHECK (
        status IN (
            'pending', 'accepted', 'ready', 'joined',
            'declined', 'cancelled', 'expired'
        )
    ),
    room_code TEXT NOT NULL DEFAULT '',
    created_utc INTEGER NOT NULL,
    updated_utc INTEGER NOT NULL,
    expires_utc INTEGER NOT NULL,
    FOREIGN KEY (sender_player_id) REFERENCES players(unity_player_id)
        ON DELETE CASCADE,
    FOREIGN KEY (recipient_player_id) REFERENCES players(unity_player_id)
        ON DELETE CASCADE,
    UNIQUE (sender_player_id, client_request_id)
);

CREATE INDEX IF NOT EXISTS idx_duel_challenges_recipient_active
    ON duel_challenges(recipient_player_id, status, updated_utc DESC);

CREATE INDEX IF NOT EXISTS idx_duel_challenges_sender_active
    ON duel_challenges(sender_player_id, status, updated_utc DESC);

CREATE INDEX IF NOT EXISTS idx_duel_challenges_expiry
    ON duel_challenges(status, expires_utc);
