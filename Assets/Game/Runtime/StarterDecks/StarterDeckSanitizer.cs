using System;
using System.Collections.Generic;

namespace ArcaneDuel.Game
{
    public sealed class StarterDeckSanitizationResult
    {
        internal StarterDeckSanitizationResult()
        {
        }

        public List<string> MainDeck { get; } = new List<string>();
        public List<string> ExtraDeck { get; } = new List<string>();
        public List<string> SideDeck { get; } = new List<string>();
        public List<ReplacementAuditEntry> Audit { get; } =
            new List<ReplacementAuditEntry>();
        public bool IsLegal { get; internal set; }
        public string LegalitySummary { get; internal set; } = string.Empty;
    }

    public static class StarterDeckSanitizer
    {
        public static StarterDeckSanitizationResult Sanitize(
            RawStarterDeckDefinition raw,
            BanlistService banlist,
            IEnumerable<ReplacementAuditEntry> approvedReplacements = null)
        {
            if (raw == null)
                throw new ArgumentNullException(nameof(raw));
            if (banlist == null)
                throw new ArgumentNullException(nameof(banlist));

            var result = new StarterDeckSanitizationResult();
            var copies = new Dictionary<string, int>(StringComparer.Ordinal);
            SanitizeSection("Main", raw.mainDeck, result.MainDeck,
                result.Audit, copies, banlist);
            SanitizeSection("Extra", raw.extraDeck, result.ExtraDeck,
                result.Audit, copies, banlist);
            SanitizeSection("Side", raw.sideDeck, result.SideDeck,
                result.Audit, copies, banlist);

            ApplyApprovedMainReplacements(
                result,
                copies,
                approvedReplacements,
                banlist);

            DeckLegalityResult legality = DeckLegalityValidator.Validate(
                result.MainDeck,
                result.ExtraDeck,
                result.SideDeck,
                banlist);
            result.IsLegal = legality.IsLegal;
            result.LegalitySummary = legality.Summary;
            return result;
        }

        private static void ApplyApprovedMainReplacements(
            StarterDeckSanitizationResult result,
            IDictionary<string, int> copies,
            IEnumerable<ReplacementAuditEntry> approvedReplacements,
            BanlistService banlist)
        {
            int required = Math.Max(
                0,
                DeckLegalityValidator.MinimumMain - result.MainDeck.Count);
            foreach (ReplacementAuditEntry replacement in
                     approvedReplacements ?? Array.Empty<ReplacementAuditEntry>())
            {
                if (required == 0 || replacement == null ||
                    !replacement.approved ||
                    !string.Equals(
                        replacement.section,
                        "Main",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string passcode = BanlistService.NormalizePasscode(
                    replacement.replacementPasscode);
                if (string.IsNullOrEmpty(passcode))
                    continue;
                copies.TryGetValue(passcode, out int current);
                int maximum = banlist.MaximumCopies(passcode);
                if (current >= maximum || current >= 3)
                    continue;

                result.MainDeck.Add(passcode);
                copies[passcode] = current + 1;
                result.Audit.Add(new ReplacementAuditEntry
                {
                    removedPasscode = replacement.removedPasscode ?? string.Empty,
                    replacementPasscode = passcode,
                    section = "Main",
                    reason = replacement.reason ??
                        "Substituicao aprovada pelo desenvolvedor.",
                    approved = true
                });
                required--;
            }

            while (required-- > 0)
            {
                result.Audit.Add(new ReplacementAuditEntry
                {
                    removedPasscode = string.Empty,
                    replacementPasscode = string.Empty,
                    section = "Main",
                    reason = "O Main Deck ficou abaixo de 40; informe uma substituicao aprovada.",
                    approved = false
                });
            }
        }

        private static void SanitizeSection(
            string section,
            IEnumerable<string> source,
            ICollection<string> destination,
            ICollection<ReplacementAuditEntry> audit,
            IDictionary<string, int> copies,
            BanlistService banlist)
        {
            foreach (string value in source ?? Array.Empty<string>())
            {
                string passcode = BanlistService.NormalizePasscode(value);
                if (string.IsNullOrEmpty(passcode))
                {
                    audit.Add(Removal(value, section, "Passcode inválido."));
                    continue;
                }

                copies.TryGetValue(passcode, out int count);
                int maximum = banlist.MaximumCopies(passcode);
                if (count >= maximum)
                {
                    string reason = maximum == 0
                        ? "Carta proibida pela banlist ativa."
                        : $"Excesso acima do limite {maximum} da banlist ativa.";
                    audit.Add(Removal(passcode, section, reason));
                    continue;
                }

                copies[passcode] = count + 1;
                destination.Add(passcode);
            }
        }

        private static ReplacementAuditEntry Removal(
            string passcode,
            string section,
            string reason)
        {
            return new ReplacementAuditEntry
            {
                removedPasscode = passcode ?? string.Empty,
                replacementPasscode = string.Empty,
                section = section,
                reason = reason,
                approved = true
            };
        }
    }
}
