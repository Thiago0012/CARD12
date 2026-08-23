using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.Game.Competitive;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    [Serializable]
    public sealed class ProfileCosmeticsState
    {
        public string equippedIconId = ProfileIconCatalog.DefaultIconId;
        public List<string> ownedIconIds = new List<string>();
    }

    [Serializable]
    public sealed class DuelStatisticsScope
    {
        public long duelsPlayed;
        public long wins;
        public long losses;
        public long draws;
        public long monstersDestroyedByBattle;
        public long monstersDestroyedByEffect;
        public long spellsDestroyed;
        public long trapsDestroyed;
        public long spellsActivated;
        public long trapsActivated;
        public long damageDealt;
        public long damageReceived;
        public long maxDamageDealtInSingleDuel;
        public long maxDamageReceivedInSingleDuel;
        public long monstersSummoned;
        public long specialSummons;

        public void Normalize()
        {
            duelsPlayed = Math.Max(0, duelsPlayed);
            wins = Math.Max(0, wins);
            losses = Math.Max(0, losses);
            draws = Math.Max(0, draws);
            monstersDestroyedByBattle = Math.Max(0, monstersDestroyedByBattle);
            monstersDestroyedByEffect = Math.Max(0, monstersDestroyedByEffect);
            spellsDestroyed = Math.Max(0, spellsDestroyed);
            trapsDestroyed = Math.Max(0, trapsDestroyed);
            spellsActivated = Math.Max(0, spellsActivated);
            trapsActivated = Math.Max(0, trapsActivated);
            damageDealt = Math.Max(0, damageDealt);
            damageReceived = Math.Max(0, damageReceived);
            maxDamageDealtInSingleDuel = Math.Max(
                0,
                maxDamageDealtInSingleDuel);
            maxDamageReceivedInSingleDuel = Math.Max(
                0,
                maxDamageReceivedInSingleDuel);
            monstersSummoned = Math.Max(0, monstersSummoned);
            specialSummons = Math.Max(0, specialSummons);
        }
    }

    [Serializable]
    public sealed class PlayerStatisticsState
    {
        public DuelStatisticsScope overall = new DuelStatisticsScope();
        public DuelStatisticsScope online = new DuelStatisticsScope();
        public DuelStatisticsScope ranked = new DuelStatisticsScope();
        public List<string> processedResultIds = new List<string>();
        public List<string> processedEventIds = new List<string>();
    }

    public enum DuelStatisticEventType
    {
        MonsterDestroyedByBattle,
        MonsterDestroyedByEffect,
        SpellDestroyed,
        TrapDestroyed,
        SpellActivated,
        TrapActivated,
        DamageDealt,
        MonsterSummoned,
        SpecialSummon
    }

    [Serializable]
    public sealed class DuelIdentitySnapshot
    {
        public string stablePlayerId;
        public string nickname;
        public string equippedIconId;
        public RankTier rankTier;
        public int rankedPoints;
        public int cosmeticsCatalogVersion;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(stablePlayerId) &&
            !string.IsNullOrWhiteSpace(nickname) &&
            ProfileIconCatalog.Resolve(equippedIconId) != null &&
            cosmeticsCatalogVersion > 0;

        public DuelIdentitySnapshot Copy()
        {
            return new DuelIdentitySnapshot
            {
                stablePlayerId = stablePlayerId ?? string.Empty,
                nickname = nickname ?? string.Empty,
                equippedIconId = ProfileIconCatalog.ResolveId(equippedIconId),
                rankTier = rankTier,
                rankedPoints = rankedPoints,
                cosmeticsCatalogVersion = cosmeticsCatalogVersion
            };
        }
    }

    public sealed class ProfileIconDefinition
    {
        public string IconId { get; }
        public string DisplayName { get; }
        public string ResourcePath { get; }
        public int PriceCoins { get; }
        public bool IsPurchasable { get; }
        public ProfileIconAssetMode AssetMode { get; }
        public Rect PortraitUv { get; }
        public ProfileIconAuraTheme AuraTheme { get; }

        public ProfileIconDefinition(
            string iconId,
            string displayName,
            string resourcePath,
            bool isPurchasable,
            ProfileIconAssetMode assetMode =
                ProfileIconAssetMode.PreframedHex,
            Rect? portraitUv = null,
            ProfileIconAuraTheme auraTheme = ProfileIconAuraTheme.None)
        {
            IconId = iconId;
            DisplayName = displayName;
            ResourcePath = resourcePath;
            PriceCoins = isPurchasable ? ProfileIconCatalog.IconPriceCoins : 0;
            IsPurchasable = isPurchasable;
            AssetMode = assetMode;
            PortraitUv = portraitUv ?? new Rect(0f, 0f, 1f, 1f);
            AuraTheme = auraTheme;
        }
    }

    public enum ProfileIconAssetMode
    {
        PreframedHex,
        UnframedPortrait
    }

    /// <summary>
    /// Tema visual opcional aplicado somente à moldura. A arte do retrato
    /// continua sendo um recurso independente e nunca recebe partículas ou
    /// brilho diretamente.
    /// </summary>
    public enum ProfileIconAuraTheme
    {
        None,
        CrimsonLegendary,
        AzureArcane,
        SolarLegendary
    }

    public static class ProfileIconCatalog
    {
        public const int CatalogVersion = 3;
        public const int IconPriceCoins = 35;
        public const string DefaultIconId = "icon-arcane-default";

        private static readonly ProfileIconDefinition[] Items =
        {
            new(DefaultIconId, "Brasão Arcano", string.Empty, false),
            new("icon-astral-paladin", "Paladino Astral", "Profile/Icons/astral-paladin", true,
                portraitUv: PortraitUv(1536, 1024, 340, 20, 1196, 996)),
            new("icon-prismatic-dragon", "Dragão Prismático", "Profile/Icons/prismatic-dragon", true,
                portraitUv: PortraitUv(1536, 1024, 344, 16, 1192, 992)),
            new("icon-sun-hawk", "Falcão Solar", "Profile/Icons/sun-hawk", true,
                portraitUv: PortraitUv(1536, 1024, 316, 4, 1216, 1016)),
            new("icon-abyssal-sorceress", "Feiticeira Abissal", "Profile/Icons/abyssal-sorceress", true,
                portraitUv: PortraitUv(1254, 1254, 84, 20, 1168, 1228)),
            new("icon-golden-dragon", "Dragão Dourado", "Profile/Icons/golden-dragon", true,
                portraitUv: PortraitUv(1024, 1536, 28, 192, 992, 1292)),
            new("icon-void-elf", "Elfa do Vazio", "Profile/Icons/void-elf", true,
                portraitUv: PortraitUv(1536, 1024, 324, 12, 1212, 1008)),
            new("icon-crimson-knight", "Cavaleiro Carmesim", "Profile/Icons/crimson-knight", true,
                portraitUv: PortraitUv(1536, 1024, 332, 12, 1204, 1004)),
            new("icon-oracle-idol", "Ídolo Oráculo", "Profile/Icons/oracle-idol", true,
                portraitUv: PortraitUv(1254, 1254, 80, 16, 1172, 1236)),
            new("icon-celestial-hydra", "Hidra Celestial", "Profile/Icons/celestial-hydra", true,
                portraitUv: PortraitUv(1024, 1536, 32, 220, 992, 1308)),
            new("icon-crimson-cyberblade", "Lâmina Cibernética Rubra",
                "Profile/Icons/crimson-cyberblade", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-astral-aegis", "Égide Astral",
                "Profile/Icons/astral-aegis", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-violet-operator", "Operadora Violeta",
                "Profile/Icons/violet-operator", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-onyx-dragon", "Dragão de Ônix",
                "Profile/Icons/onyx-dragon", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-glacial-duelist", "Duelista Glacial",
                "Profile/Icons/glacial-duelist", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-amethyst-knight", "Cavaleiro Ametista",
                "Profile/Icons/amethyst-knight", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-crystal-dragon", "Dragão de Cristal",
                "Profile/Icons/crystal-dragon", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-eclipse-empress", "Imperatriz do Eclipse",
                "Profile/Icons/eclipse-empress", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-crimson-arcanist", "Arcanista Rubro",
                "Profile/Icons/crimson-arcanist", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-emerald-mage", "Maga Esmeralda",
                "Profile/Icons/emerald-mage", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-arcane-enigma", "Enigma Arcano",
                "Profile/Icons/arcane-enigma", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-sapphire-swordsman", "Espadachim Safira",
                "Profile/Icons/sapphire-swordsman", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-cosmic-imperator", "Imperador Cósmico",
                "Profile/Icons/cosmic-imperator", true,
                ProfileIconAssetMode.UnframedPortrait),
            new("icon-crimson-veil-arcanist", "Arcanista do Véu Rubro",
                "Profile/Icons/crimson-veil-arcanist", true,
                ProfileIconAssetMode.UnframedPortrait,
                auraTheme: ProfileIconAuraTheme.CrimsonLegendary)
        };

        private static readonly Dictionary<string, Texture2D> TextureCache =
            new(StringComparer.Ordinal);

        public static IReadOnlyList<ProfileIconDefinition> All => Items;
        public static IEnumerable<ProfileIconDefinition> Purchasable =>
            Items.Where(item => item.IsPurchasable);

        public static ProfileIconDefinition Resolve(string iconId)
        {
            return Items.FirstOrDefault(item => string.Equals(
                       item.IconId, iconId, StringComparison.Ordinal)) ?? Items[0];
        }

        public static string ResolveId(string iconId) => Resolve(iconId).IconId;

        public static string ResolveForStableIdentity(string stablePlayerId)
        {
            ProfileIconDefinition[] choices = Items
                .Where(item => item.IsPurchasable)
                .ToArray();
            if (choices.Length == 0)
                return DefaultIconId;
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in stablePlayerId ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return choices[hash % (uint)choices.Length].IconId;
            }
        }

        private static Rect PortraitUv(
            int width,
            int height,
            int minX,
            int minYFromTop,
            int maxX,
            int maxYFromTop)
        {
            // Os arquivos originais possuem uma moldura ciano e quantidades
            // diferentes de área vazia. A caixa informada acima delimita o
            // hexágono visível em cada textura. Um único recuo proporcional
            // remove a moldura pré-renderizada e entrega somente o retrato ao
            // HexIconView, que então aplica a moldura oficial do jogo.
            const float uniformInnerInset = 0.0425f;
            float visibleWidth = maxX - minX + 1f;
            float visibleHeight = maxYFromTop - minYFromTop + 1f;
            float insetX = visibleWidth * uniformInnerInset;
            float insetY = visibleHeight * uniformInnerInset;
            float xMin = Mathf.Clamp01((minX + insetX) / width);
            float xMax = Mathf.Clamp01((maxX + 1f - insetX) / width);
            float yMin = Mathf.Clamp01(
                1f - (maxYFromTop + 1f - insetY) / height);
            float yMax = Mathf.Clamp01(
                1f - (minYFromTop + insetY) / height);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static Texture2D LoadTexture(string iconId)
        {
            string resolved = ResolveId(iconId);
            if (TextureCache.TryGetValue(resolved, out Texture2D cached))
                return cached;
            ProfileIconDefinition definition = Resolve(resolved);
            Texture2D texture = string.IsNullOrWhiteSpace(definition.ResourcePath)
                ? CreateDefaultTexture()
                : Resources.Load<Texture2D>(definition.ResourcePath);
            if (texture == null)
                texture = CreateDefaultTexture();
            TextureCache[resolved] = texture;
            return texture;
        }

        private static Texture2D CreateDefaultTexture()
        {
            if (TextureCache.TryGetValue(DefaultIconId, out Texture2D cached))
                return cached;
            const int size = 128;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Arcane Default Profile Icon",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color dark = new(0.01f, 0.04f, 0.08f, 1f);
            Color cyan = new(0.02f, 0.78f, 0.95f, 1f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float ring = Mathf.Abs(Mathf.Max(Mathf.Abs(nx) * 0.866f +
                    Mathf.Abs(ny) * 0.5f, Mathf.Abs(ny)) - 0.74f);
                texture.SetPixel(x, y, ring < 0.055f ? cyan : dark);
            }
            texture.Apply(false, true);
            TextureCache[DefaultIconId] = texture;
            return texture;
        }
    }
}
