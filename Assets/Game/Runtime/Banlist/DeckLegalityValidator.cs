using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcaneDuel.Game
{
    public sealed class DeckLegalityResult
    {
        private readonly List<string> errors = new List<string>();

        public bool IsLegal => errors.Count == 0;
        public IReadOnlyList<string> Errors => errors;
        public string Summary => IsLegal
            ? "Deck legal para a banlist ativa."
            : string.Join(" ", errors);

        internal void Add(string error)
        {
            errors.Add(error);
        }
    }

    public static class DeckLegalityValidator
    {
        public const int MinimumMain = 40;
        public const int MaximumMain = 60;
        public const int MaximumExtra = 15;
        public const int MaximumSide = 15;

        public static DeckLegalityResult Validate(
            IEnumerable<string> main,
            IEnumerable<string> extra,
            IEnumerable<string> side,
            BanlistService banlist)
        {
            if (banlist == null)
                throw new ArgumentNullException(nameof(banlist));

            List<string> mainCards = Normalize(main);
            List<string> extraCards = Normalize(extra);
            List<string> sideCards = Normalize(side);
            var result = new DeckLegalityResult();

            if (mainCards.Count < MinimumMain || mainCards.Count > MaximumMain)
            {
                result.Add($"O Main Deck deve ter {MinimumMain}–{MaximumMain} cartas.");
            }
            if (extraCards.Count > MaximumExtra)
                result.Add($"O Extra Deck deve ter no máximo {MaximumExtra} cartas.");
            if (sideCards.Count > MaximumSide)
                result.Add($"O Side Deck deve ter no máximo {MaximumSide} cartas.");

            ValidateIdentifiers(mainCards, "Main Deck", result);
            ValidateIdentifiers(extraCards, "Extra Deck", result);
            ValidateIdentifiers(sideCards, "Side Deck", result);

            var copies = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string passcode in mainCards.Concat(extraCards).Concat(sideCards))
            {
                if (string.IsNullOrEmpty(passcode))
                    continue;
                copies.TryGetValue(passcode, out int count);
                copies[passcode] = count + 1;
            }

            foreach (KeyValuePair<string, int> copy in copies)
            {
                int maximum = banlist.MaximumCopies(copy.Key);
                if (copy.Value <= maximum)
                    continue;
                BanlistEntry entry = banlist.Find(copy.Key);
                string name = string.IsNullOrWhiteSpace(entry?.officialName)
                    ? copy.Key
                    : entry.officialName;
                result.Add($"{name} ({copy.Key}) usa {copy.Value} cópia(s); máximo {maximum}.");
            }
            return result;
        }

        private static List<string> Normalize(IEnumerable<string> cards)
        {
            return (cards ?? Array.Empty<string>())
                .Select(BanlistService.NormalizePasscode)
                .ToList();
        }

        private static void ValidateIdentifiers(
            IReadOnlyList<string> cards,
            string section,
            DeckLegalityResult result)
        {
            for (int index = 0; index < cards.Count; index++)
            {
                if (string.IsNullOrEmpty(cards[index]))
                    result.Add($"{section} #{index + 1}: passcode inválido.");
            }
        }
    }
}
