using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.Game;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public sealed partial class DeckRepository
    {
        private const string LegacyStarterNamePrefix = "Deck Inicial ";

        private void NormalizeStarterDeckDisplayNames()
        {
            if (State?.decks == null || State.decks.Count == 0)
                return;

            StarterDeckCatalog catalog = Resources.Load<StarterDeckCatalog>(
                "StarterDecks/StarterDeckCatalog");
            if (catalog == null)
                return;

            foreach (DeckRecord deck in State.decks)
            {
                if (deck == null || !LooksLikeLegacyStarterName(deck.displayName))
                    continue;

                StarterDeckDefinition definition = ResolveLegacyStarter(
                    catalog,
                    deck);
                if (definition != null &&
                    !string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    deck.displayName = definition.DisplayName;
                }
            }
        }

        private StarterDeckDefinition ResolveLegacyStarter(
            StarterDeckCatalog catalog,
            DeckRecord deck)
        {
            string numericSuffix = deck.displayName
                .Substring(LegacyStarterNamePrefix.Length)
                .Trim();
            StarterDeckDefinition direct = catalog.Find(
                "starter_" + numericSuffix);
            if (direct != null)
                return direct;

            if (!string.IsNullOrWhiteSpace(State.starterDeckId))
            {
                direct = catalog.Find(State.starterDeckId.Trim());
                if (direct != null && SameStarterComposition(deck, direct))
                    return direct;
            }

            return catalog.Decks.FirstOrDefault(candidate =>
                candidate != null && SameStarterComposition(deck, candidate));
        }

        private static bool LooksLikeLegacyStarterName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !value.Trim().StartsWith(
                    LegacyStarterNamePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string suffix = value.Trim()
                .Substring(LegacyStarterNamePrefix.Length)
                .Trim();
            return suffix.Length > 0 && suffix.All(char.IsDigit);
        }

        private static bool SameStarterComposition(
            DeckRecord deck,
            StarterDeckDefinition definition)
        {
            return SameCards(deck.mainDeckCardIds, definition.MainDeck) &&
                   SameCards(deck.extraDeckCardIds, definition.ExtraDeck);
        }

        private static bool SameCards(
            IEnumerable<string> left,
            IEnumerable<string> right)
        {
            string[] normalizedLeft = (left ?? Array.Empty<string>())
                .Select(NormalizeCardId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] normalizedRight = (right ?? Array.Empty<string>())
                .Select(NormalizeCardId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return normalizedLeft.SequenceEqual(
                normalizedRight,
                StringComparer.Ordinal);
        }

        private static string NormalizeCardId(string value)
        {
            return uint.TryParse(value, out uint code)
                ? code.ToString("00000000")
                : (value ?? string.Empty).Trim();
        }
    }
}
