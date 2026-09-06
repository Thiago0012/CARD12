using System.Collections.Generic;
using ArcaneDuel.DuelEngine.Data;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Read-only breakdown of identities already safe to show in a public pile.
    /// Hidden cards must be supplied as code zero, even if the local player
    /// knows their identity. Never use these UI counts to resolve card effects:
    /// the native Core owns current types, legality and effect-specific filters.
    /// </summary>
    public sealed class PublicPileTypeSummary
    {
        public int Monsters { get; private set; }
        public int Spells { get; private set; }
        public int Traps { get; private set; }
        public int Unidentified { get; private set; }

        public static PublicPileTypeSummary FromVisibleCodes(
            CardDatabase database, IEnumerable<uint> codes)
        {
            var summary = new PublicPileTypeSummary();
            if (codes == null) return summary;
            foreach (uint code in codes)
            {
                if (code == 0 || database == null ||
                    !database.TryGet(code, out CardRecord card))
                {
                    summary.Unidentified++;
                    continue;
                }
                // These are bit flags, not exact type values: Fusion, Ritual,
                // Link and Pendulum retain the Monster bit outside the field.
                if ((card.Type & 0x1) != 0) summary.Monsters++;
                else if ((card.Type & 0x2) != 0) summary.Spells++;
                else if ((card.Type & 0x4) != 0) summary.Traps++;
                else summary.Unidentified++;
            }
            return summary;
        }

        public string ToDisplayText()
        {
            string text = $"MONSTROS {Monsters} · MAGIAS {Spells} · ARMADILHAS {Traps}";
            return Unidentified == 0
                ? text
                : text + $" · NÃO REVELADAS {Unidentified}";
        }
    }
}
