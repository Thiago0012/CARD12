using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        public int CoinBalance => Math.Max(0, State?.coinBalance ?? 0);

        public PendingPackOpeningRecord PendingPackOpening =>
            State?.pendingPackOpenings?.FirstOrDefault(opening =>
                opening != null && opening.cardIds != null &&
                opening.revealed != null &&
                opening.cardIds.Count == opening.revealed.Count);

        private void NormalizeEconomyState(int loadedSchemaVersion)
        {
            State.coinBalance = Math.Max(0, State.coinBalance);
            State.cardQuantities ??= new List<CardQuantityRecord>();
            State.structureDeckPurchases ??=
                new List<StructureDeckPurchaseRecord>();
            State.pendingPackOpenings ??=
                new List<PendingPackOpeningRecord>();
            State.processedShopTransactions ??=
                new List<ShopTransactionRecord>();

            var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (CardQuantityRecord record in State.cardQuantities)
            {
                if (record == null)
                    continue;
                string cardId = FrontendCardIdentity.NormalizeOfficialId(record.cardId);
                if (string.IsNullOrWhiteSpace(cardId))
                    continue;
                quantities.TryGetValue(cardId, out int current);
                quantities[cardId] = Math.Max(0, current + Math.Max(0, record.quantity));
            }
            State.cardQuantities = quantities
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new CardQuantityRecord
                {
                    cardId = pair.Key,
                    quantity = pair.Value
                })
                .ToList();

            State.structureDeckPurchases.RemoveAll(record =>
                record == null || string.IsNullOrWhiteSpace(record.productId));
            foreach (StructureDeckPurchaseRecord record in
                     State.structureDeckPurchases)
            {
                record.purchaseCount = Math.Max(0, record.purchaseCount);
            }

            State.pendingPackOpenings.RemoveAll(opening =>
                opening == null || string.IsNullOrWhiteSpace(opening.transactionId) ||
                ShopPackCatalog.Find(opening.packId) == null);
            foreach (PendingPackOpeningRecord opening in State.pendingPackOpenings)
            {
                opening.cardIds ??= new List<string>();
                opening.revealed ??= new List<bool>();
                while (opening.revealed.Count < opening.cardIds.Count)
                    opening.revealed.Add(false);
                if (opening.revealed.Count > opening.cardIds.Count)
                {
                    opening.revealed.RemoveRange(
                        opening.cardIds.Count,
                        opening.revealed.Count - opening.cardIds.Count);
                }
            }

            State.processedShopTransactions.RemoveAll(transaction =>
                transaction == null ||
                string.IsNullOrWhiteSpace(transaction.transactionId));
            foreach (ShopTransactionRecord transaction in
                     State.processedShopTransactions)
            {
                transaction.grantedCardIds ??= new List<string>();
                transaction.balanceAfter = Math.Max(0, transaction.balanceAfter);
            }

            if (loadedSchemaVersion >= 4)
                return;

            // Migração não destrutiva: produtos obtidos na loja antiga eram
            // gratuitos. As cartas já recebidas continuam na coleção e passam
            // a ter quantidades explícitas, sem conceder moedas retroativas.
            foreach (string productId in State.unlockedDeckProductIds.ToArray())
            {
                DeckShopProduct product = DeckShopCatalog.Find(productId);
                if (product == null)
                    continue;
                foreach (string cardId in product.MainDeckCardIds.Concat(
                             product.ExtraDeckCardIds))
                {
                    AddCardQuantity(cardId, 1);
                }
                StructureDeckPurchaseRecord purchase =
                    FindStructurePurchase(productId, true);
                purchase.purchaseCount = Math.Max(1, purchase.purchaseCount);
            }
        }

        public int OwnedCardQuantity(string cardId)
        {
            return DeckShopCatalog.OwnedCopies(State, cardId);
        }

        public int StructureDeckPurchaseCount(string productId)
        {
            return FindStructurePurchase(productId, false)?.purchaseCount ?? 0;
        }

        public bool TryPurchaseStructureDeck(
            string productId,
            string transactionId,
            out ShopTransactionRecord receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            if (!TryPrepareTransaction(
                    transactionId,
                    "structure-deck",
                    productId,
                    out receipt,
                    out rejection))
            {
                return receipt != null;
            }

            DeckShopProduct product = DeckShopCatalog.Find(productId);
            if (product == null)
            {
                rejection = "Esse Deck Estrutural não existe no catálogo.";
                return false;
            }
            if (product.PriceCoins <= 0 || product.PreviewCardIds.Count != 3)
            {
                rejection = "O Deck Estrutural está com preço ou destaques inválidos.";
                return false;
            }
            int purchaseCount = StructureDeckPurchaseCount(productId);
            if (purchaseCount >= product.MaxPurchases)
            {
                rejection = "O limite de compras deste Deck Estrutural foi atingido.";
                return false;
            }
            if (CoinBalance < product.PriceCoins)
            {
                rejection = $"Saldo insuficiente: faltam {product.PriceCoins - CoinBalance} moedas.";
                return false;
            }

            DeckRecord deck = product.CreateDeckRecord();
            if (!TryValidateForDuel(deck, _catalog, out rejection))
                return false;

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                State.coinBalance -= product.PriceCoins;
                var granted = new List<string>();
                foreach (string cardId in product.MainDeckCardIds.Concat(
                             product.ExtraDeckCardIds))
                {
                    AddCardQuantity(cardId, 1);
                    granted.Add(cardId);
                }

                StructureDeckPurchaseRecord purchase =
                    FindStructurePurchase(productId, true);
                purchase.purchaseCount++;
                if (!State.unlockedDeckProductIds.Contains(productId))
                    State.unlockedDeckProductIds.Add(productId);
                if (!State.decks.Any(candidate => candidate != null &&
                        string.Equals(candidate.deckId, deck.deckId,
                            StringComparison.Ordinal)))
                {
                    State.decks.Add(deck);
                }

                receipt = CreateTransaction(
                    transactionId,
                    "structure-deck",
                    productId,
                    -product.PriceCoins,
                    granted);
                State.processedShopTransactions.Add(receipt);
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                rejection = "A compra não foi concluída e nenhuma moeda foi gasta: " +
                    exception.GetBaseException().Message;
                receipt = null;
                return false;
            }
        }

        public bool TryPurchasePack(
            string packId,
            string transactionId,
            out PendingPackOpeningRecord opening,
            out ShopTransactionRecord receipt,
            out string rejection)
        {
            opening = null;
            receipt = null;
            rejection = string.Empty;
            if (!TryPrepareTransaction(
                    transactionId,
                    "pack",
                    packId,
                    out receipt,
                    out rejection))
            {
                if (receipt != null)
                {
                    opening = State.pendingPackOpenings.FirstOrDefault(item =>
                        item != null && string.Equals(item.transactionId,
                            transactionId, StringComparison.Ordinal));
                    return true;
                }
                return false;
            }

            ShopPackDefinition pack = ShopPackCatalog.Find(packId);
            if (pack == null || pack.CardIds.Count < 1 || pack.CardIds.Count > 38)
            {
                rejection = "Esse pacote está com um catálogo inválido.";
                return false;
            }
            if (_catalog == null)
            {
                rejection = "O catálogo de cartas ainda não foi carregado.";
                return false;
            }
            foreach (string cardId in pack.CardIds)
            {
                if (ResolveCard(_catalog, cardId) != null)
                    continue;
                rejection = $"O pacote referencia a carta inexistente {cardId}.";
                return false;
            }
            if (CoinBalance < ShopPackCatalog.PackPriceCoins)
            {
                rejection = $"Saldo insuficiente: faltam " +
                    $"{ShopPackCatalog.PackPriceCoins - CoinBalance} moedas.";
                return false;
            }

            var draws = new List<string>(ShopPackCatalog.CardsPerOpening);
            for (int index = 0; index < ShopPackCatalog.CardsPerOpening; index++)
                draws.Add(pack.CardIds[SecureIndex(pack.CardIds.Count)]);

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                State.coinBalance -= ShopPackCatalog.PackPriceCoins;
                foreach (string cardId in draws)
                    AddCardQuantity(cardId, 1);

                opening = new PendingPackOpeningRecord
                {
                    transactionId = transactionId,
                    packId = packId,
                    cardIds = new List<string>(draws),
                    revealed = Enumerable.Repeat(false, draws.Count).ToList()
                };
                State.pendingPackOpenings.Add(opening);
                receipt = CreateTransaction(
                    transactionId,
                    "pack",
                    packId,
                    -ShopPackCatalog.PackPriceCoins,
                    draws);
                State.processedShopTransactions.Add(receipt);
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                rejection = "A compra não foi concluída e nenhuma moeda foi gasta: " +
                    exception.GetBaseException().Message;
                opening = null;
                receipt = null;
                return false;
            }
        }

        public bool TryRevealPackCard(
            string transactionId,
            int index,
            out string rejection)
        {
            rejection = string.Empty;
            PendingPackOpeningRecord opening = State?.pendingPackOpenings?
                .FirstOrDefault(item => item != null && string.Equals(
                    item.transactionId, transactionId, StringComparison.Ordinal));
            if (opening == null || opening.cardIds == null ||
                opening.revealed == null || index < 0 ||
                index >= opening.cardIds.Count || index >= opening.revealed.Count)
            {
                rejection = "Essa abertura de pacote não está mais pendente.";
                return false;
            }
            if (opening.revealed[index])
                return true;

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                opening.revealed[index] = true;
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                rejection = exception.GetBaseException().Message;
                return false;
            }
        }

        public bool TryCompletePackOpening(
            string transactionId,
            out string rejection)
        {
            rejection = string.Empty;
            PendingPackOpeningRecord opening = State?.pendingPackOpenings?
                .FirstOrDefault(item => item != null && string.Equals(
                    item.transactionId, transactionId, StringComparison.Ordinal));
            if (opening == null)
                return true;
            if (opening.revealed == null || opening.revealed.Any(value => !value))
            {
                rejection = "Revele as cinco cartas antes de concluir.";
                return false;
            }

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                State.pendingPackOpenings.Remove(opening);
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                rejection = exception.GetBaseException().Message;
                return false;
            }
        }

        private bool TryPrepareTransaction(
            string transactionId,
            string kind,
            string productId,
            out ShopTransactionRecord existing,
            out string rejection)
        {
            existing = null;
            rejection = string.Empty;
            if (State == null)
            {
                rejection = "O perfil local ainda não foi carregado.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                rejection = "A compra exige um ID de transação.";
                return false;
            }
            existing = FindTransaction(transactionId);
            if (existing == null)
                return true;
            if (!string.Equals(existing.kind, kind, StringComparison.Ordinal) ||
                !string.Equals(existing.productId ?? string.Empty,
                    productId ?? string.Empty, StringComparison.Ordinal))
            {
                rejection = "O ID da transação já foi usado em outra compra.";
                existing = null;
            }
            return false;
        }

        private ShopTransactionRecord FindTransaction(string transactionId)
        {
            if (string.IsNullOrWhiteSpace(transactionId) ||
                State?.processedShopTransactions == null)
                return null;
            return State.processedShopTransactions.FirstOrDefault(transaction =>
                transaction != null && string.Equals(transaction.transactionId,
                    transactionId, StringComparison.Ordinal));
        }

        private StructureDeckPurchaseRecord FindStructurePurchase(
            string productId,
            bool create)
        {
            StructureDeckPurchaseRecord record = State.structureDeckPurchases
                .FirstOrDefault(item => item != null && string.Equals(
                    item.productId, productId, StringComparison.Ordinal));
            if (record == null && create)
            {
                record = new StructureDeckPurchaseRecord
                {
                    productId = productId,
                    purchaseCount = 0
                };
                State.structureDeckPurchases.Add(record);
            }
            return record;
        }

        private void AddCardQuantity(string cardId, int amount)
        {
            string normalized = FrontendCardIdentity.NormalizeOfficialId(cardId);
            if (string.IsNullOrWhiteSpace(normalized) || amount <= 0)
                return;
            CardQuantityRecord record = State.cardQuantities.FirstOrDefault(item =>
                item != null && string.Equals(
                    FrontendCardIdentity.NormalizeOfficialId(item.cardId),
                    normalized,
                    StringComparison.Ordinal));
            if (record == null)
            {
                record = new CardQuantityRecord { cardId = normalized };
                State.cardQuantities.Add(record);
            }
            record.quantity = checked(Math.Max(0, record.quantity) + amount);
        }

        private ShopTransactionRecord CreateTransaction(
            string transactionId,
            string kind,
            string productId,
            int coinDelta,
            IEnumerable<string> grantedCards)
        {
            return new ShopTransactionRecord
            {
                transactionId = transactionId,
                kind = kind,
                productId = productId ?? string.Empty,
                coinDelta = coinDelta,
                balanceAfter = CoinBalance,
                createdUtcTicks = DateTime.UtcNow.Ticks,
                grantedCardIds = (grantedCards ?? Array.Empty<string>())
                    .Select(FrontendCardIdentity.NormalizeOfficialId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList()
            };
        }

        private void RestoreEconomySnapshot(string json)
        {
            State = JsonUtility.FromJson<DeckCollectionState>(json) ??
                new DeckCollectionState();
            State.schemaVersion = CurrentSchemaVersion;
            State.decks ??= new List<DeckRecord>();
            State.unlockedDeckProductIds ??= new List<string>();
            NormalizeEconomyState(CurrentSchemaVersion);
            NormalizeCoinRewardAuthorizationState(CurrentSchemaVersion);
        }

        private static int SecureIndex(int count)
        {
            if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            int limit = int.MaxValue - int.MaxValue % count;
            byte[] bytes = new byte[4];
            using RandomNumberGenerator generator = RandomNumberGenerator.Create();
            int value;
            do
            {
                generator.GetBytes(bytes);
                value = BitConverter.ToInt32(bytes, 0) & int.MaxValue;
            } while (value >= limit);
            return value % count;
        }
    }
}
