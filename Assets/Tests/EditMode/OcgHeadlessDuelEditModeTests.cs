using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class OcgHeadlessDuelEditModeTests
    {
        [Test]
        public void RuntimeDuelSeedsDoNotRepeat()
        {
            ulong[] seeds = Enumerable.Range(0, 64)
                .Select(_ => DuelConfiguration.FreshSeed())
                .ToArray();
            Assert.That(seeds.Distinct().Count(), Is.EqualTo(seeds.Length));
        }

        [Test]
        public void SameSeedAdvancesAFullTurnDeterministicallyAndDisposes()
        {
            string first = RunUntilSecondTurn(0x1020304050607080UL);
            string second = RunUntilSecondTurn(0x1020304050607080UL);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Does.Contain("40:1"));
        }

        [Test]
        public void TwelveCardVerticalSliceCanReachAWinner()
        {
            int decisions = 0;
            int distinctFeaturedCards = 0;
            int turns = 0;
            int retries = 0;
            int wins = 0;
            var seen = new HashSet<uint>();
            var prompts = new Dictionary<CoreMessage, int>();
            using (OcgDuelEngine engine = OcgDuelEngine.CreateDefault(
                       DuelConfiguration.VerticalSlice(0x5A17C0DE12345678UL)))
            {
                engine.EventReceived += duelEvent =>
                {
                    if (duelEvent.Message == CoreMessage.NewTurn) turns++;
                    if (duelEvent.Message == CoreMessage.Retry) retries++;
                    if (duelEvent.Message == CoreMessage.Win) wins++;
                    if (duelEvent.Prompt != null)
                    {
                        prompts.TryGetValue(duelEvent.Prompt.Message, out int count);
                        prompts[duelEvent.Prompt.Message] = count + 1;
                    }
                    if (duelEvent.Code != 0) seen.Add(duelEvent.Code);
                    if (duelEvent.Codes != null)
                    {
                        foreach (uint code in duelEvent.Codes) seen.Add(code);
                    }
                };
                engine.Start();
                while (!engine.IsFinished && decisions++ < 1600)
                {
                    Assert.That(engine.CurrentPrompt, Is.Not.Null,
                        $"Untyped awaiting message after {decisions} decisions.");
                    engine.SubmitResponse(DeterministicDuelPolicy.Choose(engine.CurrentPrompt).Response);
                }
                string metrics = $"turns={turns}, decisions={decisions}, retries={retries}, wins={wins}, " +
                                 $"status={engine.Status}, prompt={engine.CurrentPrompt?.Message}, " +
                                 $"prompts={string.Join(",", prompts)}";
                Assert.That(engine.IsFinished, Is.True, "Vertical slice did not reach a winner. " + metrics);
            }
            uint[] featured =
            {
                89631139, 46986414, 74131780, 71413901, 7089711, 93920745,
                97268402, 53129443, 5318639, 44095762, 11901678, 77585513
            };
            foreach (uint code in featured)
            {
                if (seen.Contains(code)) distinctFeaturedCards++;
            }
            Assert.That(distinctFeaturedCards, Is.GreaterThanOrEqualTo(4),
                "The automated duel did not exercise a representative part of the 12-card slice.");
        }

        [Test]
        public void TacticalOpponentReceivesItsTurnAndSubmitsLegalActions()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            var presentation = new DuelPresentationState(database);
            DuelConfiguration configuration =
                DuelConfiguration.VerticalSlice(0xA17A17A17UL);
            configuration.SimpleOpponentAi = false;
            int turns = 0;
            int opponentDecisions = 0;
            int retries = 0;
            string lastDecision = "nenhuma";
            int constructiveOpponentDecisions = 0;
            var opponent = new TacticalOpponentAgent();

            using (OcgDuelEngine engine =
                   OcgDuelEngine.CreateDefault(configuration))
            {
                engine.EventReceived += duelEvent =>
                {
                    presentation.Apply(duelEvent);
                    if (duelEvent.Message == CoreMessage.NewTurn) turns++;
                    if (duelEvent.Message == CoreMessage.Retry) retries++;
                };
                engine.Start();
                int decisions = 0;
                while (!engine.IsFinished &&
                       turns < 6
                       && decisions++ < 520)
                {
                    DuelPrompt prompt = engine.CurrentPrompt;
                    string eventTrace = string.Join(
                        " | ",
                        engine.EventHistory
                            .TakeLast(8)
                            .Select(duelEvent =>
                                $"{duelEvent.RawMessage}:{duelEvent.Message}:" +
                                duelEvent.Detail));
                    string nativeTrace = string.Join(
                        " | ",
                        engine.NativeLogs.TakeLast(4));
                    Assert.That(
                        prompt,
                        Is.Not.Null,
                        "The duel stalled without a typed decision. " +
                        $"status={engine.Status}, turns={turns}, " +
                        $"decisions={decisions}, opponent={opponentDecisions}, " +
                        $"last={lastDecision}, events={eventTrace}, " +
                        $"native={nativeTrace}");
                    DuelChoice choice;
                    if (prompt.Player == 1)
                    {
                        opponentDecisions++;
                        float delay =
                            TacticalOpponentPolicy.DecisionDelay(prompt);
                        Assert.That(
                            delay,
                            Is.InRange(0.20f, 1.10f),
                            "Opponent presentation pacing must remain responsive.");
                        choice = opponent.Choose(
                            prompt,
                            presentation,
                            database);
                        if (choice != null &&
                            choice.Label != null &&
                            choice.Label.IndexOf(
                                "Encerrar",
                                StringComparison.OrdinalIgnoreCase) < 0 &&
                            choice.Label.IndexOf(
                                "Não",
                                StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            constructiveOpponentDecisions++;
                        }
                    }
                    else
                    {
                        choice = DeterministicDuelPolicy.Choose(prompt);
                    }
                    Assert.That(choice, Is.Not.Null);
                    lastDecision =
                        $"{prompt.Message}:p{prompt.Player}:" +
                        $"{choice.Label}:code={choice.CardCode}:" +
                        $"location={choice.Controller}/{choice.Location}/" +
                        $"{choice.Sequence}:response=" +
                        BitConverter.ToString(choice.Response);
                    engine.SubmitResponse(choice.Response);
                }
            }

            Assert.That(turns, Is.GreaterThanOrEqualTo(6));
            Assert.That(opponentDecisions, Is.GreaterThan(2));
            Assert.That(constructiveOpponentDecisions, Is.GreaterThan(0));
            Assert.That(retries, Is.Zero);
        }

        private static string RunUntilSecondTurn(ulong seed)
        {
            uint[] deck = new uint[40];
            for (int i = 0; i < deck.Length; i++) deck[i] = 1784619;
            var configuration = new DuelConfiguration
            {
                Seed = seed,
                PlayerDeck = (uint[])deck.Clone(),
                OpponentDeck = (uint[])deck.Clone(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>(),
                SimpleOpponentAi = true
            };
            string root = Path.Combine(Application.streamingAssetsPath, "Ygo");
            var signature = new List<string>();
            int turns = 0;
            using (var engine = new OcgDuelEngine(CardDatabase.LoadDefault(), root, configuration))
            {
                engine.EventReceived += duelEvent =>
                {
                    if (duelEvent.Message == CoreMessage.NewTurn) turns++;
                    if (duelEvent.Message == CoreMessage.NewTurn ||
                        duelEvent.Message == CoreMessage.NewPhase ||
                        duelEvent.Message == CoreMessage.Draw ||
                        duelEvent.Message == CoreMessage.Move)
                    {
                        signature.Add($"{duelEvent.RawMessage}:{duelEvent.Player}:{duelEvent.Value}:{duelEvent.Code}");
                    }
                };
                engine.Start();
                int decisions = 0;
                while (!engine.IsFinished && turns < 2 && decisions++ < 80)
                {
                    Assert.That(engine.CurrentPrompt, Is.Not.Null,
                        "ocgcore awaited a message for which the decoder did not expose a typed prompt.");
                    engine.SubmitResponse(DeterministicDuelPolicy.Choose(engine.CurrentPrompt).Response);
                }
                Assert.That(turns, Is.GreaterThanOrEqualTo(2), "The repeated seed did not advance one full turn.");
            }
            return string.Join("|", signature);
        }
    }
}
