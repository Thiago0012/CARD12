using System;

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

    [Serializable]
    public sealed class FriendProfileView
    {
        public string playerId;
        public string publicId;
        public string displayName;
        public string unityPlayerName;
        public string equippedIconId;
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
                connectionState = FriendConnectionState.None,
                presence = online
                    ? FriendPresenceState.Online
                    : FriendPresenceState.Offline
            };
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
