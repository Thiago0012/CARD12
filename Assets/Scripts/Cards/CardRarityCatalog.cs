using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ArcaneArena.Cards
{
    public enum CardRarity
    {
        Unknown = 0,
        N = 1,
        R = 2,
        SR = 3,
        UR = 4
    }

    public enum CardArtVariant
    {
        Auto = 0,
        Base = 1,
        Alt = 2,
        Alt1 = 3,
        Alt2 = 4
    }

    /// <summary>
    /// Resolves Master Duel rarity from the official English card name while
    /// leaving localized presentation names untouched.  BASE is the safe
    /// automatic choice when alternate arts have different rarities.
    /// </summary>
    public static class CardRarityCatalog
    {
        private const string ResourcePath = "CardData/MasterDuelRarities";
        private static Dictionary<string, List<CardRarityEntryRecord>> _entries;
        private static Dictionary<string, CardRarityEntryRecord> _articleAliases;
        private static bool _loadAttempted;

        public static bool TryResolve(
            string englishName,
            out CardRarity rarity)
        {
            return TryResolve(
                englishName,
                CardArtVariant.Auto,
                out rarity,
                out _);
        }

        public static bool TryResolve(
            string englishName,
            CardArtVariant variant,
            out CardRarity rarity,
            out string matchedEnglishName)
        {
            EnsureLoaded();
            rarity = CardRarity.Unknown;
            matchedEnglishName = string.Empty;
            string key = NormalizeName(englishName);
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (_entries != null &&
                _entries.TryGetValue(key, out List<CardRarityEntryRecord> candidates) &&
                TrySelect(candidates, variant, out CardRarityEntryRecord selected))
            {
                rarity = selected.rarity;
                matchedEnglishName = selected.englishName ?? string.Empty;
                return IsValid(rarity);
            }

            // "The" is optional only when doing so cannot confuse two cards
            // with different rarities (for example Rock Spirit).
            if (_articleAliases != null &&
                _articleAliases.TryGetValue(key, out CardRarityEntryRecord alias))
            {
                rarity = alias.rarity;
                matchedEnglishName = alias.englishName ?? string.Empty;
                return IsValid(rarity);
            }
            return false;
        }

        public static bool IsValid(CardRarity rarity)
        {
            return rarity >= CardRarity.N && rarity <= CardRarity.UR;
        }

        public static string Label(CardRarity rarity)
        {
            return IsValid(rarity) ? rarity.ToString() : "?";
        }

        public static int GenerateCost(CardRarity rarity)
        {
            return IsValid(rarity) ? 30 : 0;
        }

        public static int DismantleReturn(
            CardRarity rarity,
            CardFinish finish = CardFinish.Normal)
        {
            if (!IsValid(rarity))
                return 0;
            return finish switch
            {
                CardFinish.Glossy => 15,
                CardFinish.Royal => 30,
                _ => 10
            };
        }

        internal static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var result = new StringBuilder(decomposed.Length);
            bool pendingSpace = false;
            foreach (char character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                    UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                if (char.IsLetterOrDigit(character))
                {
                    if (pendingSpace && result.Length > 0)
                        result.Append(' ');
                    result.Append(char.ToLowerInvariant(character));
                    pendingSpace = false;
                }
                else if (result.Length > 0)
                {
                    pendingSpace = true;
                }
            }
            return result.ToString();
        }

        internal static void ResetForTests()
        {
            _entries = null;
            _articleAliases = null;
            _loadAttempted = false;
        }

        private static void EnsureLoaded()
        {
            if (_loadAttempted)
                return;
            _loadAttempted = true;
            _entries = new Dictionary<string, List<CardRarityEntryRecord>>(
                StringComparer.Ordinal);
            _articleAliases = new Dictionary<string, CardRarityEntryRecord>(
                StringComparer.Ordinal);
            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError(
                    $"Catálogo de raridades ausente em Resources/{ResourcePath}.json.");
                return;
            }

            CardRarityFile file = JsonUtility.FromJson<CardRarityFile>(asset.text);
            if (file?.entries == null || file.schemaVersion != 1)
            {
                Debug.LogError("Catálogo de raridades inválido ou incompatível.");
                return;
            }

            foreach (CardRarityEntryRecord entry in file.entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.englishName) ||
                    !IsValid(entry.rarity))
                {
                    continue;
                }
                string key = NormalizeName(entry.englishName);
                if (!_entries.TryGetValue(
                        key,
                        out List<CardRarityEntryRecord> candidates))
                {
                    candidates = new List<CardRarityEntryRecord>();
                    _entries.Add(key, candidates);
                }
                candidates.Add(entry);
            }

            var aliasCandidates = new Dictionary<
                string,
                List<CardRarityEntryRecord>>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<CardRarityEntryRecord>> pair in _entries)
            {
                if (!pair.Key.StartsWith("the ", StringComparison.Ordinal))
                    continue;
                string aliasKey = pair.Key.Substring(4);
                if (!aliasCandidates.TryGetValue(
                        aliasKey,
                        out List<CardRarityEntryRecord> candidates))
                {
                    candidates = new List<CardRarityEntryRecord>();
                    aliasCandidates.Add(aliasKey, candidates);
                }
                if (TrySelect(
                        pair.Value,
                        CardArtVariant.Auto,
                        out CardRarityEntryRecord selected))
                {
                    candidates.Add(selected);
                }
            }
            foreach (KeyValuePair<string, List<CardRarityEntryRecord>> pair in
                     aliasCandidates)
            {
                if (_entries.ContainsKey(pair.Key) || pair.Value.Count == 0)
                    continue;
                CardRarity rarity = pair.Value[0].rarity;
                if (pair.Value.TrueForAll(entry => entry.rarity == rarity))
                    _articleAliases[pair.Key] = pair.Value[0];
            }
        }

        private static bool TrySelect(
            List<CardRarityEntryRecord> candidates,
            CardArtVariant requested,
            out CardRarityEntryRecord selected)
        {
            selected = null;
            if (candidates == null || candidates.Count == 0)
                return false;
            string expected = requested switch
            {
                CardArtVariant.Base => "BASE",
                CardArtVariant.Alt => "ALT",
                CardArtVariant.Alt1 => "ALT 1",
                CardArtVariant.Alt2 => "ALT 2",
                _ => "BASE"
            };
            selected = candidates.Find(entry => string.Equals(
                entry.variant,
                expected,
                StringComparison.Ordinal));
            if (selected == null && requested == CardArtVariant.Alt)
            {
                selected = candidates.Find(entry => string.Equals(
                    entry.variant,
                    "ALT 1",
                    StringComparison.Ordinal));
            }
            if (selected == null && requested == CardArtVariant.Alt1)
            {
                selected = candidates.Find(entry => string.Equals(
                    entry.variant,
                    "ALT",
                    StringComparison.Ordinal));
            }
            selected ??= candidates[0];
            return true;
        }
    }

    public enum CardFinish
    {
        Normal = 0,
        Glossy = 1,
        Royal = 2
    }

    [Serializable]
    internal sealed class CardRarityFile
    {
        public int schemaVersion;
        public List<CardRarityEntryRecord> entries;
    }

    [Serializable]
    internal sealed class CardRarityEntryRecord
    {
        public string englishName;
        public string variant;
        public CardRarity rarity;
    }
}
