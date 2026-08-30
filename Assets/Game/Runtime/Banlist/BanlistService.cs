using System;
using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Data;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public sealed class BanlistService
    {
        public const string ActiveBanlistId = "tcg_eu_2026_05_18";
        private const string ActiveResourcePath =
            "Banlist/tcg_eu_2026_05_18";

        private static BanlistService active;
        private static readonly object CardIdentityLock = new object();
        private static CardDatabase defaultCardDatabase;
        private static bool defaultCardDatabaseResolved;
        private readonly Dictionary<string, int> limits;
        private readonly Dictionary<string, string> equivalenceByPasscode;

        public BanlistService(BanlistDefinition definition)
            : this(definition, ResolveDefaultCardDatabase())
        {
        }

        public BanlistService(
            BanlistDefinition definition,
            CardDatabase cardDatabase)
        {
            Definition = definition != null
                ? definition
                : throw new ArgumentNullException(nameof(definition));
            equivalenceByPasscode = BuildEquivalenceMap(cardDatabase);
            limits = BuildLimits(definition.Entries, equivalenceByPasscode);
        }

        public BanlistDefinition Definition { get; }
        public string Id => Definition.Id;

        public static BanlistService Active => active ??= LoadActive();

        public int MaximumCopies(string passcode)
        {
            string restrictionKey = RestrictionKey(passcode);
            return limits.TryGetValue(restrictionKey, out int maximum)
                ? maximum
                : 3;
        }

        /// <summary>
        /// Returns the shared restriction identity for alternate artworks and
        /// any other records that the compiled Core catalog names as the same
        /// card. Deck validation uses this key so mixed artworks cannot bypass
        /// a forbidden, limited, semi-limited, or general copy limit.
        /// </summary>
        public string RestrictionKey(string passcode)
        {
            string normalized = NormalizePasscode(passcode);
            return equivalenceByPasscode.TryGetValue(
                normalized,
                out string equivalent)
                ? equivalent
                : normalized;
        }

        public BanlistEntry Find(string passcode)
        {
            string restrictionKey = RestrictionKey(passcode);
            BanlistEntry bestMatch = null;
            foreach (BanlistEntry entry in Definition.Entries)
            {
                if (entry != null &&
                    string.Equals(
                        RestrictionKey(entry.passcode),
                        restrictionKey,
                        StringComparison.Ordinal))
                {
                    if (bestMatch == null ||
                        entry.maxCopies < bestMatch.maxCopies)
                    {
                        bestMatch = entry;
                    }
                }
            }
            return bestMatch;
        }

        public Sprite BadgeFor(string passcode)
        {
            switch (MaximumCopies(passcode))
            {
                case 0: return Definition.ForbiddenBadge;
                case 1: return Definition.LimitedBadge;
                case 2: return Definition.SemiLimitedBadge;
                default: return null;
            }
        }

        public static string NormalizePasscode(string value)
        {
            if (!uint.TryParse(value?.Trim(), out uint code) || code == 0)
                return string.Empty;
            return code.ToString("00000000");
        }

        private static BanlistService LoadActive()
        {
            BanlistDefinition definition =
                Resources.Load<BanlistDefinition>(ActiveResourcePath);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"Banlist ativa ausente em Resources/{ActiveResourcePath}.asset.");
            }
            return new BanlistService(definition);
        }

        private static Dictionary<string, int> BuildLimits(
            IReadOnlyList<BanlistEntry> entries,
            IReadOnlyDictionary<string, string> equivalenceByPasscode)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            var seenPasscodes = new HashSet<string>(StringComparer.Ordinal);
            if (entries == null)
                return result;

            foreach (BanlistEntry entry in entries)
            {
                string passcode = NormalizePasscode(entry?.passcode);
                if (string.IsNullOrEmpty(passcode))
                    throw new InvalidOperationException("A banlist contém passcode inválido.");
                if (entry.maxCopies < 0 || entry.maxCopies > 2)
                    throw new InvalidOperationException(
                        $"Limite inválido para {passcode}: {entry.maxCopies}.");
                if (!seenPasscodes.Add(passcode))
                    throw new InvalidOperationException(
                        $"Passcode duplicado na banlist: {passcode}.");
                string restrictionKey = equivalenceByPasscode.TryGetValue(
                    passcode,
                    out string equivalent)
                    ? equivalent
                    : passcode;
                if (result.TryGetValue(restrictionKey, out int existing))
                {
                    // If a legacy seed lists two artworks separately, the
                    // strictest entry becomes authoritative for the card.
                    result[restrictionKey] = Math.Min(
                        existing,
                        entry.maxCopies);
                }
                else
                {
                    result.Add(restrictionKey, entry.maxCopies);
                }
            }
            return result;
        }

        private static Dictionary<string, string> BuildEquivalenceMap(
            CardDatabase database)
        {
            var result = new Dictionary<string, string>(
                StringComparer.Ordinal);
            if (database == null)
                return result;

            var cardsByName = new Dictionary<string, List<CardRecord>>(
                StringComparer.OrdinalIgnoreCase);
            foreach (CardRecord card in database.Cards)
            {
                if (card == null || card.Code == 0)
                    continue;
                string name = NormalizeCardName(
                    string.IsNullOrWhiteSpace(card.EnglishName)
                        ? card.Name
                        : card.EnglishName);
                if (string.IsNullOrEmpty(name))
                    name = card.Code.ToString("00000000");
                if (!cardsByName.TryGetValue(
                        name,
                        out List<CardRecord> sameName))
                {
                    sameName = new List<CardRecord>();
                    cardsByName.Add(name, sameName);
                }
                sameName.Add(card);
            }

            foreach (List<CardRecord> sameName in cardsByName.Values)
            {
                uint canonicalCode = uint.MaxValue;
                foreach (CardRecord card in sameName)
                {
                    uint candidate = card.Alias != 0
                        ? card.Alias
                        : card.Code;
                    canonicalCode = Math.Min(canonicalCode, candidate);
                }
                if (canonicalCode == uint.MaxValue)
                    continue;
                string canonical = canonicalCode.ToString("00000000");
                foreach (CardRecord card in sameName)
                {
                    result[card.Code.ToString("00000000")] = canonical;
                    if (card.Alias != 0)
                    {
                        result[card.Alias.ToString("00000000")] = canonical;
                    }
                }
            }
            return result;
        }

        private static string NormalizeCardName(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private static CardDatabase ResolveDefaultCardDatabase()
        {
            lock (CardIdentityLock)
            {
                if (defaultCardDatabaseResolved)
                    return defaultCardDatabase;
                defaultCardDatabaseResolved = true;
                try
                {
                    defaultCardDatabase = CardDatabase.LoadDefault();
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "A equivalência de artes da banlist não pôde ser " +
                        "carregada; serão usados somente os passcodes da " +
                        "seed. " + exception.GetBaseException().Message);
                }
                return defaultCardDatabase;
            }
        }
    }
}
