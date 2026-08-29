using System;
using System.Collections.Generic;

namespace ArcaneDuel.Game.Accounts
{
    /// <summary>
    /// Fixed-capacity allowlist for local developer-only diagnostics. Public
    /// IDs are used so no authentication provider identifier is stored in
    /// source. The second slot remains reserved until its owner is defined.
    /// </summary>
    public static class DeveloperAccountRegistry
    {
        public const int Capacity = 2;
        public const uint MixaelCardCode = 99990001U;
        public const uint WomenRepellentCardCode = 99990002U;
        public const uint ImminentMisfortuneCardCode = 99990003U;
        public const string PrimaryPublicId = "656728582265";
        public const string SecondaryPublicId = "";

        private static readonly string[] PublicIds =
        {
            PrimaryPublicId,
            SecondaryPublicId
        };

        public static IReadOnlyList<string> ConfiguredPublicIds => PublicIds;

        public static bool IsDeveloperPublicId(string publicId)
        {
            string normalized = (publicId ?? string.Empty).Trim();
            if (!PlayerIdAccessPolicy.IsValidPublicId(normalized))
                return false;

            foreach (string configured in PublicIds)
            {
                if (!string.IsNullOrWhiteSpace(configured) &&
                    string.Equals(
                        configured,
                        normalized,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsDeveloperCanonicalId(string canonicalPlayerId)
        {
            return IsDeveloperPublicId(
                PlayerIdAccessPolicy.FormatPublicId(canonicalPlayerId));
        }

        /// <summary>
        /// Cards made available to the authenticated developer command menu.
        /// Mixael is staged in the authoritative Extra Deck; the Spell command
        /// cards use the Core's reserved command location. None of them enters
        /// the opening hand or is persisted in a saved deck.
        /// </summary>
        public static uint[] CreateDeveloperCommandCards() =>
            new[]
            {
                MixaelCardCode,
                WomenRepellentCardCode,
                ImminentMisfortuneCardCode
            };

        public static uint[] CreateDeveloperSpellCommandCards() =>
            new[]
            {
                WomenRepellentCardCode,
                ImminentMisfortuneCardCode
            };

        public static bool IsDeveloperOnlyCard(uint code) =>
            code == MixaelCardCode ||
            code == WomenRepellentCardCode ||
            code == ImminentMisfortuneCardCode;
    }
}
