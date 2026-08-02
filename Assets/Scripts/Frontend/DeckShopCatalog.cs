using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.Game;

namespace ArcaneArena.Frontend
{
    internal static class FrontendCardIdentity
    {
        public static string NormalizeOfficialId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string trimmed = value.Trim();
            for (int index = 0; index < trimmed.Length; index++)
            {
                if (!char.IsDigit(trimmed[index]))
                    return string.Empty;
            }

            string normalized = trimmed.TrimStart('0');
            return normalized.Length == 0 ? "0" : normalized;
        }
    }

    public sealed class DeckShopProduct
    {
        public string ProductId { get; }
        public string DisplayName { get; }
        public string ArchetypeLabel { get; }
        public string Description { get; }
        public string CoverCardId { get; }
        public string SourceUrl { get; }
        public int CaseTheme { get; }
        public int PriceCoins { get; }
        public int MaxPurchases { get; }
        public IReadOnlyList<string> MainDeckCardIds { get; }
        public IReadOnlyList<string> ExtraDeckCardIds { get; }
        public IReadOnlyList<DeckShopPreview> Previews { get; }

        public DeckShopProduct(
            string productId,
            string displayName,
            string archetypeLabel,
            string description,
            string coverCardId,
            string sourceUrl,
            int caseTheme,
            IEnumerable<string> mainDeckCardIds,
            IEnumerable<string> extraDeckCardIds,
            int priceCoins = 425,
            int maxPurchases = 3,
            IEnumerable<DeckShopPreview> previews = null)
        {
            ProductId = productId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ArchetypeLabel = archetypeLabel ?? string.Empty;
            Description = description ?? string.Empty;
            CoverCardId = NormalizeCardId(coverCardId);
            SourceUrl = sourceUrl ?? string.Empty;
            CaseTheme = Math.Max(0, caseTheme);
            PriceCoins = Math.Max(1, priceCoins);
            MaxPurchases = Math.Max(1, maxPurchases);
            MainDeckCardIds = NormalizeCards(mainDeckCardIds);
            ExtraDeckCardIds = NormalizeCards(extraDeckCardIds);
            Previews = previews != null
                ? previews.Where(preview => preview != null).Take(3).ToArray()
                : new[] { CoverCardId }
                    .Concat(MainDeckCardIds)
                    .Concat(ExtraDeckCardIds)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .Take(3)
                    .Select(id => new DeckShopPreview(id, 0f, 0f, 1f, 1f))
                    .ToArray();
        }

        public string DeckId => $"shop:{ProductId}";

        public IReadOnlyList<string> PreviewCardIds =>
            Previews.Select(preview => preview.CardId).ToArray();

        public DeckRecord CreateDeckRecord()
        {
            var deck = new DeckRecord
            {
                deckId = DeckId,
                displayName = DisplayName,
                caseTheme = CaseTheme,
                mainDeckCardIds =
                    new List<string>(MainDeckCardIds),
                extraDeckCardIds =
                    new List<string>(ExtraDeckCardIds)
            };
            deck.featuredCardIds.Add(CoverCardId);
            deck.Normalize();
            return deck;
        }

        private static IReadOnlyList<string> NormalizeCards(
            IEnumerable<string> source)
        {
            return source == null
                ? Array.Empty<string>()
                : source
                    .Select(NormalizeCardId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToArray();
        }

        private static string NormalizeCardId(string value)
        {
            return FrontendCardIdentity.NormalizeOfficialId(
                value);
        }
    }

    public sealed class DeckShopPreview
    {
        public string CardId { get; }
        public float CropX { get; }
        public float CropY { get; }
        public float CropWidth { get; }
        public float CropHeight { get; }
        public bool HasValidCrop =>
            CropX >= 0f && CropY >= 0f &&
            CropWidth > 0f && CropHeight > 0f &&
            CropX + CropWidth <= 1f &&
            CropY + CropHeight <= 1f;

        public DeckShopPreview(
            string cardId,
            float cropX,
            float cropY,
            float cropWidth,
            float cropHeight)
        {
            CardId = FrontendCardIdentity.NormalizeOfficialId(cardId);
            CropX = cropX;
            CropY = cropY;
            CropWidth = cropWidth;
            CropHeight = cropHeight;
        }
    }

    /// <summary>
    /// Produtos gratuitos e determinísticos da loja. As listas preservam o
    /// Main/Extra Deck das páginas informadas; Side Deck não faz parte do
    /// modelo de deck atual do projeto.
    /// </summary>
    public static partial class DeckShopCatalog
    {
        public const string BlueEyesProductId =
            "classic-blue-eyes-dragon-genesys-99";
        public const string DarkMagicianProductId =
            "yugi-mutou-dark-magician-classic";
        public const string RedEyesProductId =
            "classic-red-eyes-black-dragon";
        public const string YugiMutoBattleCityProductId =
            "yugi-muto-battle-city-722944";
        public const string ToonTestProductId =
            "toon-test-718475";
        public const string ShiranuiSupremacyProductId =
            "shiranui-supremacy-699840";
        public const string MausoleumLockdownEdisonProductId =
            "mausoleum-lockdown-edison-724211";

        private static readonly IReadOnlyList<DeckShopProduct>
            AvailableProducts = new[]
            {
                CreateBlueEyesMaxReplacementProduct(),
                CreateDarkMagicalBlastReplacementProduct(),
                new DeckShopProduct(
                    RedEyesProductId,
                    "Deck Dragão Negro",
                    "OLHOS VERMELHOS",
                    "A linha clássica do Dragão Negro com destruição e pressão ofensiva.",
                    "74677422",
                    "https://ygoprodeck.com/deck/red-eyes-black-dragon-276135",
                    1,
                    new[]
                    {
                        "74677422", "74677422", "74677422", "36262024",
                        "36262024", "51632798", "51632798", "51632798",
                        "96561011", "71413901", "69015963", "88240808",
                        "88240808", "15960641", "15960641", "15960641",
                        "83011278", "83011278", "83011278", "4335645",
                        "26202165", "79571449", "19613556", "52684508",
                        "46411259", "46411259", "5318639", "71044499",
                        "71044499", "55144522", "70828912", "45986603",
                        "97077563", "44095762", "83555666", "56120475",
                        "56120475", "56120475", "77754944", "77754944"
                    },
                    new[]
                    {
                        "13756293", "13756293", "63519819", "63519819"
                    }),
                new DeckShopProduct(
                    YugiMutoBattleCityProductId,
                    "Yugi Muto — Cidade das Batalhas",
                    "CIDADE DAS BATALHAS",
                    "A lista clássica de Yugi na Cidade das Batalhas, com Slifer e seus monstros emblemáticos.",
                    "10000020",
                    "https://ygoprodeck.com/deck/yugi-muto-battle-city-722944",
                    2,
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.YugiMutoBattleCityMain),
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.YugiMutoBattleCityExtra)),
                new DeckShopProduct(
                    ToonTestProductId,
                    "Deck Toon",
                    "MUNDO TOON",
                    "Estratégia Toon com controle, respostas rápidas e um Deck Adicional completo.",
                    "53183600",
                    "https://ygoprodeck.com/deck/toon-test-718475",
                    0,
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.ToonTestMain),
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.ToonTestExtra)),
                new DeckShopProduct(
                    ShiranuiSupremacyProductId,
                    "Supremacia Shiranui",
                    "SHIRANUI / MAYAKASHI",
                    "Sincronias Zumbi, efeitos no banimento e a escalada sobrenatural dos Shiranui e Mayakashi.",
                    "59843383",
                    "https://ygoprodeck.com/deck/shiranui-supremacy-699840",
                    1,
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.ShiranuiSupremacyMain),
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.ShiranuiSupremacyExtra)),
                new DeckShopProduct(
                    MausoleumLockdownEdisonProductId,
                    "Mausoléu Lockdown Edison",
                    "FORMATO EDISON",
                    "Campos que aceleram monstros de alto Nível, pressão de Máquinas e bloqueios clássicos do formato Edison.",
                    "80921533",
                    "https://ygoprodeck.com/deck/mausoleum-lockdown-edison-724211",
                    2,
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.MausoleumLockdownEdisonMain),
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.MausoleumLockdownEdisonExtra))
            }
            .Concat(CreateBatchJuly2026Products())
            .Concat(CreateBatchAugust2026Products())
            .ToArray();

        private static readonly HashSet<string> LegacyOwnedCardIds =
            new HashSet<string>(
                new[]
                {
                    "89631139", "71413901", "97077563", "46986414",
                    "97268402", "44095762", "83764719", "05318639",
                    "55144522", "26202165", "82732705", "73628505"
                },
                StringComparer.Ordinal);

        private static readonly HashSet<string> ProductCardIds =
            new HashSet<string>(
                AvailableProducts.SelectMany(product =>
                    product.MainDeckCardIds.Concat(
                        product.ExtraDeckCardIds)),
                StringComparer.Ordinal);

        public static IReadOnlyList<DeckShopProduct> Products =>
            AvailableProducts;

        public static IReadOnlyList<DeckRecord> CreateOpponentRoster()
        {
            // A random opponent selects one complete thematic list. Cards
            // from different archetypes are never mixed to manufacture a deck.
            return AvailableProducts
                .Select(product => product.CreateDeckRecord())
                .ToArray();
        }

        public static DeckRecord ChooseOpponentDeck(
            string playerDeckId,
            ulong selector)
        {
            DeckRecord[] completeDecks = CreateOpponentRoster()
                .Where(deck =>
                    !string.Equals(
                        deck.deckId,
                        playerDeckId,
                        StringComparison.Ordinal))
                .ToArray();
            if (completeDecks.Length == 0)
                completeDecks = CreateOpponentRoster().ToArray();
            if (completeDecks.Length == 0)
                return null;

            int index = (int)(selector % (ulong)completeDecks.Length);
            return completeDecks[index];
        }

        public static int UniqueCardCount =>
            ProductCardIds.Count;
        public static IReadOnlyList<string> CollectibleCardIds =>
            ProductCardIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        public static int InitiallyLockedCardCount =>
            ProductCardIds.Count(id =>
                !LegacyOwnedCardIds.Contains(id));

        public static DeckShopProduct Find(string productId)
        {
            if (string.IsNullOrWhiteSpace(productId))
                return null;

            return AvailableProducts.FirstOrDefault(product =>
                string.Equals(
                    product.ProductId,
                    productId,
                    StringComparison.Ordinal));
        }

        public static bool IsLockedShopCard(string cardId)
        {
            var normalized =
                FrontendCardIdentity.NormalizeOfficialId(
                    cardId);
            return ProductCardIds.Contains(normalized) &&
                   !LegacyOwnedCardIds.Contains(normalized);
        }

        public static int OwnedCopies(
            DeckCollectionState state,
            string cardId)
        {
            var normalized =
                FrontendCardIdentity.NormalizeOfficialId(
                    cardId);
            int explicitQuantity = 0;
            if (state?.cardQuantities != null)
            {
                foreach (CardQuantityRecord record in state.cardQuantities)
                {
                    if (record != null && string.Equals(
                            FrontendCardIdentity.NormalizeOfficialId(record.cardId),
                            normalized,
                            StringComparison.Ordinal))
                    {
                        explicitQuantity = Math.Max(0, record.quantity);
                        break;
                    }
                }
            }

            if (!IsLockedShopCard(normalized))
                return Math.Max(3, explicitQuantity);
            if (state?.unlockedDeckProductIds == null)
                return explicitQuantity;

            var copies = 0;
            foreach (var productId in
                     state.unlockedDeckProductIds)
            {
                var product = Find(productId);
                if (product == null)
                    continue;

                copies += product.MainDeckCardIds.Count(id =>
                    string.Equals(
                        id,
                        normalized,
                        StringComparison.Ordinal));
                copies += product.ExtraDeckCardIds.Count(id =>
                    string.Equals(
                        id,
                        normalized,
                        StringComparison.Ordinal));
                if (copies >= 3)
                    break;
            }

            return Math.Max(explicitQuantity, Math.Min(3, copies));
        }
    }
}
