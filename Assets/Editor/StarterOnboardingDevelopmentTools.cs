using System;
using System.IO;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor
{
    public static class StarterOnboardingDevelopmentTools
    {
        [MenuItem("Arcane Arena/Development/Reset Starter Deck Choice")]
        public static void ResetStarterDeckChoice()
        {
            string savePath = Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "decks.json");
            if (!File.Exists(savePath))
            {
                EditorUtility.DisplayDialog(
                    "Deck inicial",
                    "Nenhum save local foi encontrado.",
                    "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog(
                    "Reset de desenvolvimento",
                    "Remover a escolha e a concessao do deck inicial deste perfil? " +
                    "Um backup sera criado antes da alteracao.",
                    "RESETAR",
                    "CANCELAR"))
            {
                return;
            }

            string backup = savePath + ".starter-reset-" +
                DateTime.UtcNow.ToString("yyyyMMddHHmmss") + ".bak";
            File.Copy(savePath, backup, false);

            CardCatalog catalog = AssetDatabase.LoadAssetAtPath<CardCatalog>(
                "Assets/Cards/CardCatalog.asset");
            var repository = new DeckRepository(savePath);
            repository.Load(catalog, false);
            DeckCollectionState state = repository.State;
            string requestId = state.starterClaimTransactionId;
            ShopTransactionRecord receipt = state.processedShopTransactions
                .FirstOrDefault(item => item != null && string.Equals(
                    item.transactionId,
                    requestId,
                    StringComparison.Ordinal));
            if (receipt != null)
            {
                foreach (string cardId in receipt.grantedCardIds)
                {
                    string key = Canonical(cardId);
                    CardQuantityRecord quantity = state.cardQuantities
                        .FirstOrDefault(item => item != null &&
                            Canonical(item.cardId) == key);
                    if (quantity != null)
                        quantity.quantity = Math.Max(0, quantity.quantity - 1);
                }
                state.processedShopTransactions.Remove(receipt);
            }
            state.cardQuantities.RemoveAll(item =>
                item == null || item.quantity <= 0);

            string starterDeckSaveId = "starter:" + state.localProfileId;
            state.decks.RemoveAll(deck => deck != null && string.Equals(
                deck.deckId,
                starterDeckSaveId,
                StringComparison.Ordinal));
            if (string.Equals(
                    state.selectedDeckId,
                    starterDeckSaveId,
                    StringComparison.Ordinal))
            {
                state.selectedDeckId = state.decks.FirstOrDefault()?.deckId ??
                    string.Empty;
            }

            state.starterDeckClaimed = false;
            state.starterDeckId = string.Empty;
            state.starterClaimTransactionId = string.Empty;
            state.starterClaimedAtUtcTicks = 0;
            state.starterCatalogVersion = 0;
            state.banlistVersionAtClaim = string.Empty;
            repository.Save();
            Debug.Log(
                "ARCANE_STARTER_RESET_OK backup=" + backup);
            EditorUtility.DisplayDialog(
                "Deck inicial resetado",
                "O onboarding sera exibido novamente. Backup: " + backup,
                "OK");
        }

        private static string Canonical(string value)
        {
            return uint.TryParse(value, out uint code) && code != 0
                ? code.ToString()
                : value?.Trim() ?? string.Empty;
        }
    }
}
