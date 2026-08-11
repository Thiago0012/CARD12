using System;
using System.Collections.Generic;

namespace ArcaneDuel.Game.Competitive
{
    public enum RankTier
    {
        Wood = 0,
        Stone = 1,
        Iron = 2,
        Silver = 3,
        Gold = 4,
        Platinum = 5,
        Diamond = 6,
        GrandMaster = 7
    }

    public enum CompetitivePolicy
    {
        Unranked = 0,
        Ranked = 1
    }

    public enum CompetitiveMatchSource
    {
        PrivateRoom = 0,
        Tournament = 1,
        Matchmaking = 2
    }

    public enum RankedOutcome
    {
        NoContest = 0,
        Win = 1,
        Loss = 2,
        Draw = 3,
        ConfirmedAbandonment = 4
    }

    public enum RankReceiptStatus
    {
        Applied = 0,
        AlreadyProcessed = 1,
        NotRanked = 2,
        Invalid = 3,
        StaleSnapshot = 4,
        IncompatibleRules = 5
    }

    [Serializable]
    public sealed class PlayerRankData
    {
        public int rankedPoints;
        public int stateVersion = 1;
        public bool promotionShieldActive;
        public RankTier promotionShieldTier = RankTier.Wood;
        public long updatedUtcTicks;
        public List<RankChangeReceipt> receipts = new List<RankChangeReceipt>();

        public void Normalize()
        {
            rankedPoints = RankRules.ClampPoints(rankedPoints);
            stateVersion = Math.Max(1, stateVersion);
            RankTier current = RankRules.ResolveTier(rankedPoints);
            if (!promotionShieldActive ||
                promotionShieldTier <= RankTier.Wood ||
                promotionShieldTier >= RankTier.GrandMaster ||
                current != promotionShieldTier)
            {
                promotionShieldActive = false;
                promotionShieldTier = RankTier.Wood;
            }
            receipts ??= new List<RankChangeReceipt>();
            receipts.RemoveAll(receipt => receipt == null ||
                string.IsNullOrWhiteSpace(receipt.transactionId));
        }

        public PlayerRankData CopyWithoutReceipts()
        {
            return new PlayerRankData
            {
                rankedPoints = rankedPoints,
                stateVersion = stateVersion,
                promotionShieldActive = promotionShieldActive,
                promotionShieldTier = promotionShieldTier,
                updatedUtcTicks = updatedUtcTicks,
                receipts = new List<RankChangeReceipt>()
            };
        }
    }

    [Serializable]
    public sealed class RankPlayerSnapshot
    {
        public string stablePlayerId;
        public int rankedPoints;
        public RankTier tier;
        public int stateVersion;
        public bool promotionShieldActive;
        public RankTier promotionShieldTier;
        public int rulesVersion;
        public string rulesHash;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(stablePlayerId) &&
            stablePlayerId.Length <= 128 &&
            rankedPoints >= RankRules.MinimumPoints &&
            rankedPoints <= RankRules.MaximumPoints &&
            tier == RankRules.ResolveTier(rankedPoints) &&
            stateVersion > 0 &&
            rulesVersion == RankRules.RulesVersion &&
            string.Equals(rulesHash, RankRules.RulesHash,
                StringComparison.Ordinal);

        public static RankPlayerSnapshot Create(
            string stablePlayerId,
            PlayerRankData data)
        {
            data ??= new PlayerRankData();
            data.Normalize();
            return new RankPlayerSnapshot
            {
                stablePlayerId = stablePlayerId?.Trim() ?? string.Empty,
                rankedPoints = data.rankedPoints,
                tier = RankRules.ResolveTier(data.rankedPoints),
                stateVersion = data.stateVersion,
                promotionShieldActive = data.promotionShieldActive,
                promotionShieldTier = data.promotionShieldTier,
                rulesVersion = RankRules.RulesVersion,
                rulesHash = RankRules.RulesHash
            };
        }
    }

    [Serializable]
    public sealed class RankedMatchSnapshot
    {
        public string matchId;
        public CompetitivePolicy policy;
        public CompetitiveMatchSource source;
        public int rulesVersion;
        public string rulesHash;
        public long sealedAtUtcTicks;
        public RankPlayerSnapshot seat0;
        public RankPlayerSnapshot seat1;

