using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ArcaneDuel.DuelEngine.Data;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class CardTokenDependencyEditModeTests
    {
        private static readonly Regex GlobalToken = new Regex(
            @"(?m)^\s*(TOKEN_[A-Z0-9_]+)\s*=\s*(\d+)\s*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex LocalSuccessorToken = new Regex(
            @"(?m)^\s*local\s+(TOKEN_[A-Z0-9_]+)\s*=\s*id\s*\+\s*1\s*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex ScriptLiteralToken = new Regex(
            @"(?m)^\s*(?:local\s+)?(TOKEN_[A-Z0-9_]+)\s*=\s*(\d+)\s*$",
            RegexOptions.CultureInvariant);

        private static readonly Regex CreateToken = new Regex(
            @"Duel\.CreateToken\s*\(\s*[^,]+,\s*" +
            @"(?<dependency>id\s*\+\s*1|\d+|TOKEN_[A-Z0-9_]+)\s*\)",
            RegexOptions.CultureInvariant);

        [Test]
        public void EveryScriptCreatedTokenHasCompiledCoreData()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            string ygoRoot = Path.Combine(
                Application.streamingAssetsPath,
                "Ygo");
            string officialRoot = Path.Combine(
                ygoRoot,
                "Scripts",
                "official");
            string constants = File.ReadAllText(Path.Combine(
                ygoRoot,
                "Scripts",
                "card_counter_constants.lua"));
            Dictionary<string, uint> globalTokens = GlobalToken
                .Matches(constants)
                .Cast<Match>()
                .ToDictionary(
                    match => match.Groups[1].Value,
                    match => uint.Parse(match.Groups[2].Value),
                    StringComparer.Ordinal);
            var unresolved = new List<string>();
            var dependencies = new HashSet<uint>();

            foreach (string scriptPath in Directory.EnumerateFiles(
                         officialRoot,
                         "c*.lua",
                         SearchOption.TopDirectoryOnly))
            {
                if (!uint.TryParse(
                        Path.GetFileNameWithoutExtension(scriptPath)
                            .Substring(1),
                        out uint scriptCode))
                {
                    continue;
                }

                string script = File.ReadAllText(scriptPath);
                Dictionary<string, uint> locals = LocalSuccessorToken
                    .Matches(script)
                    .Cast<Match>()
                    .ToDictionary(
                        match => match.Groups[1].Value,
                        _ => checked(scriptCode + 1),
                        StringComparer.Ordinal);
                foreach (Match literal in ScriptLiteralToken.Matches(script))
                {
                    locals[literal.Groups[1].Value] =
                        uint.Parse(literal.Groups[2].Value);
                }
                foreach (Match call in CreateToken.Matches(script))
                {
                    string expression = call.Groups["dependency"].Value;
                    if (!TryResolve(
                            expression,
                            scriptCode,
                            locals,
                            globalTokens,
                            out uint dependency))
                    {
                        unresolved.Add(
                            $"{Path.GetFileName(scriptPath)}: {expression}");
                        continue;
                    }
                    dependencies.Add(dependency);
                }
            }

            Assert.That(
                unresolved,
                Is.Empty,
                "Every CreateToken expression must be statically auditable.");
            Assert.That(
                dependencies,
                Is.Not.Empty,
                "The pinned script set should expose token dependencies.");
            string[] missing = dependencies
                .Where(code => !database.TryGet(code, out _))
                .OrderBy(code => code)
                .Select(code => code.ToString("00000000"))
                .ToArray();
            Assert.That(
                missing,
                Is.Empty,
                "A missing generated Token causes ocgcore's card-reader " +
                "callback to interrupt the resolving effect.");
        }

        [TestCase(2625940U, 16401U, 0, 0, 1, 16777216UL, 16U, 0U)]
        [TestCase(23331401U, 16401U, 0, 0, 1, 16777216UL, 16U, 0U)]
        [TestCase(26326542U, 16401U, 0, 0, 1, 16UL, 4U, 217U)]
        [TestCase(27198002U, 16401U, 500, 500, 2, 16UL, 4U, 0U)]
        [TestCase(27204312U, 16401U, -2, -2, 11, 256UL, 16U, 0U)]
        [TestCase(67922703U, 16401U, 0, 0, 3, 32UL, 8U, 4123U)]
        public void NewlyPinnedTokenMatchesOfficialCoreMetadata(
            uint code,
            uint type,
            int attack,
            int defense,
            int level,
            ulong race,
            uint attribute,
            uint expectedSetcode)
        {
            CardRecord token = CardDatabase.LoadDefault().Get(code);

            Assert.That(token.Type, Is.EqualTo(type));
            Assert.That(token.Attack, Is.EqualTo(attack));
            Assert.That(token.Defense, Is.EqualTo(defense));
            Assert.That(token.Level, Is.EqualTo(level));
            Assert.That(token.Race, Is.EqualTo(race));
            Assert.That(token.Attribute, Is.EqualTo(attribute));
            if (expectedSetcode == 0)
                Assert.That(token.Setcodes, Is.Empty);
            else
                Assert.That(token.Setcodes, Does.Contain((ushort)expectedSetcode));
        }

        private static bool TryResolve(
            string expression,
            uint scriptCode,
            IReadOnlyDictionary<string, uint> locals,
            IReadOnlyDictionary<string, uint> globals,
            out uint code)
        {
            string value = Regex.Replace(
                expression ?? string.Empty,
                @"\s+",
                string.Empty);
            if (value == "id+1")
            {
                code = checked(scriptCode + 1);
                return true;
            }
            if (uint.TryParse(value, out code))
                return true;
            if (locals.TryGetValue(value, out code))
                return true;
            return globals.TryGetValue(value, out code);
        }
    }
}
