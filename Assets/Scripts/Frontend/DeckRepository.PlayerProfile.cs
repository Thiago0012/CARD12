using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.Game.Competitive;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        private const int MaximumProcessedStatisticResults = 256;

        public string EquippedIconId => ProfileIconCatalog.ResolveId(
            State?.cosmetics?.equippedIconId);

        public IReadOnlyCollection<string> OwnedIconIds
        {
            get
            {
                if (State?.cosmetics?.ownedIconIds == null)
                    return Array.Empty<string>();
                return State.cosmetics.ownedIconIds.AsReadOnly();
            }
        }

        public PlayerStatisticsState Statistics => State?.statistics;

        private void NormalizePlayerProfileState(int loadedSchemaVersion)
        {
            if (State == null)
                return;
            State.cosmetics ??= new ProfileCosmeticsState();
            State.cosmetics.ownedIconIds ??= new List<string>();
            State.cosmetics.ownedIconIds = State.cosmetics.ownedIconIds
                .Select(ProfileIconCatalog.ResolveId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            if (!State.cosmetics.ownedIconIds.Contains(
                    ProfileIconCatalog.DefaultIconId))
            {
                State.cosmetics.ownedIconIds.Insert(
                    0, ProfileIconCatalog.DefaultIconId);
            }
            State.cosmetics.equippedIconId = ProfileIconCatalog.ResolveId(
                State.cosmetics.equippedIconId);
            if (!State.cosmetics.ownedIconIds.Contains(
                    State.cosmetics.equippedIconId))
            {
                State.cosmetics.equippedIconId =
                    ProfileIconCatalog.DefaultIconId;
            }

            State.statistics ??= new PlayerStatisticsState();
            State.statistics.overall ??= new DuelStatisticsScope();
            State.statistics.online ??= new DuelStatisticsScope();
            State.statistics.ranked ??= new DuelStatisticsScope();
            State.statistics.processedResultIds ??= new List<string>();
            State.statistics.processedEventIds ??= new List<string>();
            State.statistics.overall.Normalize();
            State.statistics.online.Normalize();
            State.statistics.ranked.Normalize();
            State.statistics.processedResultIds = State.statistics
                .processedResultIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .TakeLast(MaximumProcessedStatisticResults)
                .ToList();
            State.statistics.processedEventIds = State.statistics
                .processedEventIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .TakeLast(MaximumProcessedStatisticResults)
                .ToList();
        }

        public bool OwnsIcon(string iconId)
        {
            string resolved = ProfileIconCatalog.ResolveId(iconId);
            return State?.cosmetics?.ownedIconIds?.Contains(resolved) == true;
        }

        public bool TryPurchaseIcon(
            string iconId,
            string transactionId,
            out ShopTransactionRecord receipt,
            out string rejection)
        {
            receipt = null;
            rejection = string.Empty;
            ProfileIconDefinition definition = ProfileIconCatalog.Resolve(iconId);
            if (definition == null || !definition.IsPurchasable ||
                !string.Equals(definition.IconId, iconId, StringComparison.Ordinal))
            {
                rejection = "Este ícone não está disponível para compra.";
                return false;
            }
            if (!TryPrepareTransaction(
                    transactionId,
                    "profile-icon",
                    definition.IconId,
                    out ShopTransactionRecord existing,
                    out rejection))
            {
                if (existing != null)
                {
                    receipt = existing;
                    return OwnsIcon(definition.IconId);
                }
                return false;
            }
            if (OwnsIcon(definition.IconId))
            {
                rejection = "Este ícone já pertence ao seu perfil.";
                return false;
            }
            if (CoinBalance < definition.PriceCoins)
            {
                rejection = $"Saldo insuficiente: faltam " +
                    $"{definition.PriceCoins - CoinBalance} moedas.";
                return false;
            }

            string snapshot = JsonUtility.ToJson(State);
            try
            {
                State.coinBalance -= definition.PriceCoins;
                State.cosmetics.ownedIconIds.Add(definition.IconId);
                receipt = CreateTransaction(
                    transactionId,
                    "profile-icon",
                    definition.IconId,
                    -definition.PriceCoins,
                    Array.Empty<string>());
                State.processedShopTransactions.Add(receipt);
                Save();
                return true;
            }
            catch (Exception exception)
            {
                RestoreEconomySnapshot(snapshot);
                rejection = exception.GetBaseException().Message;
                receipt = null;
                return false;
            }
        }

        public bool TryEquipIcon(string iconId, out string rejection)
        {
            rejection = string.Empty;
            if (State?.cosmetics == null)
            {
                rejection = "O perfil local ainda não foi carregado.";
                return false;
            }
            string resolved = ProfileIconCatalog.ResolveId(iconId);
            if (!string.Equals(resolved, iconId, StringComparison.Ordinal) ||
                !OwnsIcon(resolved))
            {
                rejection = "Compre este ícone antes de equipá-lo.";
                return false;
            }
            State.cosmetics.equippedIconId = resolved;
            Save();
            return true;
        }

        public DuelIdentitySnapshot CaptureDuelIdentitySnapshot()
        {
            if (State == null)
                return null;
            PlayerRankData rank = State.rankData ?? new PlayerRankData();
            rank.Normalize();
            return new DuelIdentitySnapshot
            {
                stablePlayerId = State.localProfileId ?? string.Empty,
                nickname = string.IsNullOrWhiteSpace(PlayerDisplayName)
                    ? "DUELISTA"
                    : PlayerDisplayName,
                equippedIconId = EquippedIconId,
                rankTier = RankRules.ResolveTier(rank.rankedPoints),
                rankedPoints = rank.rankedPoints,
                cosmeticsCatalogVersion = ProfileIconCatalog.CatalogVersion
            };
        }

        public bool TryRecordAuthoritativeDuelResult(
            string resultId,
            bool winner,
            bool draw,
            bool online,
            bool ranked,
            long damageDealt,
            long damageReceived,
            out string rejection)
        {
            rejection = string.Empty;
            if (State?.statistics == null || string.IsNullOrWhiteSpace(resultId))
            {
                rejection = "O resultado autoritativo é inválido.";
                return false;
            }
            if (State.statistics.processedResultIds.Contains(resultId))
                return true;

            ApplyResult(
                State.statistics.overall,
                winner,
                draw,
                damageDealt,
                damageReceived);
            if (online)
                ApplyResult(
                    State.statistics.online,
                    winner,
                    draw,
                    damageDealt,
                    damageReceived);
            if (ranked)
                ApplyResult(
                    State.statistics.ranked,
                    winner,
                    draw,
                    damageDealt,
                    damageReceived);
            State.statistics.processedResultIds.Add(resultId);
            if (State.statistics.processedResultIds.Count >
                MaximumProcessedStatisticResults)
            {
                State.statistics.processedResultIds.RemoveRange(
                    0,
                    State.statistics.processedResultIds.Count -
                    MaximumProcessedStatisticResults);
            }
            Save();
            return true;
        }

        private static void ApplyResult(
            DuelStatisticsScope scope,
            bool winner,
            bool draw,
            long damageDealt,
            long damageReceived)
        {
            long dealt = Math.Max(0, damageDealt);
            long received = Math.Max(0, damageReceived);
            scope.duelsPlayed++;
            if (draw)
                scope.draws++;
            else if (winner)
                scope.wins++;
            else
                scope.losses++;
            scope.damageDealt += dealt;
            scope.damageReceived += received;
            scope.maxDamageDealtInSingleDuel = Math.Max(
                scope.maxDamageDealtInSingleDuel,
                dealt);
            scope.maxDamageReceivedInSingleDuel = Math.Max(
                scope.maxDamageReceivedInSingleDuel,
                received);
        }

        public bool TryRecordAuthoritativeStatisticEvent(
            string eventId,
            DuelStatisticEventType eventType,
            long amount,
            bool online,
            bool ranked,
            out string rejection)
        {
            rejection = string.Empty;
            if (State?.statistics == null || string.IsNullOrWhiteSpace(eventId) ||
                amount <= 0)
            {
                rejection = "O evento estatístico autoritativo é inválido.";
                return false;
            }
            if (State.statistics.processedEventIds.Contains(eventId))
                return true;

            ApplyStatisticEvent(State.statistics.overall, eventType, amount);
            if (online)
                ApplyStatisticEvent(State.statistics.online, eventType, amount);
            if (ranked)
                ApplyStatisticEvent(State.statistics.ranked, eventType, amount);
            State.statistics.processedEventIds.Add(eventId);
            if (State.statistics.processedEventIds.Count >
                MaximumProcessedStatisticResults)
            {
                State.statistics.processedEventIds.RemoveRange(
                    0,
                    State.statistics.processedEventIds.Count -
                    MaximumProcessedStatisticResults);
            }
            Save();
            return true;
        }

        private static void ApplyStatisticEvent(
            DuelStatisticsScope scope,
            DuelStatisticEventType eventType,
            long amount)
        {
            switch (eventType)
            {
                case DuelStatisticEventType.MonsterDestroyedByBattle:
                    scope.monstersDestroyedByBattle += amount;
                    break;
                case DuelStatisticEventType.MonsterDestroyedByEffect:
                    scope.monstersDestroyedByEffect += amount;
                    break;
                case DuelStatisticEventType.SpellDestroyed:
                    scope.spellsDestroyed += amount;
                    break;
                case DuelStatisticEventType.TrapDestroyed:
                    scope.trapsDestroyed += amount;
                    break;
                case DuelStatisticEventType.SpellActivated:
                    scope.spellsActivated += amount;
                    break;
                case DuelStatisticEventType.TrapActivated:
                    scope.trapsActivated += amount;
                    break;
                case DuelStatisticEventType.DamageDealt:
                    scope.damageDealt += amount;
                    break;
                case DuelStatisticEventType.MonsterSummoned:
                    scope.monstersSummoned += amount;
                    break;
                case DuelStatisticEventType.SpecialSummon:
                    scope.specialSummons += amount;
                    scope.monstersSummoned += amount;
                    break;
            }
        }
    }
}
