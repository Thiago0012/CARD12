const UNITY_JWKS_URL =
  "https://player-auth.services.api.unity.com/.well-known/jwks.json";
const PUBLIC_ID_PATTERN = /^\d{12}$/;
const KEY_PATTERN = /^[a-z0-9][a-z0-9._-]{0,63}$/;
const SESSION_TTL_SECONDS = 180;
const ACCESS_SNAPSHOT_SECONDS = 600;
const CHALLENGE_TTL_SECONDS = 180;
const CHALLENGE_READY_TTL_SECONDS = 300;
const ACTIVE_CHALLENGE_STATUSES = ["pending", "accepted", "ready"];
const CAPABILITY_ONLINE = "online";
const CAPABILITY_RANKED = "ranked";
const FEATURE_EXCLUSIVE_ANIMATED_PROFILE_ICONS =
  "exclusive-animated-profile-icons";
const EXCLUSIVE_ANIMATED_PROFILE_ICON_IDS = new Set([
  "icon-crimson-veil-arcanist",
  "icon-azure-tempest-dragon",
  "icon-violet-eclipse-sorceress"
]);
let jwksCache = { expiresAt: 0, keys: [] };

export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);
      if (request.method === "GET" && url.pathname === "/health") {
        return json({ ok: true, service: "card12-player-directory", schemaVersion: 5 });
      }

      if (url.pathname.startsWith("/v1/admin/")) {
        return await handleAdmin(request, env, url);
      }

      const identity = await authenticatePlayer(request, env);
      if (request.method === "POST" &&
          (url.pathname === "/v1/player/open" ||
           url.pathname === "/v1/player/heartbeat")) {
        return await openOrHeartbeat(
          request,
          env,
          identity,
          url.pathname.endsWith("heartbeat") ? "heartbeat" : "open");
      }
      if (request.method === "GET" && url.pathname === "/v1/player/search") {
        return await searchPlayer(env, identity, url.searchParams.get("query") || "");
      }
      if (request.method === "GET" && url.pathname === "/v1/player/me") {
        return await getPrivateAccount(env, identity);
      }
      if (request.method === "GET" &&
          url.pathname === "/v1/duel/challenges") {
        return await getDuelChallengeState(env, identity);
      }
      if (request.method === "POST" &&
          url.pathname === "/v1/duel/challenges") {
        return await createDuelChallenge(request, env, identity);
      }
      const challengeAction = url.pathname.match(
        /^\/v1\/duel\/challenges\/([a-f0-9]{32})\/(accept|decline|cancel|room|joined)$/);
      if (request.method === "POST" && challengeAction) {
        return await mutateDuelChallenge(
          request,
          env,
          identity,
          challengeAction[1],
          challengeAction[2]);
      }
      return json({ message: "Rota não encontrada." }, 404);
    } catch (error) {
      const status = Number.isInteger(error?.status) ? error.status : 500;
      return json(
        { message: status === 500 ? "Falha interna do catálogo." : error.message },
        status);
    }
  }
};

