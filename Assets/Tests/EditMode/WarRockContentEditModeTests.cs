using System;
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
    public sealed class WarRockContentEditModeTests
    {
        private static readonly uint[] Codes =
        {
            19771459,
            47504322,
            83286340
        };

        [Test]
        public void RequestedWarRockCardsHavePortugueseMetadataAndPresentation()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();

            AssertCard(database.Get(19771459), "Caverguerra Gactos", 1900, 1900);
            AssertCard(database.Get(47504322), "Caverguerra Wento", 1800, 1800);
            AssertCard(database.Get(83286340), "Caverguerra Fortia", 1700, 1700);

            foreach (uint code in Codes)
            {
                CardRecord card = database.Get(code);
                Assert.That(card.Description, Does.Contain("Caverguerra"), card.Name);
                Assert.That(card.Strings, Has.Length.EqualTo(16), card.Name);
                Assert.That(card.Strings[0], Is.Not.Empty, card.Name);
                Assert.That(card.Setcodes, Does.Contain((ushort)0x0161), card.Name);
                Assert.That(visuals.TryGet(code, out CardVisualData visual), Is.True, card.Name);
                Assert.That(File.Exists(visuals.ArtPath(code)), Is.True, card.Name);
                Assert.That(visual.scriptFile, Is.EqualTo($"c{code}.lua"), card.Name);
                Assert.That(
                    File.Exists(Path.Combine(
                        Application.streamingAssetsPath,
                        "Ygo",
                        "Scripts",
                        "official",
                        visual.scriptFile)),
                    Is.True,
                    card.Name);
            }
        }

        [Test]
        public void RequestedWarRockCardsRegisterInNativeCoreWithoutScriptErrors()
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
                foreach (uint code in Codes)
                {
                    int firstLog = engine.NativeLogs.Count;
                    Assert.DoesNotThrow(
                        () => engine.AddCard(0, code, DuelLocation.Deck),
                        database.Get(code).Name);
                    string[] failures = engine.NativeLogs
                        .Skip(firstLog)
                        .Where(log =>
                            log.StartsWith("SCRIPT_MISSING", StringComparison.Ordinal) ||
                            log.StartsWith("[0]", StringComparison.Ordinal))
                        .ToArray();
                    Assert.That(failures, Is.Empty, string.Join(" | ", failures));
                }
            }
        }

        [Test]
        public void RequestedWarRockCardsSurviveSixTurnsThroughNativeCore()
        {
            uint[] deck = Enumerable.Range(0, 40)
                .Select(index => Codes[index % Codes.Length])
                .ToArray();
            var configuration = new DuelConfiguration
            {
                Seed = 0xCACE000019771459UL,
                StartingLifePoints = 20000,
                PlayerDeck = (uint[])deck.Clone(),
                OpponentDeck = (uint[])deck.Clone(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>(),
                SimpleOpponentAi = true,
                ShuffleMainDecks = false
            };
            int turns = 0;
            int retries = 0;
            int unknownMessages = 0;
            var seen = new System.Collections.Generic.HashSet<uint>();

            using (OcgDuelEngine engine = OcgDuelEngine.CreateDefault(configuration))
            {
                engine.EventReceived += duelEvent =>
                {
                    if (duelEvent.Message == CoreMessage.NewTurn) turns++;
                    if (duelEvent.Message == CoreMessage.Retry) retries++;
                    if (duelEvent.IsUnknown) unknownMessages++;
                    if (duelEvent.Code != 0) seen.Add(duelEvent.Code);
                    if (duelEvent.Codes != null)
                    {
                        foreach (uint code in duelEvent.Codes) seen.Add(code);
                    }
                };
                engine.Start();
                int decisions = 0;
                while (!engine.IsFinished && turns < 6 && decisions++ < 900)
                {
                    Assert.That(engine.CurrentPrompt, Is.Not.Null,
                        "Caverguerra deixou o Core aguardando uma escolha não tipada.");
                    engine.SubmitResponse(
                        DeterministicDuelPolicy.Choose(engine.CurrentPrompt).Response);
                }
            }

            Assert.That(turns, Is.GreaterThanOrEqualTo(6));
            Assert.That(retries, Is.Zero);
            Assert.That(unknownMessages, Is.Zero);
            Assert.That(seen.Intersect(Codes).Count(), Is.GreaterThanOrEqualTo(2));
        }

        private static void AssertCard(
            CardRecord card,
            string expectedName,
            int expectedAttack,
            int expectedDefense)
        {
            Assert.That(card.Name, Is.EqualTo(expectedName));
            Assert.That(card.Type & 0x21U, Is.EqualTo(0x21U), card.Name);
            Assert.That(card.Attribute, Is.EqualTo(0x01U), card.Name);
            Assert.That(card.Race, Is.EqualTo(0x01UL), card.Name);
            Assert.That(card.Level, Is.EqualTo(4), card.Name);
            Assert.That(card.Attack, Is.EqualTo(expectedAttack), card.Name);
            Assert.That(card.Defense, Is.EqualTo(expectedDefense), card.Name);
        }
    }
}
