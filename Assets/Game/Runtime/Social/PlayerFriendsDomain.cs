using System;
using ArcaneDuel.Game.Competitive;

namespace ArcaneDuel.Game.Social
{
    public enum FriendConnectionState
    {
        None,
        IncomingRequest,
        OutgoingRequest,
        Friend,
        Blocked
    }

    public enum FriendPresenceState
    {
        Unknown,
        Online,
        Busy,
        Away,
        Offline
    }

    public enum FriendDuelMode
    {
        Casual,
        Ranked
    }

    public enum FriendDuelChallengeStatus
    {
        Unknown,
        Pending,
        Accepted,
        Ready,
        Joined,
        Declined,
        Cancelled,
        Expired
    }

    [Serializable]
    public sealed class FriendProfileView
    {
        public string playerId;
        public string publicId;
        public string displayName;
        public string unityPlayerName;
        public string equippedIconId;
        public int publicProfileSchemaVersion;
        public RankTier rankTier;
        public int rankedPoints;
        public long duelsPlayed;
        public long wins;
        public long losses;
        public long draws;
        public long profileUpdatedUtcUnixSeconds;
        public long publicProfileRevisionUtcMilliseconds;
        public FriendConnectionState connectionState;
        public FriendPresenceState presence;
        public long lastSeenUtcUnixSeconds;

        public FriendProfileView Copy()
        {
            return new FriendProfileView
            {
                playerId = playerId ?? string.Empty,
                publicId = publicId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                unityPlayerName = unityPlayerName ?? string.Empty,
                equippedIconId = equippedIconId ?? string.Empty,
                publicProfileSchemaVersion = publicProfileSchemaVersion,
                rankTier = rankTier,
                rankedPoints = rankedPoints,
                duelsPlayed = duelsPlayed,
                wins = wins,
                losses = losses,
                draws = draws,
                profileUpdatedUtcUnixSeconds = profileUpdatedUtcUnixSeconds,
                publicProfileRevisionUtcMilliseconds =
                    publicProfileRevisionUtcMilliseconds,
                connectionState = connectionState,
                presence = presence,
                lastSeenUtcUnixSeconds = lastSeenUtcUnixSeconds
            };
        }
    }

    [Serializable]
    public sealed class FriendSearchResponse
    {
        public bool found;
        public string playerId;
        public string publicId;
        public string displayName;
        public string unityPlayerName;
        public string equippedIconId;
        public int publicProfileSchemaVersion;
        public RankTier rankTier;
        public int rankedPoints;
        public long duelsPlayed;
        public long wins;
        public long losses;
        public long draws;
        public long profileUpdatedUtcUnixSeconds;
        public long publicProfileRevisionUtcMilliseconds;
        public long lastSeenUtcUnixSeconds;
        public bool online;
        public string message;

        public FriendProfileView ToProfile()
        {
            return new FriendProfileView
            {
                playerId = (playerId ?? string.Empty).Trim(),
                publicId = (publicId ?? string.Empty).Trim(),
                displayName = (displayName ?? string.Empty).Trim(),
                unityPlayerName = (unityPlayerName ?? string.Empty).Trim(),
                equippedIconId = (equippedIconId ?? string.Empty).Trim(),
                publicProfileSchemaVersion = Math.Max(
                    0,
                    publicProfileSchemaVersion),
                rankTier = rankTier,
                rankedPoints = Math.Max(0, rankedPoints),
                duelsPlayed = Math.Max(0, duelsPlayed),
                wins = Math.Max(0, wins),
                losses = Math.Max(0, losses),
                draws = Math.Max(0, draws),
                profileUpdatedUtcUnixSeconds = Math.Max(
                    0,
                    profileUpdatedUtcUnixSeconds),
                publicProfileRevisionUtcMilliseconds = Math.Max(
                    0,
                    publicProfileRevisionUtcMilliseconds),
                connectionState = FriendConnectionState.None,
                presence = online
                    ? FriendPresenceState.Online
                    : FriendPresenceState.Offline,
                lastSeenUtcUnixSeconds = Math.Max(0, lastSeenUtcUnixSeconds)
            };
        }
    }

    [Serializable]
    public sealed class FriendDuelChallengeView
    {
        public string challengeId;
        public string senderPlayerId;
        public string senderPublicId;
        public string senderDisplayName;
        public string senderIconId;
        public int senderRankedPoints;
        public string recipientPlayerId;
        public string recipientPublicId;
        public string recipientDisplayName;
        public string recipientIconId;
        public int recipientRankedPoints;
        public string duelMode;
        public string status;
        public string roomCode;
        public long createdUtcUnixSeconds;
        public long updatedUtcUnixSeconds;
        public long expiresUtcUnixSeconds;

        public FriendDuelMode Mode =>
            FriendDuelChallengePolicy.ParseMode(duelMode);
        public FriendDuelChallengeStatus Status =>
            FriendDuelChallengePolicy.ParseStatus(status);
        public bool IsActive =>
            FriendDuelChallengePolicy.IsActive(Status);