async function openOrHeartbeat(request, env, identity, operation) {
  const body = await readJson(request);
  if (body.playerId && body.playerId !== identity.playerId) {
    throw httpError(403, "O PlayerId enviado não pertence ao token autenticado.");
  }

  const now = unixNow();
  let player = await env.DB.prepare(
    "SELECT * FROM players WHERE unity_player_id = ?")
    .bind(identity.playerId).first();
  if (!player) {
    player = await createPlayer(
      env,
      identity.playerId,
      body.publicId,
      cleanDisplayName(body.playerDisplayName),
      now);
  }

  const displayName = cleanDisplayName(body.playerDisplayName) || player.display_name;
  const publicProfile = await normalizePublicProfile(env, body, player, now);
  const privateProfile = normalizePrivateProfile(body, player, now);
  await env.DB.prepare(
    `UPDATE players
       SET display_name = ?, normalized_name = ?, last_seen_utc = ?,
           last_build_version = ?, last_platform = ?
     WHERE unity_player_id = ?`)
    .bind(
      displayName,
      normalizeName(displayName),
      now,
      cleanShort(body.buildVersion, 32),
      cleanShort(body.platform, 32),
      identity.playerId)
    .run();

  if (publicProfile.shouldUpdate) {
    await env.DB.prepare(
      `UPDATE players
          SET equipped_icon_id = ?, public_profile_schema_version = ?,
              ranked_points = ?, duels_played = ?, wins = ?, losses = ?,
              draws = ?, profile_updated_utc = ?,
              public_profile_revision_utc_ms = ?
        WHERE unity_player_id = ?
          AND public_profile_revision_utc_ms < ?`)
      .bind(
        publicProfile.equippedIconId,
        publicProfile.schemaVersion,
        publicProfile.rankedPoints,
        publicProfile.duelsPlayed,
        publicProfile.wins,
        publicProfile.losses,
        publicProfile.draws,
        publicProfile.updatedUtc,
        publicProfile.revisionUtcMilliseconds,
        identity.playerId,
        publicProfile.revisionUtcMilliseconds)
      .run();
  }

  if (privateProfile.shouldUpdate) {
    await env.DB.prepare(
      `UPDATE players
          SET private_profile_schema_version = ?,
              private_profile_revision_utc_ms = ?, coin_balance = ?,
              owned_icon_count = ?, owned_artwork_count = ?,
              owned_card_copies = ?, unique_card_count = ?, deck_count = ?,
              unlocked_deck_count = ?, craft_points_n = ?, craft_points_r = ?,
              craft_points_sr = ?, craft_points_ur = ?, equipped_artwork_id = ?,
              private_profile_updated_utc = ?
        WHERE unity_player_id = ?
          AND private_profile_revision_utc_ms < ?`)
      .bind(
        privateProfile.schemaVersion,
        privateProfile.revisionUtcMilliseconds,
        privateProfile.coinBalance,
        privateProfile.ownedIconCount,
        privateProfile.ownedArtworkCount,
        privateProfile.ownedCardCopies,
        privateProfile.uniqueCardCount,
        privateProfile.deckCount,
        privateProfile.unlockedDeckCount,
        privateProfile.craftPointsN,
        privateProfile.craftPointsR,
        privateProfile.craftPointsSR,
        privateProfile.craftPointsUR,
        privateProfile.equippedArtworkId,
        privateProfile.updatedUtc,
        identity.playerId,
        privateProfile.revisionUtcMilliseconds)
      .run();
  }

  const sessionId = cleanSessionId(body.sessionId);
  if (sessionId) {
    await env.DB.prepare(
      `INSERT INTO player_sessions
         (session_id, unity_player_id, opened_utc, last_heartbeat_utc,
          build_version, platform)
       VALUES (?, ?, ?, ?, ?, ?)
       ON CONFLICT(session_id) DO UPDATE SET
         last_heartbeat_utc = excluded.last_heartbeat_utc,
         build_version = excluded.build_version,
         platform = excluded.platform`)
      .bind(
        sessionId,
        identity.playerId,
        now,
        now,
        cleanShort(body.buildVersion, 32),
        cleanShort(body.platform, 32))
      .run();
  }

  if (operation === "open") {
    await writeAudit(env, identity.playerId, "session-open", sessionId, now);
  }
  await env.DB.prepare(
    "DELETE FROM player_sessions WHERE last_heartbeat_utc < ?")
    .bind(now - SESSION_TTL_SECONDS * 4).run();
  return json(await buildSnapshot(env, identity.playerId, now));
}

async function getDuelChallengeState(env, identity) {
  // This route is intentionally protected on the server as well as in the
  // Unity client.  A modified client must not be able to read or act on
  // challenge data after its online entitlement has been revoked.
  await requirePlayerCapability(env, identity.playerId, CAPABILITY_ONLINE);
  const now = unixNow();
  await expireDuelChallenges(env, identity.playerId, now);
  const rows = await env.DB.prepare(
    `${duelChallengeSelect()}
     WHERE (c.sender_player_id = ? OR c.recipient_player_id = ?)
       AND c.status IN ('pending', 'accepted', 'ready')
     ORDER BY c.updated_utc DESC
     LIMIT 4`)
    .bind(identity.playerId, identity.playerId)
    .all();
  const rawChallenges = rows.results || [];
  // Only one active challenge may exist for a player, but check every row to
  // keep this fail-closed if that invariant ever changes.
  if (rawChallenges.some(value => value.duel_mode === "ranked")) {
    await requirePlayerCapability(env, identity.playerId, CAPABILITY_RANKED);
  }
  const challenges = rawChallenges.map(serializeDuelChallenge);
  return json({
    schemaVersion: 1,
    incoming: challenges.find(value =>
      value.recipientPlayerId === identity.playerId) || null,
    outgoing: challenges.find(value =>
      value.senderPlayerId === identity.playerId) || null,
    serverUtcUnixSeconds: now,
    message: challenges.length > 0
      ? "Desafio de duelo sincronizado."
      : "Nenhum desafio de duelo ativo."
  });
}

