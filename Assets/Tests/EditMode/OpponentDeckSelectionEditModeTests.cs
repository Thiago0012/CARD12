using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ArcaneDuel.Game;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Core;
using System.IO;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class OpponentDeckSelectionEditModeTests
    {
        [Test]
        public void OpponentRosterContainsOnlyCompleteCuratedDecks()
        {
            Type catalog = FindType("ArcaneArena.Frontend.DeckShopCatalog");
            MethodInfo createRoster = catalog.GetMethod(
                "CreateOpponentRoster",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(createRoster, Is.Not.Null);

            object[] decks = ((IEnumerable)createRoster.Invoke(null, null))
                .Cast<object>()
                .ToArray();
            Assert.That(decks, Has.Length.EqualTo(7));

            string[] expectedSizes =
            {
                "40+15",
                "50+15",
                "40+4",
                "40+2",
                "40+15",
                "40+11",
                "40+15"
            };
            string[] actualSizes = decks
                .Select(deck =>
                    $"{Cards(deck, "mainDeckCardIds").Count}+" +
                    $"{Cards(deck, "extraDeckCardIds").Count}")
                .ToArray();
            Assert.That(actualSizes, Is.EquivalentTo(expectedSizes));
            Assert.That(
                decks.Select(deck => Text(deck, "deckId")).Distinct().Count(),
                Is.EqualTo(decks.Length));
            Assert.That(
                decks.All(deck => Cards(deck, "mainDeckCardIds").Count >= 40),
                Is.True);
        }

        [Test]
        public void RandomOpponentSelectsAWholeThemeAndAvoidsThePlayersDeck()
        {
            Type catalog = FindType("ArcaneArena.Frontend.DeckShopCatalog");
            MethodInfo createRoster = catalog.GetMethod(
                "CreateOpponentRoster",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo choose = catalog.GetMethod(
                "ChooseOpponentDeck",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(createRoster, Is.Not.Null);
            Assert.That(choose, Is.Not.Null);

            object[] roster = ((IEnumerable)createRoster.Invoke(null, null))
                .Cast<object>()
                .ToArray();
            var validSignatures = new HashSet<string>(
                roster.Select(Signature),
                StringComparer.Ordinal);
            string playerDeckId = Text(roster[0], "deckId");
            var selectedDeckIds = new HashSet<string>(StringComparer.Ordinal);

            for (ulong selector = 0; selector < 24; selector++)
            {
                object selected = choose.Invoke(
                    null,
                    new object[] { playerDeckId, selector });
                Assert.That(selected, Is.Not.Null);
                Assert.That(Text(selected, "deckId"), Is.Not.EqualTo(playerDeckId));
                Assert.That(
                    validSignatures.Contains(Signature(selected)),
                    Is.True,
                    "A random opponent must be one complete curated list, never a mixture of archetypes.");
                selectedDeckIds.Add(Text(selected, "deckId"));
            }

            Assert.That(
                selectedDeckIds.Count,
                Is.EqualTo(6),
                "Every complete alternative theme should be reachable by the random selector.");
        }

        [Test]
        public void EveryCuratedDeckPairStartsAndProcessesLegalCoreTurns()
        {
            Type catalog = FindType("ArcaneArena.Frontend.DeckShopCatalog");
            MethodInfo createRoster = catalog.GetMethod(
                "CreateOpponentRoster",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(createRoster, Is.Not.Null);
            object[] roster = ((IEnumerable)createRoster.Invoke(null, null))
                .Cast<object>()
                .ToArray();

            CardDatabase database = CardDatabase.LoadDefault();
            string ygoRoot = Path.Combine(Application.streamingAssetsPath, "Ygo");
            int matchupIndex = 0;
            foreach (object playerDeck in roster)
            {
                foreach (object opponentDeck in roster)
                {
                    uint[] playerMain = CardCodes(playerDeck, "mainDeckCardIds");
                    uint[] playerExtra = CardCodes(playerDeck, "extraDeckCardIds");
                    uint[] opponentMain = CardCodes(opponentDeck, "mainDeckCardIds");
                    uint[] opponentExtra = CardCodes(opponentDeck, "extraDeckCardIds");
                    string matchup =
                        Text(playerDeck, "displayName") + " vs " +
                        Text(opponentDeck, "displayName");
                    string[] unsupported = DuelContentValidator.FindProblems(
                        database,
                        ygoRoot,
                        playerMain,
                        playerExtra,
                        opponentMain,
                        opponentExtra);
                    Assert.That(
                        unsupported,
                        Is.Empty,
                        matchup + " contains unsupported content: " +
                        string.Join(" | ", unsupported));

                    var configuration = new DuelConfiguration
                    {
                        StartingLifePoints = 20000,
                        Seed = 0xC0DEC0DE00000000UL + (uint)matchupIndex,
                        ShuffleMainDecks = true,
                        SimpleOpponentAi = false,
                        PlayerDeck = playerMain,
                        PlayerExtraDeck = playerExtra,
                        OpponentDeck = opponentMain,
                        OpponentExtraDeck = opponentExtra
                    };
                    var presentation = new DuelPresentationState(database);
                    var opponentAgent = new TacticalOpponentAgent();
                    int turns = 0;
                    int retries = 0;
                    int unknown = 0;
                    int decisions = 0;
                    using (OcgDuelEngine engine =
                           OcgDuelEngine.CreateDefault(configuration))
                    {
                        engine.EventReceived += duelEvent =>
                        {
                            presentation.Apply(duelEvent);
                            if (duelEvent.Message == CoreMessage.NewTurn) turns++;
                            if (duelEvent.Message == CoreMessage.Retry) retries++;
                            if (duelEvent.IsUnknown) unknown++;
                        };
                        engine.Start();
                        while (!engine.IsFinished &&
                               turns < 4 &&
                               decisions++ < 900)
                        {
                            DuelPrompt prompt = engine.CurrentPrompt;
                            Assert.That(
                                prompt,
                                Is.Not.Null,
                                matchup + " stalled without a typed prompt. " +
                                Trace(engine));
                            DuelChoice choice = prompt.Player == 1
                                ? opponentAgent.Choose(
                                    prompt,
                                    presentation,
                                    database)
                                : DeterministicDuelPolicy.Choose(prompt);
                            Assert.That(choice, Is.Not.Null, matchup);
                            engine.SubmitResponse(choice.Response);
                        }
                        Assert.That(
                            engine.IsFinished || turns >= 4,
                            Is.True,
                            matchup + " did not progress through four turns. " +
                            Trace(engine));
                        Assert.That(retries, Is.Zero, matchup);
                        Assert.That(unknown, Is.Zero, matchup);
                    }
                    matchupIndex++;
                }
            }
            Assert.That(matchupIndex, Is.EqualTo(49));
        }

        private static uint[] CardCodes(object deck, string fieldName)
        {
            return Cards(deck, fieldName)
                .Cast<object>()
                .Select(value => uint.Parse(value.ToString()))
                .ToArray();
        }

        private static string Trace(OcgDuelEngine engine)
        {
            return "events=" + string.Join(
                       " | ",
                       engine.EventHistory.TakeLast(10).Select(duelEvent =>
                           $"{duelEvent.RawMessage}:{duelEvent.Message}:{duelEvent.Code}:{duelEvent.Detail}")) +
                   "; native=" + string.Join(
                       " | ",
                       engine.NativeLogs.TakeLast(6));
        }
        private static Type FindType(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, $"Runtime type {fullName} was not loaded.");
            return type;
        }

        private static string Signature(object deck)
        {
            return Text(deck, "deckId") + "|" +
                   string.Join(",", Cards(deck, "mainDeckCardIds")) + "|" +
                   string.Join(",", Cards(deck, "extraDeckCardIds"));
        }

        private static string Text(object source, string fieldName)
        {
            return source.GetType().GetField(fieldName)?.GetValue(source) as string
                   ?? string.Empty;
        }

        private static IList Cards(object source, string fieldName)
        {
            return source.GetType().GetField(fieldName)?.GetValue(source) as IList
                   ?? Array.Empty<string>();
        }
    }
}
