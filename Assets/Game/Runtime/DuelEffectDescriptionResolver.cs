using System;
using System.Text;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Turns the description identifier emitted by ocgcore into the concrete
    /// effect option presented to a player. Rules remain authoritative in the
    /// Core; this class only prevents distinct effects from sharing a generic
    /// "Activate" button.
    /// </summary>
    public static class DuelEffectDescriptionResolver
    {
        public static string ChoiceLabel(
            DuelChoice choice,
            CardDatabase database)
        {
            if (choice == null)
                return string.Empty;

            string contextualLabel = ContextualActionLabel(
                choice,
                database);

            if (TryResolve(
                    choice.DescriptionId,
                    database,
                    out string effectText,
                    out int effectNumber,
                    out _))
            {
                string action = ActionLabel(
                    contextualLabel,
                    effectNumber);
                // Distinct effects can share a long prefix and diverge only
                // near the end. A fixed cap would make legal Core candidates
                // visually indistinguishable.
                return action + "\n" + CollapseWhitespace(effectText);
            }

            if (choice.CardCode != 0 && database != null &&
                database.TryGet(choice.CardCode, out CardRecord card) &&
                !string.IsNullOrWhiteSpace(card.Name))
            {
                return contextualLabel + "\n" +
                       CollapseWhitespace(card.Name);
            }

            return contextualLabel;
        }

        /// <summary>
        /// Makes the three different uses of a Pendulum Monster explicit in
        /// the action UI without changing the Core response.  The Core is
        /// still the sole authority that decides which actions are legal.
        /// </summary>
        public static string ContextualActionLabel(
            DuelChoice choice,
            CardDatabase database)
        {
            string label = choice?.Label ?? string.Empty;
            if (choice == null || choice.CardCode == 0 || database == null ||
                !database.TryGet(choice.CardCode, out CardRecord card) ||
                (card.Type & 0x01000000U) == 0U)
            {
                return label;
            }

            if (Contains(label, "Invocação especial") &&
                (choice.Location & DuelLocation.SpellTrapZone) != 0)
            {
                return "Invocação-Pêndulo";
            }

            if (string.Equals(
                    label,
                    "Invocar",
                    StringComparison.OrdinalIgnoreCase) &&
                (choice.Location & DuelLocation.Hand) != 0)
            {
                return "Invocação-Normal";
            }

            if (choice.DescriptionId == 0 &&
                string.Equals(
                    label,
                    "Ativar",
                    StringComparison.OrdinalIgnoreCase) &&
                (choice.Location & DuelLocation.Hand) != 0)
            {
                return "Ativar como Magia Pêndulo";
            }

            return label;
        }

        public static bool TryResolve(
            ulong descriptionId,
            CardDatabase database,
            out string effectText,
            out int effectNumber,
            out uint cardCode)
        {
            effectText = string.Empty;
            effectNumber = 0;
            cardCode = 0;
            if (descriptionId == 0 || database == null)
                return false;

            // Auxiliary.Stringid packs the printed card code in the upper
            // bits and the per-card string index in the lower 20 bits.
            cardCode = unchecked((uint)(descriptionId >> 20));
            int index = checked((int)(descriptionId & 0xFFFFF));
            if (cardCode == 0 ||
                !database.TryGet(cardCode, out CardRecord card) ||
                card.Strings == null || index >= card.Strings.Length)
            {
                return false;
            }

            effectText = CollapseWhitespace(card.Strings[index]);
            if (string.IsNullOrWhiteSpace(effectText))
                return false;
            // Script string slots are not necessarily contiguous. Jioh, for
            // example, uses slots 0 and 4. Present the ordinal among the
            // card's real, localized effect descriptions instead of leaking
            // the database slot as "effect 5" to the player.
            effectNumber = 1;
            for (int prior = 0; prior < index; prior++)
            {
                if (!string.IsNullOrWhiteSpace(card.Strings[prior]))
                    effectNumber++;
            }
            return true;
        }

        private static string ActionLabel(string source, int effectNumber)
        {
            string label = source ?? string.Empty;
            if (Contains(label, "Não ativar") ||
                Contains(label, "Nao ativar") ||
                Contains(label, "Não responder") ||
                Contains(label, "Nao responder"))
            {
                return $"NÃO ATIVAR · EFEITO {effectNumber}";
            }
            if (Contains(label, "Ativar") || Contains(label, "Opção"))
                return $"ATIVAR · EFEITO {effectNumber}";
            return string.IsNullOrWhiteSpace(label)
                ? $"EFEITO {effectNumber}"
                : $"{label} · EFEITO {effectNumber}";
        }

        private static string CollapseWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            var result = new StringBuilder(value.Length);
            bool previousWhitespace = false;
            foreach (char character in value.Trim())
            {
                bool whitespace = char.IsWhiteSpace(character);
                if (whitespace)
                {
                    if (!previousWhitespace) result.Append(' ');
                }
                else result.Append(character);
                previousWhitespace = whitespace;
            }
            return result.ToString();
        }

        private static bool Contains(string source, string fragment)
        {
            return (source ?? string.Empty).IndexOf(
                fragment,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
