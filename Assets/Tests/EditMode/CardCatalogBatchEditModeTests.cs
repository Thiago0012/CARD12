using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Interop;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class CardCatalogBatchEditModeTests
    {
        private const int BatchSize = 25;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        public void ImportedBatchHasDataArtAndResolvedScripts(int batchIndex)
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            CardVisualData[] batch = visuals.Cards
                .OrderBy(card => card.officialCode)
                .Skip(batchIndex * BatchSize)
                .Take(BatchSize)
                .ToArray();

            int expectedCount = Math.Min(
                BatchSize,
                visuals.Count - batchIndex * BatchSize);
            Assert.That(expectedCount, Is.GreaterThan(0));
            Assert.That(batch, Has.Length.EqualTo(expectedCount));
            foreach (CardVisualData visual in batch)
            {
                CardRecord record = database.Get(visual.officialCode);
                Assert.That(record.Name, Is.Not.Empty, visual.officialCode.ToString());
                Assert.That(
                    File.Exists(visuals.ArtPath(visual.officialCode)),
                    Is.True,
                    $"Art is missing for {visual.officialCode:00000000}.");
                Assert.That(
                    visual.riskLevel,
                    Is.EqualTo("A").Or.EqualTo("B").Or.EqualTo("C"),
                    record.Name);
                if (visual.scriptStatus != "not_required_no_effect")
                {
                    Assert.That(visual.scriptFile, Is.Not.Empty, record.Name);
                    string scriptFolder = visual.scriptStatus == "via_alias"
                        ? "CustomScripts"
                        : Path.Combine("Scripts", "official");
                    string scriptFile = visual.scriptStatus == "via_alias"
                        ? $"c{visual.officialCode}.lua"
                        : visual.scriptFile;
                    Assert.That(
                        File.Exists(Path.Combine(
                            Application.streamingAssetsPath,
                            "Ygo",
                            scriptFolder,
                            scriptFile)),
                        Is.True,
                        $"Resolved script is missing for {record.Name}.");
                }
            }
        }

        [Test]
        public void PresentationCatalogHasUniqueEntriesAndNoRuleMethods()
        {
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            CardVisualData[] cards = visuals.Cards.ToArray();
            Assert.That(visuals.Count, Is.EqualTo(229));
            Assert.That(
                cards.Select(card => card.officialCode).Distinct().Count(),
                Is.EqualTo(229));
            Assert.That(
                typeof(CardVisualData).GetMethod("ResolveEffect"),
                Is.Null,
                "Presentation metadata must never resolve game rules.");
            Assert.That(
                cards.Count(card => card.riskLevel == "A") +
                cards.Count(card => card.riskLevel == "B") +
                cards.Count(card => card.riskLevel == "C"),
                Is.EqualTo(229));
        }

        [Test]
        public void EveryCompiledCoreCardRegistersWithNativeCoreLifecycle()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            var configuration = new DuelConfiguration
            {
                PlayerDeck = Array.Empty<uint>(),
                OpponentDeck = Array.Empty<uint>(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>(),
                ShuffleMainDecks = false
            };
            using (OcgDuelEngine engine = OcgDuelEngine.CreateDefault(configuration))
            {
                foreach (CardRecord card in database.Cards.OrderBy(card => card.Code))
                {
                    bool requiresScript =
                        DuelContentValidator.RequiresScript(card);
                    uint location = DeckRules.IsExtraDeck(card)
                        ? DuelLocation.Extra
                        : DuelLocation.Deck;
                    int firstNewLog = engine.NativeLogs.Count;
                    Assert.DoesNotThrow(
                        () => engine.AddCard(0, card.Code, location),
                        card.Name);
                    string[] scriptFailures = engine.NativeLogs
                        .Skip(firstNewLog)
                        .Where(log =>
                            (requiresScript &&
                             log.StartsWith(
                                 "SCRIPT_MISSING",
                                 StringComparison.Ordinal)) ||
                            log.StartsWith(
                                "[0]",
                                StringComparison.Ordinal))
                        .ToArray();
                    Assert.That(
                        scriptFailures,
                        Is.Empty,
                        $"{card.Name} ({card.Code:00000000}) rejected its " +
                        $"resolved script: {string.Join(" | ", scriptFailures)}");
                }
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        public void CatalogBatchSurvivesTenTurnsThroughNativeCore(
            int batchIndex)
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            uint[] batch = visuals.Cards
                .OrderBy(card => card.officialCode)
                .Skip(batchIndex * BatchSize)
                .Take(BatchSize)
                .Select(card => card.officialCode)
                .ToArray();
            var main = new List<uint>();
            var extra = new List<uint>();
            foreach (uint code in batch)
            {
                CardRecord card = database.Get(code);
                if (DeckRules.IsExtraDeck(card)) extra.Add(code);
                else main.Add(code);
            }
            Assert.That(main, Is.Not.Empty, $"Batch {batchIndex}");
            int originalMainCount = main.Count;
            for (int index = 0; main.Count < 40; index++)
            {
                main.Add(main[index % originalMainCount]);
            }

            var configuration = new DuelConfiguration
            {
                Seed = 0x200CA7A100000000UL + (uint)batchIndex,
                StartingLifePoints = 20000,
                PlayerDeck = main.Take(60).ToArray(),
                OpponentDeck = main.Take(60).ToArray(),
                PlayerExtraDeck = extra.Take(15).ToArray(),
                OpponentExtraDeck = extra.Take(15).ToArray(),
                SimpleOpponentAi = true,
                ShuffleMainDecks = false
            };
            int turns = 0;
            int retries = 0;
            var unknown = new List<byte>();
            using (OcgDuelEngine engine =
                   OcgDuelEngine.CreateDefault(configuration))
            {
                engine.EventReceived += duelEvent =>
                {
                    if (duelEvent.Message == CoreMessage.NewTurn) turns++;
                    if (duelEvent.Message == CoreMessage.Retry) retries++;
                    if (duelEvent.IsUnknown)
                        unknown.Add(duelEvent.RawMessage);
                };
                engine.Start();
                int decisions = 0;
                while (!engine.IsFinished &&
                       turns < 10 &&
                       decisions++ < 1200)
                {
                    Assert.That(
                        engine.CurrentPrompt,
                        Is.Not.Null,
                        $"Batch {batchIndex} stopped at an untyped prompt.");
                    DuelChoice choice =
                        DeterministicDuelPolicy.Choose(engine.CurrentPrompt);
                    engine.SubmitResponse(choice.Response);
                }
                Assert.That(
                    turns,
                    Is.GreaterThanOrEqualTo(10),
                    $"Batch {batchIndex} did not survive ten turns.");
                Assert.That(
                    retries,
                    Is.Zero,
                    $"Batch {batchIndex} generated an invalid response.");
                Assert.That(
                    unknown,
                    Is.Empty,
                    $"Batch {batchIndex} emitted unknown messages: " +
                    string.Join(",", unknown));
            }
        }
    }
}
