using System;
using System.Collections.Generic;
using ArcaneArena.Cards;
using ArcaneDuel.Game.Competitive;

namespace ArcaneArena.Frontend
{
    [Serializable]
    public sealed class DeckCollectionState
    {
        public int schemaVersion = 12;
        public string localProfileId;
        public string playerDisplayName;
        public bool starterDeckClaimed;
        public string starterDeckId;
        public string starterClaimTransactionId;
        public long starterClaimedAtUtcTicks;
        public int starterCatalogVersion;
        public string banlistVersionAtClaim;
        public string selectedDeckId;
        public List<DeckRecord> decks = new List<DeckRecord>();
        public List<string> unlockedDeckProductIds =
            new List<string>();
        public int coinBalance;
        public List<CardQuantityRecord> cardQuantities =
            new List<CardQuantityRecord>();
        public List<StructureDeckPurchaseRecord> structureDeckPurchases =
            new List<StructureDeckPurchaseRecord>();
        public List<PendingPackOpeningRecord> pendingPackOpenings =
            new List<PendingPackOpeningRecord>();
        public List<string> pendingDeckEditorNewCardIds =
            new List<string>();
        public List<ShopTransactionRecord> processedShopTransactions =
            new List<ShopTransactionRecord>();
        public PlayerCraftWallet craftPoints = new PlayerCraftWallet();
        public List<ProtectedCardQuantityRecord> protectedCardQuantities =
            new List<ProtectedCardQuantityRecord>();
        public List<CraftTransactionRecord> craftTransactions =
            new List<CraftTransactionRecord>();
        public CoinRewardAuthorizationState coinRewardAuthorization =
            new CoinRewardAuthorizationState();
        public PlayerRankData rankData = new PlayerRankData();
        public ProfileCosmeticsState cosmetics = new ProfileCosmeticsState();
        public PlayerStatisticsState statistics = new PlayerStatisticsState();
    }

    [Serializable]
    public sealed class CoinRewardAuthorizationState
    {
        public bool isAuthorized;
        public bool isRevoked;
        public string catalogEntryId;
        public string originallyAuthorizedNickname;
        public string normalizedAuthorizedNickname;
        public string boundLocalProfileId;
        public string boundInstallId;
        public long authorizedAtUtcUnixSeconds;
        public int catalogVersionAtAuthorization;
        public string integrityTag;
    }

    [Serializable]
    public sealed class CardQuantityRecord
    {
        public string cardId;
        public int quantity;
    }

    [Serializable]
    public sealed class PlayerCraftWallet
    {
        public int cpN;
        public int cpR;
        public int cpSR;
        public int cpUR;
    }

    [Serializable]
    public sealed class ProtectedCardQuantityRecord
    {
        public string cardId;
        public int quantity;
    }

    [Serializable]
    public sealed class CraftTransactionRecord
    {
        public string transactionId;
        public string operation;
        public string cardId;
        public CardRarity rarity;
        public CardFinish finish;
        public int quantity;
        public int cpDelta;
        public int balanceAfter;
        public long createdUtcTicks;
    }

    [Serializable]
    public sealed class StructureDeckPurchaseRecord
    {
        public string productId;
        public int purchaseCount;
    }

    [Serializable]
    public sealed class PendingPackOpeningRecord
    {
        public string transactionId;
        public string packId;
        public List<string> cardIds = new List<string>();
        public List<bool> revealed = new List<bool>();
    }

    [Serializable]
    public sealed class ShopTransactionRecord
    {
        public string transactionId;
        public string kind;
        public string productId;
        public int coinDelta;
        public int balanceAfter;
        public int damageDealt;
        public int completedRounds;
        public bool winner;
        public bool draw;
        public string matchId;
        public string localPlayerId;
        public string localProfileId;
        public string catalogEntryId;
        public int rewardRuleVersion;
        public RewardReceiptStatus rewardStatus = RewardReceiptStatus.Granted;
        public long createdUtcTicks;
        public List<string> grantedCardIds = new List<string>();
    }

    /// <summary>
    /// Cópia imutável por convenção do deck escolhido para uma nova partida.
    /// Contém somente IDs estáveis e dados serializáveis: nenhuma referência de
    /// cena, Sprite ou estado visual participa da identidade do loadout.
    /// </summary>
    [Serializable]
    public sealed class DuelDeckLoadout
    {
        public string profileId;
        public string playerDisplayName;
        public string deckId;
        public string displayName;
        public List<string> mainDeckCardIds = new List<string>();
        public List<string> extraDeckCardIds = new List<string>();
        public List<string> sideDeckCardIds = new List<string>();
        public string banlistId;
        public string normalizedDeckSha256;
        public DuelIdentitySnapshot identity;

        public static DuelDeckLoadout Create(
            string profileId,
            DeckRecord deck,
            string playerDisplayName = null)
        {
            if (deck == null)
                return null;

            deck.Normalize();
            return new DuelDeckLoadout
            {
                profileId = profileId ?? string.Empty,
                playerDisplayName = playerDisplayName ?? string.Empty,
                deckId = deck.deckId ?? string.Empty,
                displayName = deck.displayName ?? "Deck sem nome",
                mainDeckCardIds = new List<string>(
                    deck.mainDeckCardIds),
                extraDeckCardIds = new List<string>(
                    deck.extraDeckCardIds),
                sideDeckCardIds = new List<string>(
                    deck.sideDeckCardIds),
                banlistId = ArcaneDuel.Game.BanlistService.ActiveBanlistId,
                normalizedDeckSha256 = ArcaneDuel.Game.DeckManifestHasher
                    .ComputeSha256(
                        ArcaneDuel.Game.BanlistService.ActiveBanlistId,
                        deck.mainDeckCardIds,
                        deck.extraDeckCardIds,
                        deck.sideDeckCardIds),
                identity = null
            };
        }
    }

    [Serializable]
    public sealed class DeckRecord
    {
        public string deckId;
        public string displayName;
        public int caseTheme;
        public List<string> mainDeckCardIds = new List<string>();
        public List<string> extraDeckCardIds = new List<string>();
        public List<string> sideDeckCardIds = new List<string>();
        public List<string> featuredCardIds = new List<string>();

        public int TotalCards =>
            (mainDeckCardIds?.Count ?? 0) +
            (extraDeckCardIds?.Count ?? 0) +
            (sideDeckCardIds?.Count ?? 0);

        public void Normalize()
        {
            mainDeckCardIds ??= new List<string>();
            extraDeckCardIds ??= new List<string>();
            sideDeckCardIds ??= new List<string>();
            featuredCardIds ??= new List<string>();
            if (string.IsNullOrWhiteSpace(deckId))
                deckId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = "Novo Deck";
            caseTheme = Math.Max(0, caseTheme);
            RefreshFeaturedCards();
        }

        public void RefreshFeaturedCards()
        {
            featuredCardIds ??= new List<string>();
            featuredCardIds.Clear();

            AppendFeatured(mainDeckCardIds);
            if (featuredCardIds.Count < 3)
                AppendFeatured(extraDeckCardIds);
        }

        private void AppendFeatured(List<string> source)
        {
            if (source == null)
                return;

            foreach (var cardId in source)
            {
                if (featuredCardIds.Count >= 3)
                    return;
                if (string.IsNullOrWhiteSpace(cardId) ||
                    featuredCardIds.Contains(cardId))
                {
                    continue;
                }

                featuredCardIds.Add(cardId);
            }
        }
    }
}
