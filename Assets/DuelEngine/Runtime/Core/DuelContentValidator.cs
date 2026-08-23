using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Scripts;

namespace ArcaneDuel.DuelEngine.Core
{
    /// <summary>
    /// Fails before a duel starts when a selected card has no compiled data
    /// or executable Core script. This prevents visual-only cards from
    /// silently entering a duel with no behaviour.
    /// </summary>
    public static class DuelContentValidator
    {
        public static string[] FindProblems(
            CardDatabase database,
            string ygoContentRoot,
            params IEnumerable<uint>[] cardGroups)
        {
            if (string.IsNullOrWhiteSpace(ygoContentRoot))
                throw new ArgumentException(
                    "The YGO content root is required.",
                    nameof(ygoContentRoot));
            return FindProblems(
                database,
                new ScriptRepository(ygoContentRoot),
                cardGroups);
        }

        internal static string[] FindProblems(
            CardDatabase database,
            ScriptRepository scripts,
            params IEnumerable<uint>[] cardGroups)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));
            if (scripts == null)
                throw new ArgumentNullException(nameof(scripts));

            var problems = new List<string>();
            foreach (string global in new[] { "constant.lua", "utility.lua" })
            {
                if (!scripts.TryRead(global, out _))
                    problems.Add($"global script missing: {global}");
            }

            IEnumerable<uint> cards = (cardGroups ??
                Array.Empty<IEnumerable<uint>>())
                .Where(group => group != null)
                .SelectMany(group => group)
                .Where(code => code != 0)
                .Distinct()
                .OrderBy(code => code);
            foreach (uint code in cards)
            {
                if (!database.TryGet(code, out CardRecord record))
                {
                    problems.Add($"card data missing: {code:00000000}");
                    continue;
                }

                // Plain Normal Monsters and generated Tokens do not own a
                // Lua effect script. Other cards resolve their printed-code
                // file, including aliases through CustomScripts wrappers.
                if (!RequiresScript(record))
                    continue;
                string scriptName = $"c{code}.lua";
                if (!scripts.TryRead(scriptName, out byte[] bytes) ||
                    bytes == null || bytes.Length == 0)
                {
                    problems.Add(
                        $"card script missing: {code:00000000} {record.Name}");
                }
            }
            return problems.ToArray();
        }

        public static bool RequiresScript(CardRecord record)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));
            const uint MonsterType = 0x1U;
            const uint NormalType = 0x10U;
            const uint TokenType = 0x4000U;
            const uint PendulumType = 0x1000000U;
            bool plainNormalMonster =
                (record.Type & MonsterType) != 0 &&
                (record.Type & NormalType) != 0 &&
                (record.Type & PendulumType) == 0;
            return !plainNormalMonster &&
                   (record.Type & TokenType) == 0;
        }

        public static void ThrowIfUnsupported(
            CardDatabase database,
            string ygoContentRoot,
            params IEnumerable<uint>[] cardGroups)
        {
            string[] problems = FindProblems(
                database,
                ygoContentRoot,
                cardGroups);
            if (problems.Length > 0)
            {
                throw new InvalidDataException(
                    "The selected duel contains unsupported cards: " +
                    string.Join(" | ", problems));
            }
        }
    }
}
