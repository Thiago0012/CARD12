const UNITY_JWKS_URL =
  "https://player-auth.services.api.unity.com/.well-known/jwks.json";
const PUBLIC_ID_PATTERN = /^\d{12}$/;
const KEY_PATTERN = /^[a-z0-9][a-z0-9._-]{0,63}$/;
const SESSION_TTL_SECONDS = 180;
const ACCESS_SNAPSHOT_SECONDS = 600;
let jwksCache = { expiresAt: 0, keys: [] };

export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);
      if (request.method === "GET" && url.pathname === "/health") {
        return json({ ok: true, service: "card12-player-directory", schemaVersion: 2 });
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
  const publicProfile = normalizePublicProfile(body, player, now);
  await env.DB.prepare(
    `UPDATE players
       SET display_name = ?, normalized_name = ?, last_seen_utc = ?,
           last_build_version = ?, last_platform = ?, equipped_icon_id = ?,
           public_profile_schema_version = ?, ranked_points = ?,
           duels_played = ?, wins = ?, losses = ?, draws = ?,
           profile_updated_utc = ?
     WHERE unity_player_id = ?`)
    .bind(
      displayName,
      normalizeName(displayName),
      now,
      cleanShort(body.buildVersion, 32),
      cleanShort(body.platform, 32),
      publicProfile.equippedIconId,
      publicProfile.schemaVersion,
      publicProfile.rankedPoints,
      publicProfile.duelsPlayed,
      publicProfile.wins,
      publicProfile.losses,
      publicProfile.draws,
      publicProfile.updatedUtc,
      identity.playerId)
    .run();

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

async function searchPlayer(env, requester, rawQuery) {
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
    lastSeenUtcUnixSeconds: player.last_seen_utc,
    online: Boolean(active),
    message: "Perfil encontrado."
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

function normalizePublicProfile(body, current, now) {
  const requestedVersion = clampInteger(
    body.publicProfileSchemaVersion,
    0,
    16);
  if (requestedVersion < 1) {
    return {
      schemaVersion: Number(current.public_profile_schema_version || 0),
      equippedIconId: String(current.equipped_icon_id || ""),
      rankedPoints: Number(current.ranked_points || 0),
      duelsPlayed: Number(current.duels_played || 0),
      wins: Number(current.wins || 0),
      losses: Number(current.losses || 0),
      draws: Number(current.draws || 0),
      updatedUtc: Number(current.profile_updated_utc || 0)
    };
  }

  const proposedIcon = cleanShort(body.equippedIconId, 64).trim();
  const equippedIconId = KEY_PATTERN.test(proposedIcon)
    ? proposedIcon
    : String(current.equipped_icon_id || "");
  const wins = clampInteger(body.wins, 0, 2147483647);
  const losses = clampInteger(body.losses, 0, 2147483647);
  const draws = clampInteger(body.draws, 0, 2147483647);
  const decidedTotal = Math.min(2147483647, wins + losses + draws);
  return {
    schemaVersion: requestedVersion,
    equippedIconId,
    rankedPoints: clampInteger(body.rankedPoints, 0, 200),
    duelsPlayed: Math.max(
      decidedTotal,
      clampInteger(body.duelsPlayed, 0, 2147483647)),
    wins,
    losses,
    draws,
    updatedUtc: now
  };
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
