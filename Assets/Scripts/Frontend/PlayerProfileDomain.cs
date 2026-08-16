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

        public ProfileIconDefinition(
            string iconId,
            string displayName,
            string resourcePath,
            bool isPurchasable,
            ProfileIconAssetMode assetMode =
                ProfileIconAssetMode.PreframedHex,
            Rect? portraitUv = null)
        {
            IconId = iconId;
            DisplayName = displayName;
            ResourcePath = resourcePath;
            PriceCoins = isPurchasable ? ProfileIconCatalog.IconPriceCoins : 0;
            IsPurchasable = isPurchasable;
            AssetMode = assetMode;
            PortraitUv = portraitUv ?? new Rect(0f, 0f, 1f, 1f);
        }
    }

    public enum ProfileIconAssetMode
    {
        PreframedHex,
        UnframedPortrait
    }

    public static class ProfileIconCatalog
    {
        public const int CatalogVersion = 1;
        public const int IconPriceCoins = 35;
        public const string DefaultIconId = "icon-arcane-default";

        private static readonly ProfileIconDefinition[] Items =
        {
            new(DefaultIconId, "Brasão Arcano", string.Empty, false),
            new("icon-astral-paladin", "Paladino Astral", "Profile/Icons/astral-paladin", true,
                portraitUv: VisibleUv(1536, 1024, 340, 20, 1196, 996)),
            new("icon-prismatic-dragon", "Dragão Prismático", "Profile/Icons/prismatic-dragon", true,
                portraitUv: VisibleUv(1536, 1024, 344, 16, 1192, 992)),
            new("icon-sun-hawk", "Falcão Solar", "Profile/Icons/sun-hawk", true,
                portraitUv: VisibleUv(1536, 1024, 316, 4, 1216, 1016)),
            new("icon-abyssal-sorceress", "Feiticeira Abissal", "Profile/Icons/abyssal-sorceress", true,
                portraitUv: VisibleUv(1254, 1254, 84, 20, 1168, 1228)),
            new("icon-golden-dragon", "Dragão Dourado", "Profile/Icons/golden-dragon", true,
                portraitUv: VisibleUv(1024, 1536, 28, 192, 992, 1292)),
            new("icon-void-elf", "Elfa do Vazio", "Profile/Icons/void-elf", true,
                portraitUv: VisibleUv(1536, 1024, 324, 12, 1212, 1008)),
            new("icon-crimson-knight", "Cavaleiro Carmesim", "Profile/Icons/crimson-knight", true,
                portraitUv: VisibleUv(1536, 1024, 332, 12, 1204, 1004)),
            new("icon-oracle-idol", "Ídolo Oráculo", "Profile/Icons/oracle-idol", true,
                portraitUv: VisibleUv(1254, 1254, 80, 16, 1172, 1236)),
            new("icon-celestial-hydra", "Hidra Celestial", "Profile/Icons/celestial-hydra", true,
                portraitUv: VisibleUv(1024, 1536, 32, 220, 992, 1308))
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

        private static Rect VisibleUv(
            int width,
            int height,
            int minX,
            int minYFromTop,
            int maxX,
            int maxYFromTop)
        {
            float visibleWidth = maxX - minX + 1f;
            float visibleHeight = maxYFromTop - minYFromTop + 1f;
            float paddingX = visibleWidth * 0.025f;
            float paddingY = visibleHeight * 0.02f;
            float xMin = Mathf.Clamp01((minX - paddingX) / width);
            float xMax = Mathf.Clamp01((maxX + 1f + paddingX) / width);
            float yMin = Mathf.Clamp01(
                1f - (maxYFromTop + 1f + paddingY) / height);
            float yMax = Mathf.Clamp01(
                1f - (minYFromTop - paddingY) / height);
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