        public bool IsValid
        {
            get
            {
                if (string.IsNullOrWhiteSpace(matchId) || matchId.Length > 128 ||
                    rulesVersion != RankRules.RulesVersion ||
                    !string.Equals(rulesHash, RankRules.RulesHash,
                        StringComparison.Ordinal) ||
                    seat0 == null || seat1 == null ||
                    !seat0.IsValid || !seat1.IsValid)
                {
                    return false;
                }
                return !string.Equals(
                    seat0.stablePlayerId,
                    seat1.stablePlayerId,
                    StringComparison.Ordinal);
            }
        }

        public RankPlayerSnapshot ForSeat(int seat) => seat == 0 ? seat0 : seat1;
        public RankPlayerSnapshot OpponentForSeat(int seat) => seat == 0 ? seat1 : seat0;
    }

    [Serializable]
    public sealed class RankChangeReceipt
    {
        public string transactionId;
        public string matchId;
        public string stablePlayerId;
        public string opponentStablePlayerId;
        public CompetitivePolicy policy;
        public CompetitiveMatchSource source;
        public RankedOutcome outcome;
        public int oldPoints;
        public int opponentPointsAtStart;
        public int newPoints;
        public int delta;
        public RankTier oldTier;
        public RankTier opponentTierAtStart;
        public RankTier newTier;
        public bool promoted;
        public bool demoted;
        public bool shieldWasActive;
        public bool shieldConsumed;
        public bool shieldPreventedDemotion;
        public bool shieldGranted;
        public bool shieldActiveAfter;
        public RankTier shieldTierAfter;
        public bool abandonmentPenaltyApplied;
        public int stateVersionBefore;
        public int stateVersionAfter;
        public int rulesVersion;
        public string rulesHash;
        public long createdUtcTicks;
        public RankReceiptStatus status;

        public RankChangeReceipt CopyWithStatus(RankReceiptStatus nextStatus)
        {
            var copy = (RankChangeReceipt)MemberwiseClone();
            copy.status = nextStatus;
            return copy;
        }
    }

    public readonly struct RankDefinition
    {
        public RankDefinition(
            RankTier tier,
            int minimum,
            int maximum,
            int win,
            int loss)
        {
            Tier = tier;
            Minimum = minimum;
            Maximum = maximum;
            BaseWin = win;
            BaseLoss = loss;
        }

        public RankTier Tier { get; }
        public int Minimum { get; }
        public int Maximum { get; }
        public int BaseWin { get; }
        public int BaseLoss { get; }
    }

    public static class RankRules
    {
        public const int RulesVersion = 1;
        public const string RulesHash =
            "57cb374d97b869911816a8855252837cd630424c46254ddf8c3455b53e08ad33";
        public const int MinimumPoints = 0;
        public const int MaximumPoints = 200;
        public const int PointsPerTier = 25;

        private static readonly RankDefinition[] Definitions =
        {
            new RankDefinition(RankTier.Wood, 0, 24, 7, 0),
            new RankDefinition(RankTier.Stone, 25, 49, 6, -1),
            new RankDefinition(RankTier.Iron, 50, 74, 5, -2),
            new RankDefinition(RankTier.Silver, 75, 99, 5, -3),
            new RankDefinition(RankTier.Gold, 100, 124, 4, -4),
            new RankDefinition(RankTier.Platinum, 125, 149, 3, -4),
            new RankDefinition(RankTier.Diamond, 150, 174, 3, -5),
            new RankDefinition(RankTier.GrandMaster, 175, 200, 2, -6)
        };

        public static int ClampPoints(int points) =>
            Math.Max(MinimumPoints, Math.Min(MaximumPoints, points));

        public static RankTier ResolveTier(int points)
        {
            int clamped = ClampPoints(points);
            int index = Math.Min(
                (int)RankTier.GrandMaster,
                clamped / PointsPerTier);
            return (RankTier)index;
        }

        public static RankDefinition Definition(RankTier tier)
        {
            int index = Math.Max(0, Math.Min(Definitions.Length - 1, (int)tier));
            return Definitions[index];
        }

        public static string DisplayName(RankTier tier)
        {
            return tier switch
            {
                RankTier.Wood => "MADEIRA",
                RankTier.Stone => "PEDRA",
                RankTier.Iron => "FERRO",
                RankTier.Silver => "PRATA",
                RankTier.Gold => "OURO",
                RankTier.Platinum => "PLATINA",
                RankTier.Diamond => "DIAMANTE",
                RankTier.GrandMaster => "GRÃO-MESTRE",
                _ => "MADEIRA"
            };
        }

