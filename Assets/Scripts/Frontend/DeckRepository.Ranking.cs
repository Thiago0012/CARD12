using System;
using System.Linq;
using ArcaneDuel.Game.Competitive;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        private const int MaximumRankReceipts = 256;

        public PlayerRankData RankData => State?.rankData;

        private void NormalizeRankState(int loadedSchemaVersion)
        {
            if (State == null)
                return;

            State.rankData ??= new PlayerRankData();
            if (loadedSchemaVersion < 7)
            {
                // Migração não destrutiva: perfis anteriores começam em
                // Madeira, sem tocar em decks, cartas, moedas ou identidade.
                State.rankData = new PlayerRankData();
            }
            State.rankData.Normalize();
            TrimRankReceipts(State.rankData);
        }

        public RankPlayerSnapshot CaptureRankSnapshot()
        {
            if (State == null)
                return null;
            State.rankData ??= new PlayerRankData();
            State.rankData.Normalize();
            return RankPlayerSnapshot.Create(
                State.localProfileId,
                State.rankData);
        }

        public RankPresentationModel GetRankPresentation()
        {
            return new RankPresentationModel(
                State?.rankData ?? new PlayerRankData());
        }

        public RankChangeReceipt FindRankReceipt(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
                return null;
            return State?.rankData?.receipts?.LastOrDefault(receipt =>
                receipt != null && string.Equals(
                    receipt.transactionId,
                    transactionId,
                    StringComparison.Ordinal));
        }

        public bool TryCommitRankReceipt(
            RankChangeReceipt proposed,
            out RankChangeReceipt receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            if (State == null || State.rankData == null)
            {
                rejection = "O perfil ranqueado ainda não foi carregado.";
                return false;
            }
            if (proposed == null ||
                string.IsNullOrWhiteSpace(proposed.transactionId))
            {
                rejection = "O recibo ranqueado é inválido.";
                return false;
            }

            RankChangeReceipt existing = FindRankReceipt(
                proposed.transactionId);
            if (existing != null)
            {
                if (!RankPointService.SameAuthoritativeChange(existing, proposed))
                {
                    rejection = "A transação ranqueada já existe com outro conteúdo.";
                    return false;
                }
                receipt = existing.CopyWithStatus(
                    RankReceiptStatus.AlreadyProcessed);
                return true;
            }

            if (proposed.policy != CompetitivePolicy.Ranked)
            {
                receipt = proposed.CopyWithStatus(RankReceiptStatus.NotRanked);
                return true;
            }
            if (proposed.rulesVersion != RankRules.RulesVersion ||
                !string.Equals(proposed.rulesHash, RankRules.RulesHash,
                    StringComparison.Ordinal))
            {
                rejection = "As regras ranqueadas da partida são incompatíveis.";
                return false;
            }
            if (!string.Equals(
                    proposed.stablePlayerId,
                    State.localProfileId,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(proposed.opponentStablePlayerId) ||
                string.Equals(proposed.stablePlayerId,
                    proposed.opponentStablePlayerId,
                    StringComparison.Ordinal))
            {
                rejection = "O recibo ranqueado pertence a outro perfil.";
                return false;
            }

            PlayerRankData current = State.rankData;
            current.Normalize();
            if (proposed.oldPoints != current.rankedPoints ||
                proposed.stateVersionBefore != current.stateVersion)
            {
                rejection = "O snapshot ranqueado ficou obsoleto; os pontos não foram alterados.";
                return false;
            }
            string expectedTransaction = RankPointService.BuildTransactionId(
                proposed.matchId,
                State.localProfileId);
            if (!string.Equals(
                    proposed.transactionId,
                    expectedTransaction,
                    StringComparison.Ordinal))
            {
                rejection = "A chave idempotente do recibo ranqueado é inválida.";
                return false;
            }

            var verificationSnapshot = new RankedMatchSnapshot
            {
                matchId = proposed.matchId,
                policy = proposed.policy,
                source = proposed.source,
                rulesVersion = RankRules.RulesVersion,
                rulesHash = RankRules.RulesHash,
                sealedAtUtcTicks = proposed.createdUtcTicks,
                seat0 = new RankPlayerSnapshot
                {
                    stablePlayerId = proposed.stablePlayerId,
                    rankedPoints = proposed.oldPoints,
                    tier = RankRules.ResolveTier(proposed.oldPoints),
                    stateVersion = proposed.stateVersionBefore,
                    promotionShieldActive = current.promotionShieldActive,
                    promotionShieldTier = current.promotionShieldTier,
                    rulesVersion = RankRules.RulesVersion,
                    rulesHash = RankRules.RulesHash
                },
                seat1 = new RankPlayerSnapshot
                {
                    stablePlayerId = proposed.opponentStablePlayerId,
                    rankedPoints = RankRules.ClampPoints(
                        proposed.opponentPointsAtStart),
                    tier = RankRules.ResolveTier(
                        proposed.opponentPointsAtStart),
                    stateVersion = 1,
                    promotionShieldActive = false,
                    promotionShieldTier = RankTier.Wood,
                    rulesVersion = RankRules.RulesVersion,
                    rulesHash = RankRules.RulesHash
                }
            };
            if (!RankPointService.TryCreateReceipt(
                    verificationSnapshot,
                    0,
                    proposed.outcome,
                    out RankChangeReceipt canonical,
                    out rejection) ||
                !RankPointService.SameAuthoritativeChange(canonical, proposed))
            {
                rejection = string.IsNullOrWhiteSpace(rejection)
                    ? "O cálculo do recibo ranqueado não confere com as regras locais."
                    : rejection;
                return false;
            }

            string stateSnapshot = JsonUtility.ToJson(State);
            try
            {
                current.rankedPoints = proposed.newPoints;
                current.stateVersion = proposed.stateVersionAfter;
                current.promotionShieldActive = proposed.shieldActiveAfter;
                current.promotionShieldTier = proposed.shieldTierAfter;
                current.updatedUtcTicks = DateTime.UtcNow.Ticks;
                current.receipts.Add(proposed.CopyWithStatus(
                    RankReceiptStatus.Applied));
                TrimRankReceipts(current);
                Save();
                receipt = current.receipts[current.receipts.Count - 1];
                return true;
            }
            catch (Exception exception)
            {
                State = JsonUtility.FromJson<DeckCollectionState>(stateSnapshot);
                rejection = "Os pontos ranqueados não foram gravados: " +
                    exception.GetBaseException().Message;
                return false;
            }
        }

        private static void TrimRankReceipts(PlayerRankData data)
        {
            if (data?.receipts == null)
                return;
            int excess = data.receipts.Count - MaximumRankReceipts;
            if (excess > 0)
                data.receipts.RemoveRange(0, excess);
        }
    }
}
