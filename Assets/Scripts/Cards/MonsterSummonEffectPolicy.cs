namespace ArcaneArena.Cards
{
    public enum MonsterSummonArrivalEffect
    {
        None = 0,
        Yellow = 1,
        Blue = 2,
        Purple = 3
    }

    /// <summary>
    /// Visual classification only. It never participates in legality,
    /// resolution, timing or any other duel rule.
    /// </summary>
    public static class MonsterSummonEffectPolicy
    {
        public const int MinimumComplexEffectCharacters = 80;

        public static MonsterSummonArrivalEffect Resolve(
            CardCatalogEntry entry)
        {
            return entry == null
                ? MonsterSummonArrivalEffect.None
                : Resolve(
                    entry.MonsterFrame,
                    entry.Level,
                    entry.EffectText);
        }

        public static MonsterSummonArrivalEffect Resolve(
            MonsterFrameKind frame,
            int level,
            string effectText)
        {
            if (frame == MonsterFrameKind.Fusion)
                return MonsterSummonArrivalEffect.Purple;
            if (frame == MonsterFrameKind.Synchro)
                return MonsterSummonArrivalEffect.Blue;

            // Links are intentionally excluded. Xyz uses Rank instead of
            // Level. Yellow is restricted to effect-bearing Level frames,
            // Level 6+, and a substantial effect text.
            bool levelEffectFrame = frame == MonsterFrameKind.Effect ||
                                    frame == MonsterFrameKind.Ritual ||
                                    frame == MonsterFrameKind.Pendulum;
            if (!levelEffectFrame || level < 6 ||
                SignificantCharacterCount(effectText) <
                    MinimumComplexEffectCharacters)
            {
                return MonsterSummonArrivalEffect.None;
            }
            return MonsterSummonArrivalEffect.Yellow;
        }

        private static int SignificantCharacterCount(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0;
            int count = 0;
            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character))
                    count++;
            }
            return count;
        }
    }
}
