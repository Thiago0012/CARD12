using System;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        public bool TryGrantStoryRogueliteCoins(
            string operationId,
            int amount,
            out ShopTransactionRecord receipt,
            out string rejection)
        {
            const string kind = "story-roguelite-reward";
            const string source = "Crônicas do Duelo";
            receipt = null;
            rejection = string.Empty;
            if (amount <= 0)
            {
                rejection = "A recompensa de moedas deve ser positiva.";
                return false;
            }
            if (!TryPrepareTransaction(operationId, kind, source,
                    out receipt, out rejection))
                return receipt != null;

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
                State.processedShopTransactions.Add(receipt);
                RecordEligibleMissionCoins(
                    operationId,
                    amount,
                    false,
                    false,
                    true);
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                receipt = null;
                rejection = "A recompensa da jornada não foi gravada: " +
                    exception.GetBaseException().Message;
                return false;
            }
        }
    }
}