async function createDuelChallenge(request, env, identity) {
  const body = await readJson(request);
  const recipientPlayerId = cleanShort(body.recipientPlayerId, 128).trim();
  const duelMode = String(body.duelMode || "").toLowerCase();
  const clientRequestId = cleanShort(body.clientRequestId, 64)
    .toLowerCase()
    .trim();
  if (!recipientPlayerId) {
    throw httpError(400, "A conta convidada é inválida.");
  }
  if (recipientPlayerId === identity.playerId) {
    throw httpError(400, "Você não pode desafiar a própria conta.");
  }
  if (duelMode !== "casual" && duelMode !== "ranked") {
    throw httpError(400, "Escolha um duelo Casual ou Ranqueado.");
  }
  if (!/^[a-f0-9-]{16,64}$/.test(clientRequestId)) {
    throw httpError(400, "A identificação do convite é inválida.");
  }

  await requireDuelChallengeCapability(env, identity.playerId, duelMode);
  // Do not expose whether a recipient is blocked, which capability was
  // revoked, or the administrator's private block message.
  await requireDuelRecipientAvailability(env, recipientPlayerId, duelMode);

  const prior = await env.DB.prepare(
    `${duelChallengeSelect()}
     WHERE c.sender_player_id = ? AND c.client_request_id = ?
     LIMIT 1`)
    .bind(identity.playerId, clientRequestId)
    .first();
  if (prior) {
    return json({
      challenge: serializeDuelChallenge(prior),
      message: "Convite já registrado."
    });
  }

  const now = unixNow();
  await expireDuelChallenges(env, identity.playerId, now);
  await expireDuelChallenges(env, recipientPlayerId, now);
  const online = await env.DB.prepare(
    `SELECT 1 AS online FROM player_sessions
     WHERE unity_player_id = ? AND last_heartbeat_utc >= ? LIMIT 1`)
    .bind(recipientPlayerId, now - SESSION_TTL_SECONDS)
    .first();
  if (!online) {
    throw httpError(409, "O jogador está offline e não pode receber o desafio agora.");
  }
  const occupied = await env.DB.prepare(
    `SELECT challenge_id FROM duel_challenges
     WHERE (sender_player_id IN (?, ?) OR recipient_player_id IN (?, ?))
       AND status IN ('pending', 'accepted', 'ready')
     LIMIT 1`)
    .bind(
      identity.playerId,
      recipientPlayerId,
      identity.playerId,
      recipientPlayerId)
    .first();
  if (occupied) {
    throw httpError(409, "Uma das contas já possui um desafio de duelo ativo.");
  }

  const challengeId = crypto.randomUUID().replace(/-/g, "");
  await env.DB.prepare(
    `INSERT INTO duel_challenges
       (challenge_id, client_request_id, sender_player_id,
        recipient_player_id, duel_mode, status, room_code,
        created_utc, updated_utc, expires_utc)
     VALUES (?, ?, ?, ?, ?, 'pending', '', ?, ?, ?)`)
    .bind(
      challengeId,
      clientRequestId,
      identity.playerId,
      recipientPlayerId,
      duelMode,
      now,
      now,
      now + CHALLENGE_TTL_SECONDS)
    .run();
  await writeAudit(
    env,
    identity.playerId,
    "duel-challenge-created",
    `${challengeId}:${duelMode}`,
    now);
  const created = await findDuelChallenge(env, challengeId);
  return json({
    challenge: serializeDuelChallenge(created),
    message: "Desafio enviado. Aguardando a resposta do duelista."
  }, 201);
}

async function mutateDuelChallenge(
  request,
  env,
  identity,
  challengeId,
  action) {
  const now = unixNow();
  await expireDuelChallenges(env, identity.playerId, now);
  let challenge = await findDuelChallenge(env, challengeId);
  if (!challenge ||
      (challenge.sender_player_id !== identity.playerId &&
       challenge.recipient_player_id !== identity.playerId)) {
    throw httpError(404, "Desafio de duelo não encontrado.");
  }

  await requireDuelChallengeCapability(
    env,
    identity.playerId,
    challenge.duel_mode);
  if (ACTIVE_CHALLENGE_STATUSES.includes(challenge.status)) {
    const otherPlayerId = challenge.sender_player_id === identity.playerId
      ? challenge.recipient_player_id
      : challenge.sender_player_id;
    await requireExistingDuelOpponentAvailability(
      env,
      otherPlayerId,
      challenge.duel_mode);
  }

  let message;
  if (action === "accept") {
    requireChallengeRole(challenge, identity.playerId, "recipient");
    if (challenge.status === "pending") {
      const changed = await transitionDuelChallenge(
        env,
        challengeId,
        "accepted",
        "",
        now,
        now + CHALLENGE_TTL_SECONDS,
        ["pending"]);
      if (!changed) challenge = await findDuelChallenge(env, challengeId);
    }
    if (challenge.status !== "pending" &&
        challenge.status !== "accepted" &&
        challenge.status !== "ready") {
      throw httpError(409, "Este desafio não está mais disponível.");
    }
    message = "Desafio aceito. Preparando a sala privada...";
  } else if (action === "decline") {
    requireChallengeRole(challenge, identity.playerId, "recipient");
    if (challenge.status !== "pending" && challenge.status !== "accepted") {
      throw httpError(409, "Este desafio não pode mais ser recusado.");
    }
    const changed = await transitionDuelChallenge(
      env,
      challengeId,
      "declined",
      "",
      now,
      now,
      ["pending", "accepted"]);
    if (!changed) {
      throw httpError(409, "Este desafio recebeu outra ação primeiro.");
    }
    message = "Desafio recusado.";
  } else if (action === "cancel") {
    if (!ACTIVE_CHALLENGE_STATUSES.includes(challenge.status)) {
      throw httpError(409, "Este desafio já foi encerrado.");
    }
    const changed = await transitionDuelChallenge(
      env,
      challengeId,
      "cancelled",
      "",
      now,
      now,
      ACTIVE_CHALLENGE_STATUSES);
    if (!changed) {
      throw httpError(409, "Este desafio recebeu outra ação primeiro.");
    }
    message = "Desafio cancelado.";
  } else if (action === "room") {
    requireChallengeRole(challenge, identity.playerId, "sender");
    const body = await readJson(request);
    const roomCode = cleanShort(body.roomCode, 12).trim().toUpperCase();
    if (!/^[A-Z0-9]{6,12}$/.test(roomCode)) {
      throw httpError(400, "O código da sala privada é inválido.");
    }
    if (challenge.status !== "accepted" && challenge.status !== "ready") {
      throw httpError(409, "O convidado ainda não aceitou este desafio.");
    }
    if (challenge.status === "ready" &&
        challenge.room_code && challenge.room_code !== roomCode) {
      throw httpError(409, "Este desafio já possui outra sala privada.");
    }
    const changed = await transitionDuelChallenge(
      env,
      challengeId,
      "ready",
      roomCode,
      now,
      now + CHALLENGE_READY_TTL_SECONDS,
      ["accepted", "ready"]);
    if (!changed) {
      throw httpError(409, "Este desafio foi encerrado antes da sala ficar pronta.");
    }
    message = "Sala privada liberada para o convidado.";
  } else if (action === "joined") {
    requireChallengeRole(challenge, identity.playerId, "recipient");
    if (challenge.status !== "ready" && challenge.status !== "joined") {
      throw httpError(409, "A sala deste desafio ainda não está pronta.");
    }
    if (challenge.status === "ready") {
      const changed = await transitionDuelChallenge(
        env,
        challengeId,
        "joined",
        challenge.room_code,
        now,
        now,
        ["ready"]);
      if (!changed) {
        challenge = await findDuelChallenge(env, challengeId);
        if (challenge?.status !== "joined") {
          throw httpError(409, "A sala foi encerrada antes da confirmação.");
        }
      }
    }
    message = "Entrada na sala confirmada.";
  }

  await writeAudit(
    env,
    identity.playerId,
    `duel-challenge-${action}`,
    challengeId,
    now);
  challenge = await findDuelChallenge(env, challengeId);
  return json({ challenge: serializeDuelChallenge(challenge), message });
}