        public static float TierProgress01(int points)
        {
            int clamped = ClampPoints(points);
            RankDefinition definition = Definition(ResolveTier(clamped));
            if (definition.Tier == RankTier.GrandMaster)
                return clamped >= MaximumPoints ? 1f :
                    (clamped - definition.Minimum) /
                    (float)(definition.Maximum - definition.Minimum);
            return (clamped - definition.Minimum) / (float)PointsPerTier;
        }

        public static int PointsUntilNextTier(int points)
        {
            int clamped = ClampPoints(points);
            RankTier tier = ResolveTier(clamped);
            if (tier == RankTier.GrandMaster)
                return Math.Max(0, MaximumPoints - clamped);
            return Definition(tier).Maximum + 1 - clamped;
        }

        public static bool CanReceivePromotionShield(RankTier tier) =>
            tier >= RankTier.Stone && tier <= RankTier.Diamond;
    }

    public static class RankPointService
    {
        public static string BuildTransactionId(
            string matchId,
            string stablePlayerId)
        {
            return $"rank:{RankRules.RulesVersion}:{matchId}:{stablePlayerId}";
        }

        public static bool TryCreateReceipt(
            RankedMatchSnapshot match,
            int localSeat,
            RankedOutcome outcome,
            out RankChangeReceipt receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            if (match == null || !match.IsValid || localSeat < 0 || localSeat > 1)
            {
                rejection = "Snapshot ranqueado inválido.";
                return false;
            }

            RankPlayerSnapshot local = match.ForSeat(localSeat);
            RankPlayerSnapshot opponent = match.OpponentForSeat(localSeat);
            int oldPoints = RankRules.ClampPoints(local.rankedPoints);
            RankTier oldTier = RankRules.ResolveTier(oldPoints);
            int rawDelta = 0;
            bool abandonment = outcome == RankedOutcome.ConfirmedAbandonment;

            if (match.policy == CompetitivePolicy.Ranked)
            {
                RankDefinition definition = RankRules.Definition(oldTier);
                if (outcome == RankedOutcome.Win)
                {
                    rawDelta = definition.BaseWin;
                    int difference = (int)opponent.tier - (int)oldTier;
                    if (difference > 0)
                        rawDelta += 1;
                    else if (difference <= -2)
                        rawDelta = Math.Max(1, rawDelta - 1);
                }
                else if (outcome == RankedOutcome.Loss || abandonment)
                {
                    int magnitude = Math.Abs(definition.BaseLoss);
                    int difference = (int)opponent.tier - (int)oldTier;
                    if (difference > 0)
                        magnitude = Math.Max(0, magnitude - 1);
                    else if (difference <= -2)
                        magnitude += 1;
                    if (abandonment)
                        magnitude += 1;
                    rawDelta = -magnitude;
                }
            }

            bool shieldWasActive = local.promotionShieldActive &&
                local.promotionShieldTier == oldTier &&
                RankRules.CanReceivePromotionShield(oldTier);
            bool shieldConsumed = false;
            int newPoints = RankRules.ClampPoints(oldPoints + rawDelta);
            bool shieldPreventedDemotion = false;
            if (match.policy == CompetitivePolicy.Ranked &&
                outcome == RankedOutcome.Loss && shieldWasActive)
            {
                shieldConsumed = true;
                shieldPreventedDemotion =
                    newPoints < RankRules.Definition(oldTier).Minimum;
                newPoints = Math.Max(
                    newPoints,
                    RankRules.Definition(oldTier).Minimum);
            }
            else if (match.policy == CompetitivePolicy.Ranked &&
                     abandonment && shieldWasActive)
            {
                // Abandono confirmado limpa o escudo e nunca protege o piso.
                shieldConsumed = true;
            }

            RankTier newTier = RankRules.ResolveTier(newPoints);
            bool promoted = newTier > oldTier;
            bool demoted = newTier < oldTier;
            bool shieldGranted = promoted &&
                RankRules.CanReceivePromotionShield(newTier);
            bool shieldActiveAfter = shieldGranted ||
                shieldWasActive && !shieldConsumed && !demoted;
            RankTier shieldTierAfter = shieldGranted
                ? newTier
                : shieldActiveAfter ? local.promotionShieldTier : RankTier.Wood;

            receipt = new RankChangeReceipt
            {
                transactionId = BuildTransactionId(
                    match.matchId,
                    local.stablePlayerId),
                matchId = match.matchId,
                stablePlayerId = local.stablePlayerId,
                opponentStablePlayerId = opponent.stablePlayerId,
                policy = match.policy,
                source = match.source,
                outcome = outcome,
                oldPoints = oldPoints,
                opponentPointsAtStart = opponent.rankedPoints,
                newPoints = newPoints,
                delta = newPoints - oldPoints,
                oldTier = oldTier,
                opponentTierAtStart = opponent.tier,
                newTier = newTier,
                promoted = promoted,
                demoted = demoted,
                shieldWasActive = shieldWasActive,
                shieldConsumed = shieldConsumed,
                shieldPreventedDemotion = shieldPreventedDemotion,
                shieldGranted = shieldGranted,
                shieldActiveAfter = shieldActiveAfter,
                shieldTierAfter = shieldTierAfter,
                abandonmentPenaltyApplied = abandonment,
                stateVersionBefore = local.stateVersion,
                stateVersionAfter = local.stateVersion +
                    (match.policy == CompetitivePolicy.Ranked ? 1 : 0),
                rulesVersion = RankRules.RulesVersion,
                rulesHash = RankRules.RulesHash,
                createdUtcTicks = DateTime.UtcNow.Ticks,
                status = match.policy == CompetitivePolicy.Ranked
                    ? RankReceiptStatus.Applied
                    : RankReceiptStatus.NotRanked
            };
            return true;
        }

