using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArcaneArena.Cards
{
    /// <summary>
    /// Metadados das cartas fornecidas pelos produtos da loja. O arquivo JSON
    /// versionado em Resources permite ampliar o catálogo sem transformar o
    /// banco legado em uma lista monolítica e continua disponível nos builds.
    /// </summary>
    public static class DeckShopCardDatabase
    {
        private const string ResourcePath = "CardData/DeckShopCards";
        private static Dictionary<string, CardMetadata> _cards;

        public static bool TryGetByAssetName(
            string assetName,
            out CardMetadata metadata)
        {
            EnsureLoaded();
            return _cards.TryGetValue(
                NormalizeAssetKey(assetName),
                out metadata);
        }

        public static bool Contains(string officialCardId)
        {
            EnsureLoaded();
            return _cards.ContainsKey(
                NormalizeAssetKey(officialCardId));
        }

        private static void EnsureLoaded()
        {
            if (_cards != null)
                return;

            _cards = new Dictionary<string, CardMetadata>(
                StringComparer.OrdinalIgnoreCase);
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning(
                    $"Metadados da loja ausentes em Resources/{ResourcePath}.json.");
                return;
            }

            var collection =
                JsonUtility.FromJson<DeckShopCardMetadataCollection>(
                    asset.text);
            if (collection?.cards == null)
                return;

            foreach (var card in collection.cards)
            {
                if (card == null ||
                    string.IsNullOrWhiteSpace(card.officialId))
                {
                    continue;
                }

                var metadata = new CardMetadata(
                    NormalizeOfficialId(card.officialId),
                    card.displayName ?? string.Empty,
                    (CardCategory)card.category,
                    (MonsterFrameKind)card.monsterFrame,
                    card.typeName ?? string.Empty,
                    card.raceName ?? string.Empty,
                    (CardAttribute)card.attribute,
                    Math.Max(0, card.level),
                    card.attack,
                    card.defense,
                    card.description ?? string.Empty,
                    CardEffectId.None);
                _cards[NormalizeAssetKey(card.officialId)] = metadata;
            }
        }

        private static string NormalizeOfficialId(string value)
        {
            var key = Path
                .GetFileNameWithoutExtension(value ?? string.Empty)
                .Trim();
            return long.TryParse(key, out var numeric)
                ? numeric.ToString("D8")
                : key;
        }

        private static string NormalizeAssetKey(string assetName)
        {
            var key = Path
                .GetFileNameWithoutExtension(assetName ?? string.Empty)
                .Trim();
            return long.TryParse(key, out var numeric)
                ? numeric.ToString()
                : key.ToLowerInvariant();
        }
    }

    [Serializable]
    internal sealed class DeckShopCardMetadataCollection
    {
        public List<DeckShopCardMetadataRecord> cards = new();
    }

    [Serializable]
    internal sealed class DeckShopCardMetadataRecord
    {
        public string officialId;
        public string displayName;
        public int category;
        public int monsterFrame;
        public string typeName;
        public string raceName;
        public int attribute;
        public int level;
        public int attack = -1;
        public int defense = -1;
        public string description;
    }
}
