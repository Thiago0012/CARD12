using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class DarkMagicianDeckAuditEditModeTests
    {
        [Serializable]
        private sealed class OfficialAuditRoot
        {
            public int schemaVersion;
            public string source;
            public OfficialAuditCard[] cards;
        }

        [Serializable]
        private sealed class OfficialAuditCard
        {
            public string code;
            public string officialName;
            public string cid;
            public string officialTextSha256;
            public string officialUrl;
        }

        [Serializable]
        private sealed class ShopRoot
        {
            public ShopCard[] cards;
        }

        [Serializable]
        private sealed class ShopCard
        {
            public string officialId;
            public string displayName;
            public string description;
        }

        [Test]
        public void DefaultRuleProfileUsesCurrentTcgSimultaneousTriggerRules()
        {
            var configuration = new DuelConfiguration();
            Assert.That(
                configuration.RuleProfile,
                Is.EqualTo(DuelRuleProfile.TcgMasterRule2021));
            Assert.That(
                OcgDuelEngine.RuleFlagsFor(configuration.RuleProfile),
                Is.EqualTo(0x30002E800UL));
            Assert.That(
                OcgDuelEngine.RuleFlagsFor(
                    DuelRuleProfile.OcgMasterRule2020),
                Is.EqualTo(0x2E800UL));

            const ulong obsoleteIgnitionPriority = 0x400000000UL;
            Assert.That(
                OcgDuelEngine.RuleFlagsFor(configuration.RuleProfile) &
                obsoleteIgnitionPriority,
                Is.Zero,
                "The legacy ignition-priority flag is not part of current TCG.");
        }

        [Test]
        public void DarkMagicianOfficialTextSnapshotMatchesPresentationCatalogs()
        {
            string auditPath = Path.Combine(
                Application.streamingAssetsPath,
                "Ygo",
                "Data",
                "official-tcg-pt-dark-magician-audit.json");
            string shopPath = Path.Combine(
                Application.dataPath,
                "Resources",
                "CardData",
                "DeckShopCards.json");
            string catalogPath = Path.Combine(
                Application.dataPath,
                "Cards",
                "CardCatalog.asset");

            var audit = JsonUtility.FromJson<OfficialAuditRoot>(
                File.ReadAllText(auditPath, Encoding.UTF8));
            var shop = JsonUtility.FromJson<ShopRoot>(
                File.ReadAllText(shopPath, Encoding.UTF8));
            string serializedCatalog = File.ReadAllText(
                catalogPath,
                Encoding.UTF8);
            Assert.That(audit, Is.Not.Null);
            Assert.That(audit.schemaVersion, Is.EqualTo(1));
            Assert.That(
                audit.source,
                Does.StartWith(
                    "https://www.db.yugioh-card.com/yugiohdb/"));
            Assert.That(audit.cards, Has.Length.EqualTo(48));
            Assert.That(shop, Is.Not.Null);
            CardDatabase localizedDatabase = CardDatabase.LoadDefault();

            uint[] actualDeckCodes = CuratedDeckLists.DarkMagicianMain
                .Concat(CuratedDeckLists.DarkMagicianExtra)
                .Distinct()
                .OrderBy(code => code)
                .ToArray();
            uint[] auditedCodes = audit.cards
                .Select(card => uint.Parse(
                    card.code,
                    CultureInfo.InvariantCulture))
                .OrderBy(code => code)
                .ToArray();
            Assert.That(auditedCodes, Is.EqualTo(actualDeckCodes));

            foreach (OfficialAuditCard official in audit.cards)
            {
                Assert.That(official.cid, Is.Not.Empty, official.code);
                Assert.That(
                    official.officialUrl,
                    Does.StartWith(
                        "https://www.db.yugioh-card.com/yugiohdb/"),
                    official.code);

                ShopCard[] shopMatches = shop.cards
                    .Where(card => card.officialId == official.code)
                    .ToArray();
                Assert.That(
                    shopMatches,
                    Has.Length.EqualTo(1),
                    official.code);
                Assert.That(
                    shopMatches[0].displayName,
                    Is.EqualTo(official.officialName),
                    official.code);
                Assert.That(
                    NormalizedSha256(shopMatches[0].description),
                    Is.EqualTo(official.officialTextSha256),
                    $"{official.code} {official.officialName}");

                string catalogText = ReadCatalogEffectText(
                    serializedCatalog,
                    official.code);
                Assert.That(
                    NormalizedSha256(catalogText),
                    Is.EqualTo(NormalizedSha256(
                        localizedDatabase.Get(uint.Parse(
                            official.code,
                            CultureInfo.InvariantCulture)).Description)),
                    $"CardCatalog {official.code} {official.officialName}");
            }
        }

        [Test]
        public void EveryDarkMagicianDeckCardHasCoreDataArtAndScript()
        {
            uint[] main = CuratedDeckLists.DarkMagicianMain;
            uint[] extra = CuratedDeckLists.DarkMagicianExtra;
            Assert.That(main, Has.Length.EqualTo(50));
            Assert.That(extra, Has.Length.EqualTo(15));
            CardDatabase database = CardDatabase.LoadDefault();
            string contentRoot = Path.Combine(
                Application.streamingAssetsPath,
                "Ygo");
            HashSet<uint> localArtCodes = Directory
                .GetFiles(
                    Path.Combine(Application.dataPath, "Cards"),
                    "*.jpg",
                    SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => uint.TryParse(name, out _))
                .Select(uint.Parse)
                .ToHashSet();

            Assert.That(
                DuelContentValidator.FindProblems(
                    database,
                    contentRoot,
                    main,
                    extra),
                Is.Empty);
            foreach (uint code in main.Concat(extra).Distinct())
            {
                CardRecord card = database.Get(code);
                Assert.That(card.Name, Is.Not.Empty, $"{code:00000000}");
                Assert.That(
                    localArtCodes.Contains(code),
                    Is.True,
                    $"{card.Name} has no local presentation artwork.");
                Assert.That(
                    DeckRules.IsExtraDeck(card),
                    Is.EqualTo(extra.Contains(code)),
                    $"{card.Name} is stored in the wrong deck section.");
            }
        }

        [Test]
        public void EveryDarkMagicianDeckScriptInitializesInsideNativeCore()
        {
            uint[] main = CuratedDeckLists.DarkMagicianMain;
            uint[] extra = CuratedDeckLists.DarkMagicianExtra;
            CardDatabase database = CardDatabase.LoadDefault();
            var configuration = new DuelConfiguration
            {
                PlayerDeck = Array.Empty<uint>(),
                OpponentDeck = Array.Empty<uint>(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>(),
                ShuffleMainDecks = false
            };

            using (OcgDuelEngine engine =
                   OcgDuelEngine.CreateDefault(configuration))
            {
                foreach (uint code in main.Concat(extra).Distinct())
                {
                    CardRecord card = database.Get(code);
                    bool requiresScript =
                        DuelContentValidator.RequiresScript(card);
                    uint location = DeckRules.IsExtraDeck(card)
                        ? DuelLocation.Extra
                        : DuelLocation.Deck;
                    int firstLog = engine.NativeLogs.Count;
                    Assert.DoesNotThrow(
                        () => engine.AddCard(0, code, location),
                        $"{card.Name} ({code:00000000})");
                    string[] failures = engine.NativeLogs
                        .Skip(firstLog)
                        .Where(log =>
                            requiresScript &&
                            log.StartsWith(
                                "SCRIPT_MISSING",
                                StringComparison.Ordinal) ||
                            log.StartsWith(
                                "[0]",
                                StringComparison.Ordinal))
                        .ToArray();
                    Assert.That(
                        failures,
                        Is.Empty,
                        $"{card.Name} failed to initialize: " +
                        string.Join(" | ", failures));
                }
            }
        }

        [TestCase(0xD401UL)]
        [TestCase(0xD402UL)]
        [TestCase(0xD403UL)]
        public void CompleteDarkMagicianDeckAdvancesWithoutRetry(
            ulong seed)
        {
            uint[] main = CuratedDeckLists.DarkMagicianMain;
            uint[] extra = CuratedDeckLists.DarkMagicianExtra;
            var configuration = new DuelConfiguration
            {
                Seed = seed,
                StartingLifePoints = 20000,
                PlayerDeck = (uint[])main.Clone(),
                OpponentDeck = (uint[])main.Clone(),
                PlayerExtraDeck = (uint[])extra.Clone(),
                OpponentExtraDeck = (uint[])extra.Clone(),
                ShuffleMainDecks = true,
                SimpleOpponentAi = false
            };
            int turns = 0;
            int retries = 0;
            int unknown = 0;
            int decisions = 0;

            using (OcgDuelEngine engine =
                   OcgDuelEngine.CreateDefault(configuration))
            {
                engine.EventReceived += duelEvent =>
                {
                    if (duelEvent.Message == CoreMessage.NewTurn)
                        turns++;
                    if (duelEvent.Message == CoreMessage.Retry)
                        retries++;
                    if (duelEvent.IsUnknown)
                        unknown++;
                };
                engine.Start();
                while (!engine.IsFinished &&
                       turns < 8 &&
                       decisions++ < 1200)
                {
                    DuelPrompt prompt = engine.CurrentPrompt;
                    Assert.That(
                        prompt,
                        Is.Not.Null,
                        $"seed={seed:X}; turn={turns}; decision={decisions}");
                    DuelChoice choice =
                        DeterministicDuelPolicy.Choose(prompt);
                    Assert.That(choice, Is.Not.Null);
                    engine.SubmitResponse(choice.Response);
                }
            }

            Assert.That(turns, Is.GreaterThanOrEqualTo(8));
            Assert.That(retries, Is.Zero);
            Assert.That(unknown, Is.Zero);
        }

        private static string NormalizedSha256(string text)
        {
            string normalized = (text ?? string.Empty)
                .Normalize(NormalizationForm.FormC)
                .ToLower(new CultureInfo("pt-BR"));
            string compact = new string(
                normalized
                    .Where(character => !char.IsWhiteSpace(character))
                    .ToArray());
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(compact));
                return BitConverter
                    .ToString(digest)
                    .Replace("-", string.Empty);
            }
        }

        private static string ReadCatalogEffectText(
            string serializedCatalog,
            string officialCode)
        {
            string codeMarker = "    officialCardId: " + officialCode;
            int codeIndex = serializedCatalog.IndexOf(
                codeMarker,
                StringComparison.Ordinal);
            Assert.That(
                codeIndex,
                Is.GreaterThanOrEqualTo(0),
                $"CardCatalog entry {officialCode}");

            const string effectMarker = "    effectText: ";
            int effectIndex = serializedCatalog.IndexOf(
                effectMarker,
                codeIndex,
                StringComparison.Ordinal);
            int reviewIndex = serializedCatalog.IndexOf(
                "\n    reviewNotes:",
                effectIndex,
                StringComparison.Ordinal);
            Assert.That(effectIndex, Is.GreaterThan(codeIndex));
            Assert.That(reviewIndex, Is.GreaterThan(effectIndex));

            string scalar = serializedCatalog
                .Substring(
                    effectIndex + effectMarker.Length,
                    reviewIndex - effectIndex - effectMarker.Length)
                .Replace("\r", string.Empty);
            scalar = string.Join(
                " ",
                scalar.Split('\n').Select(line => line.Trim()));
            return DecodeUnityYamlScalar(scalar);
        }

        private static string DecodeUnityYamlScalar(string scalar)
        {
            if (scalar.StartsWith("'", StringComparison.Ordinal) &&
                scalar.EndsWith("'", StringComparison.Ordinal))
            {
                return scalar
                    .Substring(1, scalar.Length - 2)
                    .Replace("''", "'");
            }

            if (!scalar.StartsWith("\"", StringComparison.Ordinal) ||
                !scalar.EndsWith("\"", StringComparison.Ordinal))
            {
                return scalar;
            }
            string value = scalar.Substring(1, scalar.Length - 2);
            value = Regex.Replace(
                value,
                @"\\x([0-9A-Fa-f]{2})",
                match => ((char)Convert.ToInt32(
                    match.Groups[1].Value,
                    16)).ToString());
            value = Regex.Replace(
                value,
                @"\\u([0-9A-Fa-f]{4})",
                match => ((char)Convert.ToInt32(
                    match.Groups[1].Value,
                    16)).ToString());
            return value
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\\"", "\"")
                .Replace("\\\\", "\\");
        }
    }
}
