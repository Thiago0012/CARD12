using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        public int CraftPointBalance(CardRarity rarity)
        {
            PlayerCraftWallet wallet = State?.craftPoints;
            if (wallet == null)
                return 0;
            return rarity switch
            {
                CardRarity.N => Math.Max(0, wallet.cpN),
                CardRarity.R => Math.Max(0, wallet.cpR),
                CardRarity.SR => Math.Max(0, wallet.cpSR),
                CardRarity.UR => Math.Max(0, wallet.cpUR),
                _ => 0
            };
        }

        public int ProtectedCardQuantity(string cardId)
        {
            string normalized = FrontendCardIdentity.NormalizeOfficialId(cardId);
            if (string.IsNullOrWhiteSpace(normalized) ||
                State?.protectedCardQuantities == null)
            {
                return 0;
            }
            ProtectedCardQuantityRecord record =
                State.protectedCardQuantities.FirstOrDefault(item =>
                    item != null && string.Equals(
                        FrontendCardIdentity.NormalizeOfficialId(item.cardId),
                        normalized,
                        StringComparison.Ordinal));
            return Math.Max(0, record?.quantity ?? 0);
        }

        public int DismantlableCardQuantity(string cardId)
        {
            string normalized = FrontendCardIdentity.NormalizeOfficialId(cardId);
            if (string.IsNullOrWhiteSpace(normalized) ||
                State?.cardQuantities == null)
            {
                return 0;
            }
            CardQuantityRecord record = State.cardQuantities.FirstOrDefault(item =>
                item != null && string.Equals(
                    FrontendCardIdentity.NormalizeOfficialId(item.cardId),
                    normalized,
                    StringComparison.Ordinal));
            int explicitQuantity = Math.Max(0, record?.quantity ?? 0);
            return Math.Max(0, explicitQuantity - ProtectedCardQuantity(normalized));
        }

        public IReadOnlyList<string> DecksAffectedByDismantle(
            string cardId,
            int quantity)
        {
            string normalized = FrontendCardIdentity.NormalizeOfficialId(cardId);
            if (string.IsNullOrWhiteSpace(normalized) || quantity <= 0 ||
                State?.decks == null)
            {
                return Array.Empty<string>();
            }
            int remaining = Math.Max(0, OwnedCardQuantity(normalized) - quantity);
            var affected = new List<string>();
            foreach (DeckRecord deck in State.decks)
            {
                if (deck == null)
                    continue;
                int used = deck.mainDeckCardIds
                    .Concat(deck.extraDeckCardIds)
                    .Concat(deck.sideDeckCardIds)
                    .Count(id => string.Equals(
                        FrontendCardIdentity.NormalizeOfficialId(id),
                        normalized,
                        StringComparison.Ordinal));
                if (used > remaining)
                    affected.Add(deck.displayName ?? "Deck sem nome");
            }
            return affected;
        }

        public bool TryGenerateCard(
            string cardId,
            int quantity,
            string transactionId,
            out CraftOperationResult result,
            out string rejection)
        {
            result = null;
            rejection = string.Empty;
            if (!TryResolveCraftCard(
                    cardId,
                    transactionId,
                    "generate",
                    quantity,
                    out CardCatalogEntry entry,
                    out CraftTransactionRecord existing,
                    out rejection))
            {
                result = ResultFor(existing);
                return existing != null;
            }
            if (!entry.IsCraftable)
            {
                rejection = "Esta carta não pode ser gerada.";
                return false;
            }
            int unitCost = CardRarityCatalog.GenerateCost(entry.Rarity);
            int totalCost = checked(unitCost * quantity);
            int before = CraftPointBalance(entry.Rarity);
            if (before < totalCost)
            {
                rejection = $"CP {entry.Rarity} insuficiente: faltam " +
                    $"{totalCost - before}.";
                return false;
            }

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                ChangeCraftPoints(entry.Rarity, -totalCost);
                AddCardQuantity(cardId, quantity);
                int after = CraftPointBalance(entry.Rarity);
                CraftTransactionRecord transaction = CreateCraftTransaction(
                    transactionId,
                    "generate",
                    cardId,
                    entry.Rarity,
                    CardFinish.Normal,
                    quantity,
                    -totalCost,
                    after);
                State.craftTransactions.Add(transaction);
                Save();
                result = new CraftOperationResult(
                    transaction,
                    before,
                    after,
                    Array.Empty<string>());
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                result = null;
                rejection = "A carta não foi gerada; inventário e CP foram restaurados: " +
                    exception.GetBaseException().Message;
                return false;
            }
        }

        public bool TryDismantleCard(
            string cardId,
            int quantity,
            CardFinish finish,
            string transactionId,
            bool deckImpactConfirmed,
            out CraftOperationResult result,
            out string rejection)
        {
            result = null;
            rejection = string.Empty;
            if (!TryResolveCraftCard(
                    cardId,
                    transactionId,
                    "dismantle",
                    quantity,
                    out CardCatalogEntry entry,
                    out CraftTransactionRecord existing,
                    out rejection))
            {
                if (existing != null && existing.finish != finish)
                {
                    result = null;
                    rejection =
                        "O ID de transação já foi usado com outro acabamento.";
                    return false;
                }
                result = ResultFor(existing);
                return existing != null;
            }
            if (!entry.IsDismantlable)
            {
                rejection = "Esta carta não pode ser desmantelada.";
                return false;
            }
            if (finish != CardFinish.Normal)
            {
                rejection =
                    "Este perfil ainda não registra acabamentos Glossy ou Royal.";
                return false;
            }
            int eligible = DismantlableCardQuantity(cardId);
            if (eligible < quantity)
            {
                int protectedQuantity = ProtectedCardQuantity(cardId);
                rejection = protectedQuantity > 0
                    ? "As cópias vindas de Deck Estrutural são protegidas e não podem ser desmanteladas."
                    : "Não há cópia elegível para desmantelar.";
                return false;
            }
            IReadOnlyList<string> affected = DecksAffectedByDismantle(
                cardId,
                quantity);
            if (affected.Count > 0 && !deckImpactConfirmed)
            {
                rejection = "A desmontagem afetará: " +
                    string.Join(", ", affected) + ". Confirme o aviso para continuar.";
                return false;
            }

            int unitReturn = CardRarityCatalog.DismantleReturn(
                entry.Rarity,
                finish);
            int totalReturn = checked(unitReturn * quantity);
            int before = CraftPointBalance(entry.Rarity);
            string snapshot = JsonUtility.ToJson(State);
            try
            {
                if (!RemoveCardQuantity(cardId, quantity))
                    throw new InvalidOperationException(
                        "A quantidade elegível mudou antes da confirmação.");
                ChangeCraftPoints(entry.Rarity, totalReturn);
                int after = CraftPointBalance(entry.Rarity);
                CraftTransactionRecord transaction = CreateCraftTransaction(
                    transactionId,
                    "dismantle",
                    cardId,
                    entry.Rarity,
                    finish,
                    quantity,
                    totalReturn,
                    after);
                State.craftTransactions.Add(transaction);
                Save();
                result = new CraftOperationResult(
                    transaction,
                    before,
                    after,
                    affected);
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                result = null;
                rejection =
                    "A desmontagem não foi concluída; inventário e CP foram restaurados: " +
                    exception.GetBaseException().Message;
                return false;
            }
        }

        private bool TryResolveCraftCard(
            string cardId,
            string transactionId,
            string operation,
            int quantity,
            out CardCatalogEntry entry,
            out CraftTransactionRecord existing,
            out string rejection)
        {
            entry = null;
            existing = null;
            rejection = string.Empty;
            if (State == null || _catalog == null)
            {
                rejection = "O perfil ou catálogo de cartas ainda não foi carregado.";
                return false;
            }
            if (quantity <= 0)
            {
                rejection = "A quantidade deve ser positiva.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                rejection = "A operação exige um ID de transação.";
                return false;
            }
            existing = State.craftTransactions.FirstOrDefault(transaction =>
                transaction != null && string.Equals(
                    transaction.transactionId,
                    transactionId,
                    StringComparison.Ordinal));
            if (existing != null)
            {
                string normalized = FrontendCardIdentity.NormalizeOfficialId(cardId);
                if (string.Equals(existing.operation, operation, StringComparison.Ordinal) &&
                    string.Equals(existing.cardId, normalized, StringComparison.Ordinal) &&
                    existing.quantity == quantity)
                {
                    return false;
                }
                rejection = "O ID de transação já foi usado em outra operação.";
                existing = null;
                return false;
            }
            entry = ResolveCard(_catalog, cardId);
            if (entry == null || !CardRarityCatalog.IsValid(entry.Rarity))
            {
                rejection = "A carta não possui raridade válida no catálogo do Master Duel.";
                return false;
            }
            return true;
        }

        private void ChangeCraftPoints(CardRarity rarity, int delta)
        {
            State.craftPoints ??= new PlayerCraftWallet();
            int current = CraftPointBalance(rarity);
            int changed = checked(current + delta);
            if (changed < 0)
                throw new InvalidOperationException("O saldo de CP não pode ficar negativo.");
            switch (rarity)
            {
                case CardRarity.N: State.craftPoints.cpN = changed; break;
                case CardRarity.R: State.craftPoints.cpR = changed; break;
                case CardRarity.SR: State.craftPoints.cpSR = changed; break;
                case CardRarity.UR: State.craftPoints.cpUR = changed; break;
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }
        }

        private static CraftTransactionRecord CreateCraftTransaction(
            string transactionId,
            string operation,
            string cardId,
            CardRarity rarity,
            CardFinish finish,
            int quantity,
            int cpDelta,
            int balanceAfter)
        {
            return new CraftTransactionRecord
            {
                transactionId = transactionId,
                operation = operation,
                cardId = FrontendCardIdentity.NormalizeOfficialId(cardId),
                rarity = rarity,
                finish = finish,
                quantity = quantity,
                cpDelta = cpDelta,
                balanceAfter = balanceAfter,
                createdUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        private static CraftOperationResult ResultFor(
            CraftTransactionRecord transaction)
        {
            if (transaction == null)
                return null;
            int before = transaction.balanceAfter - transaction.cpDelta;
            return new CraftOperationResult(
                transaction,
                before,
                transaction.balanceAfter,
                Array.Empty<string>());
        }
    }

    public sealed class CraftOperationResult
    {
        public CraftTransactionRecord Transaction { get; }
        public int BalanceBefore { get; }
        public int BalanceAfter { get; }
        public IReadOnlyList<string> AffectedDecks { get; }

        internal CraftOperationResult(
            CraftTransactionRecord transaction,
            int balanceBefore,
            int balanceAfter,
            IReadOnlyList<string> affectedDecks)
        {
            Transaction = transaction;
            BalanceBefore = balanceBefore;
            BalanceAfter = balanceAfter;
            AffectedDecks = affectedDecks ?? Array.Empty<string>();
        }
    }
}