        public static bool SameAuthoritativeChange(
            RankChangeReceipt left,
            RankChangeReceipt right)
        {
            if (left == null || right == null)
                return false;
            return string.Equals(left.transactionId, right.transactionId,
                       StringComparison.Ordinal) &&
                   string.Equals(left.matchId, right.matchId,
                       StringComparison.Ordinal) &&
                   string.Equals(left.stablePlayerId, right.stablePlayerId,
                       StringComparison.Ordinal) &&
                   string.Equals(left.opponentStablePlayerId,
                       right.opponentStablePlayerId, StringComparison.Ordinal) &&
                   left.policy == right.policy &&
                   left.source == right.source &&
                   left.outcome == right.outcome &&
                   left.oldPoints == right.oldPoints &&
                   left.opponentPointsAtStart == right.opponentPointsAtStart &&
                   left.newPoints == right.newPoints &&
                   left.delta == right.delta &&
                   left.oldTier == right.oldTier &&
                   left.opponentTierAtStart == right.opponentTierAtStart &&
                   left.newTier == right.newTier &&
                   left.promoted == right.promoted &&
                   left.demoted == right.demoted &&
                   left.shieldWasActive == right.shieldWasActive &&
                   left.shieldConsumed == right.shieldConsumed &&
                   left.shieldPreventedDemotion ==
                       right.shieldPreventedDemotion &&
                   left.shieldGranted == right.shieldGranted &&
                   left.shieldActiveAfter == right.shieldActiveAfter &&
                   left.shieldTierAfter == right.shieldTierAfter &&
                   left.abandonmentPenaltyApplied ==
                       right.abandonmentPenaltyApplied &&
                   left.stateVersionBefore == right.stateVersionBefore &&
                   left.stateVersionAfter == right.stateVersionAfter &&
                   left.rulesVersion == right.rulesVersion &&
                   string.Equals(left.rulesHash, right.rulesHash,
                       StringComparison.Ordinal);
        }
    }

    public readonly struct RankPresentationModel
    {
        public RankPresentationModel(PlayerRankData data)
        {
            data ??= new PlayerRankData();
            data.Normalize();
            Points = data.rankedPoints;
            Tier = RankRules.ResolveTier(Points);
            NextTier = Tier == RankTier.GrandMaster
                ? RankTier.GrandMaster
                : (RankTier)((int)Tier + 1);
            Progress01 = RankRules.TierProgress01(Points);
            PointsUntilNext = RankRules.PointsUntilNextTier(Points);
            ShieldActive = data.promotionShieldActive;
        }

        public int Points { get; }
        public RankTier Tier { get; }
        public RankTier NextTier { get; }
        public float Progress01 { get; }
        public int PointsUntilNext { get; }
        public bool ShieldActive { get; }
        public bool IsMaximum => Tier == RankTier.GrandMaster &&
                                 Points >= RankRules.MaximumPoints;
    }
}