function requireChallengeRole(challenge, playerId, role) {
  const actual = role === "sender"
    ? challenge.sender_player_id
    : challenge.recipient_player_id;
  if (actual !== playerId) {
    throw httpError(403, "Esta ação não pertence à sua conta.");
  }
}

async function expireDuelChallenges(env, playerId, now) {
  await env.DB.prepare(
    `UPDATE duel_challenges
     SET status = 'expired', updated_utc = ?
     WHERE (sender_player_id = ? OR recipient_player_id = ?)
       AND status IN ('pending', 'accepted', 'ready')
       AND expires_utc <= ?`)
    .bind(now, playerId, playerId, now)
    .run();
}

async function transitionDuelChallenge(
  env,
  challengeId,
  status,
  roomCode,
  updatedUtc,
  expiresUtc,
  allowedStatuses) {
  const expected = Array.isArray(allowedStatuses)
    ? allowedStatuses.filter(value =>
        ACTIVE_CHALLENGE_STATUSES.includes(value))
    : [];
  if (expected.length === 0) {
    throw new Error("A transição precisa de um estado anterior permitido.");
  }
  const placeholders = expected.map(() => "?").join(", ");
  const result = await env.DB.prepare(
    `UPDATE duel_challenges
     SET status = ?, room_code = ?, updated_utc = ?, expires_utc = ?
     WHERE challenge_id = ? AND status IN (${placeholders})`)
    .bind(
      status,
      roomCode,
      updatedUtc,
      expiresUtc,
      challengeId,
      ...expected)
    .run();
  return result.meta?.changes === 1;
}

async function findDuelChallenge(env, challengeId) {
  return await env.DB.prepare(
    `${duelChallengeSelect()} WHERE c.challenge_id = ? LIMIT 1`)
    .bind(challengeId)
    .first();
}

function duelChallengeSelect() {
  return `SELECT c.*,
      sender.public_id AS sender_public_id,
      sender.display_name AS sender_display_name,
      sender.equipped_icon_id AS sender_icon_id,
      sender.ranked_points AS sender_ranked_points,
      recipient.public_id AS recipient_public_id,
      recipient.display_name AS recipient_display_name,
      recipient.equipped_icon_id AS recipient_icon_id,
      recipient.ranked_points AS recipient_ranked_points
    FROM duel_challenges c
    JOIN players sender
      ON sender.unity_player_id = c.sender_player_id
    JOIN players recipient
      ON recipient.unity_player_id = c.recipient_player_id`;
}