        public FriendDuelChallengeView Copy()
        {
            return new FriendDuelChallengeView
            {
                challengeId = challengeId ?? string.Empty,
                senderPlayerId = senderPlayerId ?? string.Empty,
                senderPublicId = senderPublicId ?? string.Empty,
                senderDisplayName = senderDisplayName ?? string.Empty,
                senderIconId = senderIconId ?? string.Empty,
                senderRankedPoints = Math.Max(0, senderRankedPoints),
                recipientPlayerId = recipientPlayerId ?? string.Empty,
                recipientPublicId = recipientPublicId ?? string.Empty,
                recipientDisplayName = recipientDisplayName ?? string.Empty,
                recipientIconId = recipientIconId ?? string.Empty,
                recipientRankedPoints = Math.Max(0, recipientRankedPoints),
                duelMode = duelMode ?? string.Empty,
                status = status ?? string.Empty,
                roomCode = roomCode ?? string.Empty,
                createdUtcUnixSeconds = Math.Max(0, createdUtcUnixSeconds),
                updatedUtcUnixSeconds = Math.Max(0, updatedUtcUnixSeconds),
                expiresUtcUnixSeconds = Math.Max(0, expiresUtcUnixSeconds)
            };
        }
    }

    [Serializable]
    public sealed class FriendDuelChallengeStateResponse
    {
        public int schemaVersion;
        public FriendDuelChallengeView incoming;
        public FriendDuelChallengeView outgoing;
        public long serverUtcUnixSeconds;
        public string message;
    }

    [Serializable]
    public sealed class FriendDuelChallengeMutationResponse
    {
        public FriendDuelChallengeView challenge;
        public string message;
    }

    public static class FriendDuelChallengePolicy
    {
        public static FriendDuelMode ParseMode(string value)
        {
            return string.Equals(
                value,
                "ranked",
                StringComparison.OrdinalIgnoreCase)
                ? FriendDuelMode.Ranked
                : FriendDuelMode.Casual;
        }

        public static string SerializeMode(FriendDuelMode mode)
        {
            return mode == FriendDuelMode.Ranked ? "ranked" : "casual";
        }

        public static FriendDuelChallengeStatus ParseStatus(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "pending" => FriendDuelChallengeStatus.Pending,
                "accepted" => FriendDuelChallengeStatus.Accepted,
                "ready" => FriendDuelChallengeStatus.Ready,
                "joined" => FriendDuelChallengeStatus.Joined,
                "declined" => FriendDuelChallengeStatus.Declined,
                "cancelled" => FriendDuelChallengeStatus.Cancelled,
                "expired" => FriendDuelChallengeStatus.Expired,
                _ => FriendDuelChallengeStatus.Unknown
            };
        }

        public static bool IsActive(FriendDuelChallengeStatus status)
        {
            return status == FriendDuelChallengeStatus.Pending ||
                   status == FriendDuelChallengeStatus.Accepted ||
                   status == FriendDuelChallengeStatus.Ready;
        }

        public static bool CanAccept(
            FriendDuelChallengeView challenge,
            string localPlayerId,
            long nowUtcUnixSeconds)
        {
            return challenge != null &&
                   challenge.Status == FriendDuelChallengeStatus.Pending &&
                   string.Equals(
                       challenge.recipientPlayerId,
                       localPlayerId,
                       StringComparison.Ordinal) &&
                   challenge.expiresUtcUnixSeconds > nowUtcUnixSeconds;
        }

        public static string ModeLabel(FriendDuelMode mode)
        {
            return mode == FriendDuelMode.Ranked ? "RANQUEADO" : "CASUAL";
        }
    }

    public static class PlayerFriendSearchPolicy
    {
        public const int MinimumNameLength = 3;
        public const int MaximumQueryLength = 50;

        public static bool TryNormalize(
            string proposedQuery,
            out string normalizedQuery,
            out bool isNumericId,
            out string rejection)
        {
            normalizedQuery = string.Empty;
            isNumericId = false;
            rejection = string.Empty;
            if (string.IsNullOrWhiteSpace(proposedQuery))
            {
                rejection = "Digite o nome ou o ID numérico do jogador.";
                return false;
            }

            string[] pieces = proposedQuery.Trim().Split(
                (char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            normalizedQuery = string.Join(" ", pieces);
            if (normalizedQuery.Length > MaximumQueryLength)
            {
                rejection =
                    $"A busca aceita no máximo {MaximumQueryLength} caracteres.";
                return false;
            }

            isNumericId = true;
            foreach (char character in normalizedQuery)
            {
                if (character < '0' || character > '9')
                {
                    isNumericId = false;
                    break;
                }
            }

            if (isNumericId)
            {
                if (normalizedQuery.Length !=
                    Accounts.PlayerIdAccessPolicy.PublicIdLength)
                {
                    rejection = "O ID do jogador deve possuir exatamente 12 números.";
                    return false;
                }
                return true;
            }

            if (normalizedQuery.Length < MinimumNameLength)
            {
                rejection =
                    $"Digite pelo menos {MinimumNameLength} caracteres do nome.";
                return false;
            }

            foreach (char character in normalizedQuery)
            {
                if (char.IsLetterOrDigit(character) ||
                    character == ' ' || character == '-' ||
                    character == '_' || character == '.' ||
                    character == '#')
                {
                    continue;
                }

                rejection =
                    "A busca contém um caractere que não pode fazer parte do nome.";
                return false;
            }
            return true;
        }
    }
}
