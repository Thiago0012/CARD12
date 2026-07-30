using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ArcaneDuel.Game
{
    [Serializable]
    public sealed class CardVisualData
    {
        public uint officialCode;
        public string artFile;
        public string frameStyle;
        public string summonVfx;
        public string activationSfx;
        public string riskLevel;
        public string scriptStatus;
        public string scriptFile;
        public string[] presentationTags;
    }

    [Serializable]
    internal sealed class CardVisualCollection
    {
        public int schemaVersion;
        public int count;
        public string catalogSha256;
        public CardVisualData[] cards;
    }

    public sealed class CardVisualCatalog
    {
        private readonly Dictionary<uint, CardVisualData> entries;

        private CardVisualCatalog(
            Dictionary<uint, CardVisualData> entries,
            string catalogSha256)
        {
            this.entries = entries;
            CatalogSha256 = catalogSha256;
        }

        public int Count => entries.Count;
        public string CatalogSha256 { get; }
        public IEnumerable<CardVisualData> Cards => entries.Values;

        public bool TryGet(uint code, out CardVisualData visual)
        {
            return entries.TryGetValue(code, out visual);
        }

        public CardVisualData Get(uint code)
        {
            if (!TryGet(code, out CardVisualData visual))
            {
                throw new KeyNotFoundException(
                    $"Card {code:00000000} has no presentation entry.");
            }
            return visual;
        }

        public string ArtPath(uint code)
        {
            CardVisualData visual = Get(code);
            return Path.Combine(
                Application.streamingAssetsPath,
                "Ygo",
                "Art",
                visual.artFile);
        }

        public static CardVisualCatalog LoadDefault()
        {
            return Load(Path.Combine(
                Application.streamingAssetsPath,
                "Ygo",
                "Visual",
                "card-visuals.json"));
        }

        public static CardVisualCatalog Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The generated visual catalog is missing.",
                    path);
            }

            CardVisualCollection collection =
                JsonUtility.FromJson<CardVisualCollection>(
                    File.ReadAllText(path));
            if (collection == null ||
                collection.schemaVersion != 1 ||
                collection.cards == null ||
                collection.cards.Length != collection.count)
            {
                throw new InvalidDataException(
                    "The visual catalog header or count is invalid.");
            }

            var result = new Dictionary<uint, CardVisualData>();
            foreach (CardVisualData visual in collection.cards)
            {
                if (visual == null || visual.officialCode == 0)
                {
                    throw new InvalidDataException(
                        "The visual catalog contains an invalid card code.");
                }
                if (string.IsNullOrWhiteSpace(visual.artFile) ||
                    string.IsNullOrWhiteSpace(visual.frameStyle) ||
                    string.IsNullOrWhiteSpace(visual.riskLevel))
                {
                    throw new InvalidDataException(
                        $"Card {visual.officialCode:00000000} has incomplete visual metadata.");
                }
                if (!result.TryAdd(visual.officialCode, visual))
                {
                    throw new InvalidDataException(
                        $"Visual code {visual.officialCode:00000000} is duplicated.");
                }
            }

            return new CardVisualCatalog(
                result,
                collection.catalogSha256 ?? string.Empty);
        }
    }
}
