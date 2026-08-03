using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        private const string StarterTransactionKind = "starter-deck";
        internal static Action StarterClaimBeforeCommitTestHook;

        public string StarterClaimRequestId => State == null
            ? string.Empty
            : $"starter-claim:{State.localProfileId}";

        public bool TryClaimStarterDeck(
            StarterDeckDefinition definition,
            StarterDeckCatalog starterCatalog,
            out ShopTransactionRecord receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            if (State == null || !HasPlayerProfile)
            {
                rejection = "Conclua primeiro o perfil do duelista.";
                return false;
            }
            if (definition == null || starterCatalog == null)
            {
                rejection = "O catalogo de decks iniciais nao esta disponivel.";
                return false;
            }
            if (!definition.IsPublishable)
            {
                rejection = definition.ValidationIssues.Count > 0
                    ? string.Join(" ", definition.ValidationIssues)
                    : "Este deck inicial ainda aguarda validacao.";
                return false;
            }
            if (!string.Equals(
                    starterCatalog.ActiveBanlistId,
                    BanlistService.ActiveBanlistId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    definition.BanlistVersion,
                    BanlistService.ActiveBanlistId,
                    StringComparison.Ordinal))
            {
                rejection = "O deck inicial foi criado para outra banlist.";
                return false;
            }

            string requestId = StarterClaimRequestId;
            if (State.starterDeckClaimed)
            {
                if (!string.Equals(
                        State.starterDeckId,
                        definition.Id,
                        StringComparison.Ordinal))
                {
                    rejection = "O deck inicial deste perfil ja foi escolhido.";
                    return false;
                }

                receipt = FindTransaction(requestId) ??
                    CreateTransaction(
                        requestId,
                        StarterTransactionKind,
                        definition.Id,
                        0,
                        Array.Empty<string>());
                return true;
            }

            var deck = new DeckRecord
            {
                deckId = $"starter:{State.localProfileId}",
                displayName = definition.DisplayName,
                caseTheme = 0,
                mainDeckCardIds = new List<string>(definition.MainDeck),
                extraDeckCardIds = new List<string>(definition.ExtraDeck),
                sideDeckCardIds = new List<string>()
            };
            deck.Normalize();
            if (!TryValidateForDuel(deck, _catalog, out rejection))
                return false;

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                var granted = deck.mainDeckCardIds
                    .Concat(deck.extraDeckCardIds)
                    .ToList();
                foreach (string cardId in granted)
                    AddCardQuantity(cardId, 1);

                State.decks.RemoveAll(candidate => candidate != null &&
                    string.Equals(candidate.deckId, deck.deckId,
                        StringComparison.Ordinal));
                State.decks.Add(deck);
                State.selectedDeckId = deck.deckId;
                State.starterDeckClaimed = true;
                State.starterDeckId = definition.Id;
                State.starterClaimTransactionId = requestId;
                State.starterClaimedAtUtcTicks = DateTime.UtcNow.Ticks;
                State.starterCatalogVersion = starterCatalog.CatalogVersion;
                State.banlistVersionAtClaim = starterCatalog.ActiveBanlistId;

                receipt = CreateTransaction(
                    requestId,
                    StarterTransactionKind,
                    definition.Id,
                    0,
                    granted);
                State.processedShopTransactions.Add(receipt);
                StarterClaimBeforeCommitTestHook?.Invoke();
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                receipt = null;
                rejection = $"Nao foi possivel entregar o deck inicial: {exception.Message}";
                return false;
            }
        }
    }
}