function serializeDuelChallenge(row) {
  if (!row) return null;
  return {
    challengeId: row.challenge_id,
    senderPlayerId: row.sender_player_id,
    senderPublicId: row.sender_public_id,
    senderDisplayName: row.sender_display_name,
    senderIconId: row.sender_icon_id,
    senderRankedPoints: row.sender_ranked_points,
    recipientPlayerId: row.recipient_player_id,
    recipientPublicId: row.recipient_public_id,
    recipientDisplayName: row.recipient_display_name,
    recipientIconId: row.recipient_icon_id,
    recipientRankedPoints: row.recipient_ranked_points,
    duelMode: row.duel_mode,
    status: row.status,
    roomCode: row.room_code,
    createdUtcUnixSeconds: row.created_utc,
    updatedUtcUnixSeconds: row.updated_utc,
    expiresUtcUnixSeconds: row.expires_utc
  };
}

async function createPlayer(env, playerId, preferredPublicId, displayName, now) {
  const preferred = PUBLIC_ID_PATTERN.test(String(preferredPublicId || ""))
    ? String(preferredPublicId)
    : null;
  for (let attempt = 0; attempt < 24; attempt++) {
    const publicId = attempt === 0 && preferred ? preferred : randomPublicId();
    const inserted = await env.DB.prepare(
      `INSERT OR IGNORE INTO players
         (unity_player_id, public_id, display_name, normalized_name,
          first_seen_utc, last_seen_utc)
       VALUES (?, ?, ?, ?, ?, ?)`)
      .bind(playerId, publicId, displayName, normalizeName(displayName), now, now)
      .run();
    if (inserted.meta?.changes === 1) {
      await writeAudit(env, playerId, "player-created", publicId, now);
      return await env.DB.prepare(
        "SELECT * FROM players WHERE unity_player_id = ?")
        .bind(playerId).first();
    }
    const concurrent = await env.DB.prepare(
      "SELECT * FROM players WHERE unity_player_id = ?")
      .bind(playerId).first();
    if (concurrent) return concurrent;
  }
  throw httpError(503, "Não foi possível reservar um ID numérico único.");
}

async function buildSnapshot(env, playerId, now = unixNow()) {
  const player = await env.DB.prepare(
    "SELECT * FROM players WHERE unity_player_id = ?")
    .bind(playerId).first();
  if (!player) throw httpError(404, "Jogador não catalogado.");
  const features = await env.DB.prepare(
    "SELECT feature_key FROM player_features WHERE unity_player_id = ? ORDER BY feature_key")
    .bind(playerId).all();
  const blocks = await env.DB.prepare(
    "SELECT capability_key FROM player_capability_blocks WHERE unity_player_id = ? ORDER BY capability_key")
    .bind(playerId).all();
  return {
    schemaVersion: 1,
    playerId: player.unity_player_id,
    publicId: player.public_id,
    blockGameAccess: player.blocked === 1,
    blockedCapabilities: blocks.results.map(row => row.capability_key),
    grantedFeatures: features.results.map(row => row.feature_key),
    message: player.block_message || "",
    firstSeenUtcUnixSeconds: player.first_seen_utc,
    lastSeenUtcUnixSeconds: player.last_seen_utc,
    validUntilUtcUnixSeconds: now + ACCESS_SNAPSHOT_SECONDS
  };
}

async function requireDuelChallengeCapability(env, playerId, duelMode) {
  await requirePlayerCapability(env, playerId, CAPABILITY_ONLINE);
  if (duelMode === "ranked") {
    await requirePlayerCapability(env, playerId, CAPABILITY_RANKED);
  }
}

async function requirePlayerCapability(env, playerId, capability) {
  const key = String(capability || "").trim().toLowerCase();
  if (key !== CAPABILITY_ONLINE && key !== CAPABILITY_RANKED) {
    throw new Error(`Capacidade de jogador não suportada: ${key}`);
  }

  const player = await env.DB.prepare(
    "SELECT unity_player_id, blocked FROM players WHERE unity_player_id = ?")
    .bind(playerId)
    .first();
  if (!player || player.blocked === 1) {
    throw httpError(403, "O acesso online desta conta não está autorizado.");
  }

  const blocked = await env.DB.prepare(
    `SELECT capability_key
       FROM player_capability_blocks
      WHERE unity_player_id = ?
        AND capability_key IN (?, '*')
      LIMIT 1`)
    .bind(playerId, key)
    .first();
  if (blocked) {
    throw httpError(403, "O acesso online desta conta não está autorizado.");
  }
  return player;
}

async function requireDuelRecipientAvailability(env, playerId, duelMode) {
  try {
    await requireDuelChallengeCapability(env, playerId, duelMode);
  } catch (error) {
    if (error?.status === 403 || error?.status === 404) {
      throw httpError(404, "O jogador convidado não está disponível.");
    }
    throw error;
  }
}

async function requireExistingDuelOpponentAvailability(env, playerId, duelMode) {
  try {
    await requireDuelChallengeCapability(env, playerId, duelMode);
  } catch (error) {
    if (error?.status === 403 || error?.status === 404) {
      throw httpError(409, "O outro duelista não está disponível.");
    }
    throw error;
  }
}

