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
        public IReadOnlyList<string> MainDeckCardIds { get; }
        public IReadOnlyList<string> ExtraDeckCardIds { get; }

        public DeckShopProduct(
            string productId,
            string displayName,
            string archetypeLabel,
            string description,
            string coverCardId,
            string sourceUrl,
            int caseTheme,
            IEnumerable<string> mainDeckCardIds,
            IEnumerable<string> extraDeckCardIds)
        {
            ProductId = productId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            ArchetypeLabel = archetypeLabel ?? string.Empty;
            Description = description ?? string.Empty;
            CoverCardId = NormalizeCardId(coverCardId);
            SourceUrl = sourceUrl ?? string.Empty;
            CaseTheme = Math.Max(0, caseTheme);
            MainDeckCardIds = NormalizeCards(mainDeckCardIds);
            ExtraDeckCardIds = NormalizeCards(extraDeckCardIds);
        }

        public string DeckId => $"shop:{ProductId}";

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

    /// <summary>
    /// Produtos gratuitos e determinísticos da loja. As listas preservam o
    /// Main/Extra Deck das páginas informadas; Side Deck não faz parte do
    /// modelo de deck atual do projeto.
    /// </summary>
    public static class DeckShopCatalog
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

        private static readonly IReadOnlyList<DeckShopProduct>
            AvailableProducts = new[]
            {
                new DeckShopProduct(
                    BlueEyesProductId,
                    "Deck Dragão Branco",
                    "OLHOS AZUIS",
                    "Poder dracônico, suporte de Nível 8 e um Deck Adicional completo.",
                    "89631139",
                    "https://ygoprodeck.com/deck/classic-blue-eyes-dragon-genesys-99-661181",
                    0,
                    new[]
                    {
                        "89631139", "89631139", "89631139", "38517737",
                        "30576089", "71039903", "17947697", "17947697",
                        "17947697", "8240199", "8240199", "8240199",
                        "54332792", "54332792", "66961194", "66961194",
                        "24508238", "24508238", "59438930", "14558127",
                        "14558127", "80326401", "93437091", "93437091",
                        "17725109", "17725109", "17725109", "71143015",
                        "38120068", "38120068", "54693926", "54693926",
                        "24382602", "56920308", "43219114", "43219114",
                        "62089826", "10045474", "10045474", "85442146"
                    },
                    new[]
                    {
                        "2129638", "56532353", "11443677", "40908371",
                        "59822133", "59822133", "89604813", "89604813",
                        "33698022", "43321985", "10515412", "16699558",
                        "16699558", "88177324", "39030163"
                    }),
                new DeckShopProduct(
                    DarkMagicianProductId,
                    "Deck Mago Negro",
                    "MAGIA NEGRA",
                    "Mago Negro, Magias temáticas e quinze opções no Deck Adicional.",
                    "46986414",
                    "https://ygoprodeck.com/deck/yugi-mutou-dark-magician-deck-classic-350924/",
                    2,
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.DarkMagicianMain),
                    CuratedDeckLists.AsCardIds(
                        CuratedDeckLists.DarkMagicianExtra)),
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
                        CuratedDeckLists.ShiranuiSupremacyExtra))
            };

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
            if (!IsLockedShopCard(normalized))
                return 3;
            if (state?.unlockedDeckProductIds == null)
                return 0;

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
                    return 3;
            }

            return Math.Min(3, copies);
        }
    }
}
