using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArcaneDuel.Game
{
    public sealed class BanlistService
    {
        public const string ActiveBanlistId = "tcg_eu_2026_05_18";
        private const string ActiveResourcePath =
            "Banlist/tcg_eu_2026_05_18";

        private static BanlistService active;
        private readonly Dictionary<string, int> limits;

        public BanlistService(BanlistDefinition definition)
        {
            Definition = definition != null
                ? definition
                : throw new ArgumentNullException(nameof(definition));
            limits = BuildLimits(definition.Entries);
        }

        public BanlistDefinition Definition { get; }
        public string Id => Definition.Id;

        public static BanlistService Active => active ??= LoadActive();

        public int MaximumCopies(string passcode)
        {
            string normalized = NormalizePasscode(passcode);
            return limits.TryGetValue(normalized, out int maximum)
                ? maximum
                : 3;
        }

        public BanlistEntry Find(string passcode)
        {
            string normalized = NormalizePasscode(passcode);
            foreach (BanlistEntry entry in Definition.Entries)
            {
                if (entry != null &&
                    string.Equals(
                        NormalizePasscode(entry.passcode),
                        normalized,
                        StringComparison.Ordinal))
                {
                    return entry;
                }
            }
            return null;
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
            IReadOnlyList<BanlistEntry> entries)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
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
                if (!result.TryAdd(passcode, entry.maxCopies))
                    throw new InvalidOperationException(
                        $"Passcode duplicado na banlist: {passcode}.");
            }
            return result;
        }
    }
}
