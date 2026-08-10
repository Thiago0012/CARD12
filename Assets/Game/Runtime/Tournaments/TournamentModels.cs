using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneDuel.Game.Tournaments
{
    public enum TournamentFormatType
    {
        SingleElimination = 0,
        Points = 1
    }

    public enum TournamentStatus
    {
        Draft = 0,
        Lobby = 1,
        InProgress = 2,
        Completed = 3,
        Cancelled = 4
    }

    public enum TournamentPlayerStatus
    {
        Waiting = 0,
        Ready = 1,
        InDuel = 2,
        Eliminated = 3,
        Offline = 4
    }

    public enum TournamentMatchStatus
    {
        Waiting = 0,
        Ready = 1,
        InProgress = 2,
        Finished = 3,
        Invalid = 4,
        Bye = 5
    }

    public enum TournamentBanListMode
    {
        Standard = 0,
        Custom = 1,
        None = 2
    }

    public enum TournamentCardPoolMode
    {
        AllCards = 0,
        SelectedCardsOnly = 1
    }

    public enum TournamentCardEventType
    {
        Draw = 0,
        SummonNormal = 1,
        SummonSpecial = 2,
        EffectActivated = 3,
        EffectResolved = 4,
        BattleDamageDealt = 5,
        EffectDamageDealt = 6,
        Destroyed = 7,
        SentToGraveyard = 8,
        Banished = 9,
        ReturnedToHand = 10,
        ReturnedToDeck = 11,
        Tributed = 12
    }

    [Serializable]
    public sealed class TournamentCardRestriction
    {
        public string cardId;
        public int maximumCopies = 3;

        public void Normalize()
        {
            cardId = NormalizeCardId(cardId);
            maximumCopies = Math.Max(0, Math.Min(3, maximumCopies));
        }

        internal static string NormalizeCardId(string value)
        {
            return uint.TryParse(value?.Trim(), out uint code) && code != 0
                ? code.ToString("00000000")
                : string.Empty;
        }
    }

    [Serializable]
    public sealed class TournamentConfig
    {
        public string tournamentId;
        public string name = "Torneio Plus Ultra";
        public string description = string.Empty;
        public TournamentFormatType formatType =
            TournamentFormatType.SingleElimination;
        public int participantLimit = 4;
        public int bestOf = 3;
        public int pointsRoundCount = 3;
        public int pointsPerWin = 3;
        public int pointsPerLoss;
        public int pointsPerWalkover = 3;
        public bool deckLocked = true;
        public TournamentCardPoolMode allowedCardPoolMode =
            TournamentCardPoolMode.AllCards;
        public List<string> allowedCardIds = new List<string>();
        public TournamentBanListMode banListMode =
            TournamentBanListMode.Standard;
        public string standardBanListId = string.Empty;
        public List<TournamentCardRestriction> customBanList =
            new List<TournamentCardRestriction>();
        public string visualTheme = "arcane-cyan";
        public string presentationMessage = string.Empty;
        public bool privateRoom = true;
        public string passwordHash = string.Empty;
        public bool allowSpectators;
        public bool allowWalkover = true;
        public bool allowEarlyStart = true;
        public int matchTimeoutMinutes = 45;
        public TournamentStatus status = TournamentStatus.Draft;
        public int bracketSeed;

        public int GamesNeededToWin => bestOf / 2 + 1;

        public void Normalize()
        {
            tournamentId = string.IsNullOrWhiteSpace(tournamentId)
                ? Guid.NewGuid().ToString("N")
                : tournamentId.Trim();
            name = CollapseWhitespace(name);
            description = description?.Trim() ?? string.Empty;
            presentationMessage = presentationMessage?.Trim() ?? string.Empty;
            participantLimit = Math.Max(2, participantLimit);
            bestOf = Math.Max(1, bestOf);
            pointsRoundCount = Math.Max(1, pointsRoundCount);
            pointsPerWin = Math.Max(0, pointsPerWin);
            pointsPerLoss = Math.Max(0, pointsPerLoss);
            pointsPerWalkover = Math.Max(0, pointsPerWalkover);
            matchTimeoutMinutes = Math.Max(5, matchTimeoutMinutes);
            standardBanListId ??= string.Empty;
            passwordHash ??= string.Empty;
            visualTheme = string.IsNullOrWhiteSpace(visualTheme)
                ? "arcane-cyan"
                : visualTheme.Trim();
            allowedCardIds ??= new List<string>();
            allowedCardIds = allowedCardIds
                .Select(TournamentCardRestriction.NormalizeCardId)
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            customBanList ??= new List<TournamentCardRestriction>();
            customBanList.RemoveAll(entry => entry == null);
            foreach (TournamentCardRestriction entry in customBanList)
                entry.Normalize();
            customBanList.RemoveAll(entry =>
                string.IsNullOrWhiteSpace(entry.cardId));
            customBanList = customBanList
                .GroupBy(entry => entry.cardId, StringComparer.Ordinal)
                .Select(group => group.Last())
                .OrderBy(entry => entry.cardId, StringComparer.Ordinal)
                .ToList();
        }

        private static string CollapseWhitespace(string value)
        {
            return string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[])null,
                    StringSplitOptions.RemoveEmptyEntries));
        }
    }

    [Serializable]
    public sealed class TournamentDeckManifest
    {
        public string deckId;
        public string displayName;
        public string banListId;
        public string sha256;
        public List<string> mainDeckCardIds = new List<string>();
        public List<string> extraDeckCardIds = new List<string>();
        public List<string> sideDeckCardIds = new List<string>();

        public void Normalize()
        {
            deckId ??= string.Empty;
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? "Deck sem nome"
                : displayName.Trim();
            banListId ??= string.Empty;
            mainDeckCardIds = NormalizeCards(mainDeckCardIds);
            extraDeckCardIds = NormalizeCards(extraDeckCardIds);
            sideDeckCardIds = NormalizeCards(sideDeckCardIds);
            sha256 ??= string.Empty;
        }

        private static List<string> NormalizeCards(IEnumerable<string> source)
        {
            return (source ?? Array.Empty<string>())
                .Select(TournamentCardRestriction.NormalizeCardId)
                .ToList();
        }
    }

    [Serializable]
    public sealed class TournamentPlayer
    {
        public string playerId;
        public string displayName;
        public string avatarId;
        public string colorTag;
        public string deckId;
        public string deckName;
        public string deckHash;
        public string lockedDeckHash;
        public bool deckValid;
        public string deckValidationMessage;
        public bool isReady;
        public bool isOrganizer;
        public bool isEliminated;
        public bool isOnline = true;
        public TournamentPlayerStatus status = TournamentPlayerStatus.Waiting;
        public int matchesPlayed;
        public int wins;
        public int losses;
        public int seriesWins;
        public int seriesLosses;
        public int gamesWon;
        public int gamesLost;
        public int points;
        public int rankPosition;
        public int currentWinStreak;
        public int longestWinStreak;
        public int currentLossStreak;
        public int longestLossStreak;
        public string statsId;
        public TournamentDeckManifest deck = new TournamentDeckManifest();

        public int GameDifferential => gamesWon - gamesLost;
    }

    [Serializable]
    public sealed class TournamentMatch
    {
        public string matchId;
        public string roundId;
        public int roundNumber;
        public int bracketIndex;
        public string playerAId;
        public string playerBId;
        public int bestOf = 1;
        public int gamesWonByA;
        public int gamesWonByB;
        public string winnerId;
        public string loserId;
        public TournamentMatchStatus status = TournamentMatchStatus.Waiting;
        public long scheduledAtUtcTicks;
        public long startedAtUtcTicks;
        public long finishedAtUtcTicks;
        public string relayRoomCode;
        public string relayHostPlayerId;
        public int updateRevision;
        public List<string> acceptedResultIds = new List<string>();

        public bool HasBothPlayers =>
            !string.IsNullOrWhiteSpace(playerAId) &&
            !string.IsNullOrWhiteSpace(playerBId);

        public bool Contains(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) &&
                (string.Equals(playerAId, playerId, StringComparison.Ordinal) ||
                 string.Equals(playerBId, playerId, StringComparison.Ordinal));
        }
    }

    [Serializable]
    public sealed class TournamentRound
    {
        public string roundId;
        public int roundNumber;
        public string displayName;
        public string byePlayerId;
        public List<string> matchIds = new List<string>();
        public bool completed;
    }

    [Serializable]
    public sealed class TournamentCardEvent
    {
        public string duelId;
        public string playerId;
        public string cardId;
        public TournamentCardEventType eventType;
        public int value;
        public int turnNumber;
        public long timestampUtcTicks;
    }

    [Serializable]
    public sealed class TournamentDuelPlayerStats
    {
        public string playerId;
        public int damageDealt;
        public int damageReceived;
        public int monstersSummoned;
        public int specialSummons;
        public int effectsActivated;
        public int effectsResolved;
        public int cardsDestroyed;
        public int cardsSentToGraveyard;
        public int cardsBanished;
        public int cardsDrawn;
        public int cardsReturnedToHand;
        public int cardsReturnedToDeck;
        public int cardsTributed;
        public bool startedFirst;
    }

    [Serializable]
    public sealed class TournamentCardStats
    {
        public string playerId;
        public string cardId;
        public int timesIncludedInDeck;
        public int timesDrawn;
        public int timesSummoned;
        public int timesActivated;
        public int timesDestroyed;
        public int timesSentToGraveyard;
        public int timesBanished;
        public int timesReturnedToHand;
        public int timesReturnedToDeck;
        public int battleDamage;
        public int effectDamage;
        public int duelsAppeared;
        public int duelsWonWhenUsed;
    }

    [Serializable]
    public sealed class TournamentDuelStatsSnapshot
    {
        public string statsSnapshotId;
        public string duelId;
        public long startedAtUtcTicks;
        public long finishedAtUtcTicks;
        public int turnCount;
        public int capturedEventCount;
        public TournamentDuelPlayerStats playerA =
            new TournamentDuelPlayerStats();
        public TournamentDuelPlayerStats playerB =
            new TournamentDuelPlayerStats();
        public List<TournamentCardStats> perCardStats =
            new List<TournamentCardStats>();
    }

    [Serializable]
    public sealed class TournamentMatchResult
    {
        public string resultId;
        public string tournamentId;
        public string roundId;
        public string matchId;
        public string playerAId;
        public string playerBId;
        public string winnerId;
        public string loserId;
        public int gamesWonByA;
        public int gamesWonByB;
        public long finishedAtUtcTicks;
        public bool surrender;
        public bool timeout;
        public bool walkover;
        public string statsSnapshotId;
        public TournamentDuelStatsSnapshot stats;
    }

    [Serializable]
    public sealed class TournamentPlayerStats
    {
        public string playerId;
        public int duelsPlayed;
        public int duelsWon;
        public int duelsLost;
        public long totalDuelDurationTicks;
        public int damageDealt;
        public int damageReceived;
        public int monstersSummoned;
        public int specialSummons;
        public int effectsActivated;
        public int effectsResolved;
        public int cardsDestroyed;
        public int cardsSentToGraveyard;
        public int cardsBanished;
        public int cardsDrawn;
        public int cardsReturnedToHand;
        public int cardsReturnedToDeck;
        public int cardsTributed;
        public int gamesStartedFirst;
        public int gamesStartedSecond;
        public int winsStartedFirst;
        public int winsStartedSecond;
        public int surrenders;
        public int opponentSurrenders;

        public float WinRate => duelsPlayed <= 0
            ? 0f
            : duelsWon * 100f / duelsPlayed;
    }

    [Serializable]
    public sealed class TournamentMatchStats
    {
        public string resultId;
        public string matchId;
        public string winnerId;
        public string loserId;
        public long durationTicks;
        public int turns;
        public int winnerDamage;
        public int loserDamage;
        public string highestDamageCardId;
    }

    [Serializable]
    public sealed class TournamentGlobalStats
    {
        public int totalParticipants;
        public int totalMatches;
        public int totalDuels;
        public long totalDurationTicks;
        public string championId;
        public string runnerUpId;
        public string thirdPlaceId;
        public string mostUsedCardId;
        public string mostBanishedCardId;
        public string highestDamageCardId;
        public string mvpPlayerId;
        public string mvpCardId;
    }

    [Serializable]
    public sealed class TournamentStats
    {
        public string tournamentId;
        public List<TournamentPlayerStats> perPlayerStats =
            new List<TournamentPlayerStats>();
        public List<TournamentCardStats> perCardStats =
            new List<TournamentCardStats>();
        public List<TournamentMatchStats> perMatchStats =
            new List<TournamentMatchStats>();
        public TournamentGlobalStats globalStats =
            new TournamentGlobalStats();
    }

    [Serializable]
    public sealed class TournamentState
    {
        public int schemaVersion = 1;
        public TournamentConfig config = new TournamentConfig();
        public string organizerPlayerId;
        public string lobbyId;
        public string lobbyCode;
        public int revision;
        public int currentRoundNumber;
        public long createdAtUtcTicks;
        public long startedAtUtcTicks;
        public long finishedAtUtcTicks;
        public long lastSavedAtUtcTicks;
        public string championPlayerId;
        public List<string> podiumPlayerIds = new List<string>();
        public List<TournamentPlayer> players = new List<TournamentPlayer>();
        public List<TournamentRound> rounds = new List<TournamentRound>();
        public List<TournamentMatch> matches = new List<TournamentMatch>();
        public List<TournamentMatchResult> results =
            new List<TournamentMatchResult>();
        public TournamentStats stats = new TournamentStats();

        public TournamentPlayer FindPlayer(string playerId)
        {
            return players?.Find(player =>
                player != null && string.Equals(
                    player.playerId,
                    playerId,
                    StringComparison.Ordinal));
        }

        public TournamentMatch FindMatch(string matchId)
        {
            return matches?.Find(match =>
                match != null && string.Equals(
                    match.matchId,
                    matchId,
                    StringComparison.Ordinal));
        }

        public TournamentRound FindRound(string roundId)
        {
            return rounds?.Find(round =>
                round != null && string.Equals(
                    round.roundId,
                    roundId,
                    StringComparison.Ordinal));
        }

        public TournamentPlayerStats FindPlayerStats(string playerId)
        {
            return stats?.perPlayerStats?.Find(item =>
                item != null && string.Equals(
                    item.playerId,
                    playerId,
                    StringComparison.Ordinal));
        }
    }

    public sealed class TournamentOperationResult
    {
        private TournamentOperationResult(bool success, string message)
        {
            Success = success;
            Message = message ?? string.Empty;
        }

        public bool Success { get; }
        public string Message { get; }

        public static TournamentOperationResult Ok(string message = "")
        {
            return new TournamentOperationResult(true, message);
        }

        public static TournamentOperationResult Fail(string message)
        {
            return new TournamentOperationResult(false, message);
        }
    }
}
