using System;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        public bool TryGrantOfflineDuelCoins(
            string operationId,
            int amount,
            long damageDealt,
            long damageReceived,
            int turns,
            bool winner,
            bool draw,
            out ShopTransactionRecord receipt,
            out string rejection)
        {
            const string kind = "offline-duel-reward";
            const string source = "Duelo Offline";
            receipt = null;
            rejection = string.Empty;
            amount = Math.Max(
                OfflineDuelCoinReward.MinimumCoins,
                Math.Min(OfflineDuelCoinReward.MaximumCoins, amount));
            if (!TryPrepareTransaction(
                    operationId,
                    kind,
                    source,
                    out receipt,
                    out rejection))
            {
                return receipt != null;
            }

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                State.coinBalance = checked(State.coinBalance + amount);
                receipt = CreateTransaction(
                    operationId,
                    kind,
                    source,
                    amount,
                    Array.Empty<string>());
                receipt.rewardStatus = RewardReceiptStatus.Granted;
                receipt.matchId = operationId ?? string.Empty;
                receipt.damageDealt = (int)Math.Min(
                    int.MaxValue,
                    Math.Max(0L, damageDealt));
                receipt.completedRounds = Math.Max(0, turns);
                receipt.winner = winner;
                receipt.draw = draw;
                State.processedShopTransactions.Add(receipt);
                RecordEligibleMissionCoins(
                    operationId,
                    amount,
                    false,
                    false,
                    false);
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                receipt = null;
                rejection = "A recompensa offline não foi gravada: " +
                    exception.GetBaseException().Message;
                return false;
            }
        }
    }
}
