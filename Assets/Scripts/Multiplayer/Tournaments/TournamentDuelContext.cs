using System;
using ArcaneDuel.Game.Competitive;

namespace ArcaneArena.Multiplayer.Tournaments
{
    [Serializable]
    public sealed class TournamentDuelContext
    {
        public string tournamentId;
        public string lobbyId;
        public string matchId;
        public string roundId;
        public int roundNumber;
        public int bestOf;
        public string playerAId;
        public string playerBId;
        public string localPlayerId;
        public CompetitivePolicy competitivePolicy =
            CompetitivePolicy.Unranked;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(tournamentId) &&
            !string.IsNullOrWhiteSpace(matchId) &&
            !string.IsNullOrWhiteSpace(playerAId) &&
            !string.IsNullOrWhiteSpace(playerBId) &&
            !string.IsNullOrWhiteSpace(localPlayerId);

        public bool LocalPlayerHosts => string.Equals(
            localPlayerId,
            playerAId,
            StringComparison.Ordinal);
    }
}