async function searchPlayer(env, requester, rawQuery) {
  await requirePlayerCapability(env, requester.playerId, CAPABILITY_ONLINE);
  const query = cleanShort(rawQuery, 64).trim();
  if (query.length < 3) {
    throw httpError(400, "Digite ao menos três caracteres ou o ID completo.");
  }
  let player;
  if (PUBLIC_ID_PATTERN.test(query)) {
    player = await env.DB.prepare(
      "SELECT * FROM players WHERE public_id = ?")
      .bind(query).first();
  } else {
    player = await env.DB.prepare(
      `SELECT * FROM players
       WHERE normalized_name = ?
       ORDER BY last_seen_utc DESC, public_id ASC
       LIMIT 1`)
      .bind(normalizeName(query)).first();
  }
  if (!player || player.blocked === 1 || player.unity_player_id === requester.playerId) {
    return json({ found: false, message: "Nenhum jogador foi encontrado." });
  }
  const now = unixNow();
  const active = await env.DB.prepare(
    `SELECT 1 AS online FROM player_sessions
     WHERE unity_player_id = ? AND last_heartbeat_utc >= ? LIMIT 1`)
    .bind(player.unity_player_id, now - SESSION_TTL_SECONDS).first();
  return json({
    found: true,
    playerId: player.unity_player_id,
    publicId: player.public_id,
    displayName: player.display_name,
    unityPlayerName: player.display_name,
    equippedIconId: player.equipped_icon_id,
    publicProfileSchemaVersion: player.public_profile_schema_version,
    rankTier: resolveRankTier(player.ranked_points),
    rankedPoints: player.ranked_points,
    duelsPlayed: player.duels_played,
    wins: player.wins,
    losses: player.losses,
    draws: player.draws,
    profileUpdatedUtcUnixSeconds: player.profile_updated_utc,
    publicProfileRevisionUtcMilliseconds:
      player.public_profile_revision_utc_ms,
    lastSeenUtcUnixSeconds: player.last_seen_utc,
    online: Boolean(active),
    message: "Perfil encontrado."
  });
}

async function getPrivateAccount(env, identity) {
  const player = await env.DB.prepare(
    "SELECT * FROM players WHERE unity_player_id = ?")
    .bind(identity.playerId)
    .first();
  if (!player) {
    throw httpError(
      404,
      "Abra o jogo ao menos uma vez para sincronizar esta conta com o site.");
  }

  const now = unixNow();
  const active = await env.DB.prepare(
    `SELECT 1 AS online FROM player_sessions
     WHERE unity_player_id = ? AND last_heartbeat_utc >= ? LIMIT 1`)
    .bind(identity.playerId, now - SESSION_TTL_SECONDS)
    .first();
  return json({
    schemaVersion: 1,
    playerId: player.unity_player_id,
    publicId: player.public_id,
    displayName: player.display_name,
    equippedIconId: player.equipped_icon_id,
    equippedArtworkId: player.equipped_artwork_id,
    rankTier: resolveRankTier(player.ranked_points),
    rankedPoints: player.ranked_points,
    duelsPlayed: player.duels_played,
    wins: player.wins,
    losses: player.losses,
    draws: player.draws,
    coinBalance: player.coin_balance,
    ownedIconCount: player.owned_icon_count,
    ownedArtworkCount: player.owned_artwork_count,
    ownedCardCopies: player.owned_card_copies,
    uniqueCardCount: player.unique_card_count,
    deckCount: player.deck_count,
    unlockedDeckCount: player.unlocked_deck_count,
    craftPoints: {
      n: player.craft_points_n,
      r: player.craft_points_r,
      sr: player.craft_points_sr,
      ur: player.craft_points_ur
    },
    profileReady: player.private_profile_schema_version > 0,
    profileUpdatedUtcUnixSeconds: player.private_profile_updated_utc,
    lastSeenUtcUnixSeconds: player.last_seen_utc,
    buildVersion: player.last_build_version,
    platform: player.last_platform,
    online: Boolean(active),
    message: player.private_profile_schema_version > 0
      ? "Conta sincronizada com o jogo."
      : "Abra uma versão atualizada do jogo para enviar moedas e coleção ao site."
  });
}

async function handleAdmin(request, env, url) {
  const expected = String(env.ADMIN_TOKEN || "");
  const supplied = (request.headers.get("Authorization") || "").replace(/^Bearer\s+/i, "");
  if (!expected || !constantTimeEqual(expected, supplied)) {
    throw httpError(401, "Credencial administrativa inválida.");
  }
  const match = url.pathname.match(
    /^\/v1\/admin\/player\/(\d{12})\/(feature|block)\/([a-z0-9][a-z0-9._-]{0,63})$/);
  if (!match) return json({ message: "Rota administrativa inválida." }, 404);
  const [, publicId, operation, rawKey] = match;
  const key = rawKey.toLowerCase();
  if (!KEY_PATTERN.test(key)) throw httpError(400, "Chave inválida.");
  const player = await env.DB.prepare(
    "SELECT unity_player_id FROM players WHERE public_id = ?")
    .bind(publicId).first();
  if (!player) throw httpError(404, "ID não encontrado.");
  const grant = request.method === "PUT";
  const revoke = request.method === "DELETE";
  if (!grant && !revoke) throw httpError(405, "Método não permitido.");
  const table = operation === "feature"
    ? "player_features"
    : "player_capability_blocks";
  const column = operation === "feature" ? "feature_key" : "capability_key";
  if (grant) {
    await env.DB.prepare(
      `INSERT OR IGNORE INTO ${table} (unity_player_id, ${column}, ${operation === "feature" ? "granted_utc" : "blocked_utc"}) VALUES (?, ?, ?)`)
      .bind(player.unity_player_id, key, unixNow()).run();
  } else {
    await env.DB.prepare(
      `DELETE FROM ${table} WHERE unity_player_id = ? AND ${column} = ?`)
      .bind(player.unity_player_id, key).run();
  }
  await writeAudit(
    env,
    player.unity_player_id,
    `${operation}-${grant ? "grant" : "revoke"}`,
    key,
    unixNow());
  return json(await buildSnapshot(env, player.unity_player_id));
}

async function authenticatePlayer(request, env) {
  const authorization = request.headers.get("Authorization") || "";
  const token = authorization.replace(/^Bearer\s+/i, "");
  if (!token || token === authorization) throw httpError(401, "Token ausente.");
  const parts = token.split(".");
  if (parts.length !== 3) throw httpError(401, "Token malformado.");
  const header = decodeJson(parts[0]);
  const payload = decodeJson(parts[1]);
  if (header.alg !== "RS256" || !header.kid) throw httpError(401, "Algoritmo inválido.");
  const jwk = await findJwk(header.kid);
  const key = await crypto.subtle.importKey(
    "jwk",
    jwk,
    { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
    false,
    ["verify"]);
  const validSignature = await crypto.subtle.verify(
    "RSASSA-PKCS1-v1_5",
    key,
    base64UrlBytes(parts[2]),
    new TextEncoder().encode(`${parts[0]}.${parts[1]}`));
  if (!validSignature) throw httpError(401, "Assinatura inválida.");
  const now = unixNow();
  const issuer = String(env.AUTH_ISSUER || "https://player-auth.services.api.unity.com");
  if (payload.iss !== issuer || Number(payload.exp || 0) <= now ||
      Number(payload.nbf || 0) > now + 30) {
    throw httpError(401, "Token expirado ou fora da validade.");
  }
  if (env.UNITY_PROJECT_ID && payload.project_id !== env.UNITY_PROJECT_ID) {
    throw httpError(403, "Token emitido para outro projeto Unity.");
  }
  if (!payload.sub) throw httpError(401, "Token sem PlayerId.");
  return { playerId: payload.sub };
}

async function findJwk(kid) {
  const now = Date.now();
  if (jwksCache.expiresAt <= now || !jwksCache.keys.some(key => key.kid === kid)) {
    const response = await fetch(UNITY_JWKS_URL, {
      headers: { Accept: "application/json" }
    });
    if (!response.ok) throw httpError(503, "Não foi possível validar a autenticação Unity.");
    const body = await response.json();
    jwksCache = { expiresAt: now + 8 * 60 * 60 * 1000, keys: body.keys || [] };
  }
  const key = jwksCache.keys.find(candidate => candidate.kid === kid);
  if (!key) throw httpError(401, "Chave de autenticação desconhecida.");
  return key;
}

function randomPublicId() {
  const bytes = new Uint8Array(12);
  crypto.getRandomValues(bytes);
  return Array.from(bytes, (value, index) =>
    String(index === 0 ? 1 + (value % 9) : value % 10)).join("");
}

function normalizeName(value) {
  return cleanDisplayName(value)
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLocaleLowerCase("pt-BR");
}

function cleanDisplayName(value) {
  return String(value || "").trim().replace(/\s+/g, " ").slice(0, 18);
}

function cleanShort(value, limit) {
  return String(value || "").replace(/[\u0000-\u001f\u007f]/g, "").slice(0, limit);
}

function cleanSessionId(value) {
  const text = String(value || "").trim();
  return /^[a-f0-9]{32}$/i.test(text) ? text.toLowerCase() : "";
}

async function normalizePublicProfile(env, body, current, now) {
  const requestedVersion = clampInteger(
    body.publicProfileSchemaVersion,
    0,
    16);
  const requestedRevision = clampInteger(
    body.publicProfileRevisionUtcMilliseconds,
    0,
    Number.MAX_SAFE_INTEGER);
  const currentRevision = clampInteger(
    current.public_profile_revision_utc_ms,
    0,
    Number.MAX_SAFE_INTEGER);
  if (requestedVersion < 1 || requestedRevision <= 0 ||
      requestedRevision <= currentRevision) {
    return { shouldUpdate: false };
  }

  const proposedIcon = cleanShort(body.equippedIconId, 64).trim();
  let equippedIconId = KEY_PATTERN.test(proposedIcon)
    ? proposedIcon
    : String(current.equipped_icon_id || "");
  if (EXCLUSIVE_ANIMATED_PROFILE_ICON_IDS.has(equippedIconId) &&
      !await playerHasFeature(
        env,
        current.unity_player_id,
        FEATURE_EXCLUSIVE_ANIMATED_PROFILE_ICONS)) {
    equippedIconId = "icon-arcane-default";
  }
  const wins = clampInteger(body.wins, 0, 2147483647);
  const losses = clampInteger(body.losses, 0, 2147483647);
  const draws = clampInteger(body.draws, 0, 2147483647);
  const decidedTotal = Math.min(2147483647, wins + losses + draws);
  return {
    shouldUpdate: true,
    schemaVersion: requestedVersion,
    equippedIconId,
    rankedPoints: clampInteger(body.rankedPoints, 0, 200),
    duelsPlayed: Math.max(
      decidedTotal,
      clampInteger(body.duelsPlayed, 0, 2147483647)),
    wins,
    losses,
    draws,
    updatedUtc: now,
    revisionUtcMilliseconds: requestedRevision
  };
}

function normalizePrivateProfile(body, current, now) {
  const requestedVersion = clampInteger(
    body.privateProfileSchemaVersion,
    0,
    16);
  const requestedRevision = clampInteger(
    body.privateProfileRevisionUtcMilliseconds,
    0,
    Number.MAX_SAFE_INTEGER);
  const currentRevision = clampInteger(
    current.private_profile_revision_utc_ms,
    0,
    Number.MAX_SAFE_INTEGER);
  if (requestedVersion < 1 || requestedRevision <= 0 ||
      requestedRevision <= currentRevision) {
    return { shouldUpdate: false };
  }

  const artwork = cleanShort(body.equippedArtworkId, 64).trim();
  return {
    shouldUpdate: true,
    schemaVersion: requestedVersion,
    revisionUtcMilliseconds: requestedRevision,
    coinBalance: clampInteger(body.coinBalance, 0, 2147483647),
    ownedIconCount: clampInteger(body.ownedIconCount, 0, 10000),
    ownedArtworkCount: clampInteger(body.ownedArtworkCount, 0, 10000),
    ownedCardCopies: clampInteger(body.ownedCardCopies, 0, 2147483647),
    uniqueCardCount: clampInteger(body.uniqueCardCount, 0, 1000000),
    deckCount: clampInteger(body.deckCount, 0, 10000),
    unlockedDeckCount: clampInteger(body.unlockedDeckCount, 0, 10000),
    craftPointsN: clampInteger(body.craftPointsN, 0, 2147483647),
    craftPointsR: clampInteger(body.craftPointsR, 0, 2147483647),
    craftPointsSR: clampInteger(body.craftPointsSR, 0, 2147483647),
    craftPointsUR: clampInteger(body.craftPointsUR, 0, 2147483647),
    equippedArtworkId: KEY_PATTERN.test(artwork) ? artwork : "",
    updatedUtc: now
  };
}

async function playerHasFeature(env, playerId, featureKey) {
  const record = await env.DB.prepare(
    "SELECT 1 AS granted FROM player_features WHERE unity_player_id = ? AND feature_key = ? LIMIT 1")
    .bind(playerId, featureKey).first();
  return Boolean(record);
}

function clampInteger(value, minimum, maximum) {
  const numeric = Number(value);
  if (!Number.isFinite(numeric)) return minimum;
  return Math.max(minimum, Math.min(maximum, Math.trunc(numeric)));
}

function resolveRankTier(rankedPoints) {
  const points = clampInteger(rankedPoints, 0, 200);
  return points >= 200 ? 8 : Math.min(7, Math.floor(points / 25));
}

function decodeJson(value) {
  try {
    return JSON.parse(new TextDecoder().decode(base64UrlBytes(value)));
  } catch {
    throw httpError(401, "Token inválido.");
  }
}

function base64UrlBytes(value) {
  const normalized = value.replace(/-/g, "+").replace(/_/g, "/");
  const padded = normalized + "=".repeat((4 - normalized.length % 4) % 4);
  const binary = atob(padded);
  return Uint8Array.from(binary, character => character.charCodeAt(0));
}

async function readJson(request) {
  try {
    return await request.json();
  } catch {
    throw httpError(400, "JSON inválido.");
  }
}

async function writeAudit(env, playerId, action, detail, now) {
  await env.DB.prepare(
    "INSERT INTO player_audit_log (unity_player_id, action, detail, created_utc) VALUES (?, ?, ?, ?)")
    .bind(playerId, action, cleanShort(detail, 160), now).run();
}

function constantTimeEqual(left, right) {
  const a = new TextEncoder().encode(left);
  const b = new TextEncoder().encode(right);
  let difference = a.length ^ b.length;
  const length = Math.max(a.length, b.length);
  for (let index = 0; index < length; index++) {
    difference |= (a[index % Math.max(1, a.length)] || 0) ^
                  (b[index % Math.max(1, b.length)] || 0);
  }
  return difference === 0;
}

function unixNow() {
  return Math.floor(Date.now() / 1000);
}

function httpError(status, message) {
  const error = new Error(message);
  error.status = status;
  return error;
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: {
      "Content-Type": "application/json; charset=utf-8",
      "Cache-Control": "no-store",
      "X-Content-Type-Options": "nosniff"
    }
  });
}
