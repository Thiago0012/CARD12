using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ExtraDeckSummonEditModeTests
    {
        private const uint BlueEyesWhiteDragon = 89631139;
        private const uint DarkMagician = 46986414;
        private const uint ApprenticeIllusionMagician = 30603688;
        private const uint EffectVeiler = 97268402;
        private const uint SummonedSkull = 70781052;
        private const uint Polymerization = 24094653;
        private const uint TheDarkMagicians = 50237654;
        private const uint BlackRoseDragon = 73580471;
        private const uint EbonIllusionMagician = 96471335;
        private const uint RelinquishedAnima = 94259633;
        private const uint FaceUpAttack = 0x1;

        [Test]
        public void FusionSummonUsesPrintedMaterialsAndPolymerization()
        {
            AssertExtraSummon(
                TheDarkMagicians,
                new[] { DarkMagician, ApprenticeIllusionMagician },
                Polymerization);
        }

        [Test]
        public void SynchroSummonUsesTunerAndExactCombinedLevel()
        {
            AssertExtraSummon(
                BlackRoseDragon,
                new[] { EffectVeiler, SummonedSkull });
        }

        [Test]
        public void XyzSummonUsesTwoLevelSevenSpellcasters()
        {
            AssertExtraSummon(
                EbonIllusionMagician,
                new[] { DarkMagician, DarkMagician });
        }

        [Test]
        public void LinkSummonUsesALegalLevelOneMaterial()
        {
            AssertExtraSummon(
                RelinquishedAnima,
                new[] { EffectVeiler });
        }

        [Test]
        public void EveryCompiledCardHasRuntimeDataAndRequiredScripts()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            string root = Path.Combine(Application.streamingAssetsPath, "Ygo");
            string[] problems = DuelContentValidator.FindProblems(
                database,
                root,
                database.Cards.Select(card => card.Code));

            Assert.That(
                problems,
                Is.Empty,
                "Every compiled card that can enter a duel must have data and its required Core script. " +
                string.Join(" | ", problems));
        }

        private static void AssertExtraSummon(
            uint extraDeckMonster,
            IReadOnlyList<uint> faceUpMaterials,
            uint activatingSpell = 0)
        {
            uint[] fillerDeck = Enumerable.Repeat(BlueEyesWhiteDragon, 40)
                .ToArray();
            var configuration = new DuelConfiguration
            {
                StartingHand = 0,
                Seed = 0xE17AD3C000000001UL ^ extraDeckMonster,
                ShuffleMainDecks = false,
                SimpleOpponentAi = false,
                PlayerDeck = (uint[])fillerDeck.Clone(),
                OpponentDeck = (uint[])fillerDeck.Clone(),
                PlayerExtraDeck = new[] { extraDeckMonster },
                OpponentExtraDeck = Array.Empty<uint>()
            };

            string root = Path.Combine(Application.streamingAssetsPath, "Ygo");
            var events = new List<DuelEvent>();
            int retries = 0;
            int unknown = 0;
            int decisions = 0;
            bool summonStarted = false;
            bool movedFromExtra = false;

            using (var engine = new OcgDuelEngine(
                       CardDatabase.LoadDefault(),
                       root,
                       configuration))
            {
                for (int index = 0; index < faceUpMaterials.Count; index++)
                {
                    engine.AddCardAt(
                        0,
                        faceUpMaterials[index],
                        DuelLocation.MonsterZone,
                        (uint)index,
                        FaceUpAttack);
                }
                if (activatingSpell != 0)
                {
                    engine.AddCardAt(
                        0,
                        activatingSpell,
                        DuelLocation.Hand,
                        0,
                        FaceUpAttack);
                }

                engine.EventReceived += duelEvent =>
                {
                    events.Add(duelEvent);
                    if (duelEvent.Message == CoreMessage.Retry) retries++;
                    if (duelEvent.IsUnknown) unknown++;
                    if (duelEvent.Code == extraDeckMonster &&
                        duelEvent.Message == CoreMessage.SpecialSummoning)
                    {
                        summonStarted = true;
                    }
                    if (duelEvent.Code == extraDeckMonster &&
                        duelEvent.Message == CoreMessage.Move &&
                        duelEvent.Previous != null &&
                        duelEvent.Previous.Location == DuelLocation.Extra &&
                        duelEvent.Current != null &&
                        duelEvent.Current.Location == DuelLocation.MonsterZone)
                    {
                        movedFromExtra = true;
                    }
                };

                engine.Start();
                while (!engine.IsFinished &&
                       !movedFromExtra &&
                       decisions++ < 80)
                {
                    DuelPrompt prompt = engine.CurrentPrompt;
                    Assert.That(
                        prompt,
                        Is.Not.Null,
                        "The Core stalled before completing the Extra Deck summon. " +
                        Trace(events, engine.NativeLogs));
                    DuelChoice choice = ChooseSummonPath(
                        prompt,
                        extraDeckMonster,
                        activatingSpell);
                    Assert.That(choice, Is.Not.Null);
                    engine.SubmitResponse(choice.Response);
                }

                Assert.That(
                    retries,
                    Is.Zero,
                    "The Core rejected a controlled legal summon. " +
                    Trace(events, engine.NativeLogs));
                Assert.That(
                    unknown,
                    Is.Zero,
                    "The Core emitted an untyped message during a controlled legal summon. " +
                    Trace(events, engine.NativeLogs));
                Assert.That(
                    summonStarted || movedFromExtra,
                    Is.True,
                    "The expected Extra Deck monster was never announced. " +
                    Trace(events, engine.NativeLogs));
                Assert.That(
                    movedFromExtra,
                    Is.True,
                    "The expected monster did not move from the Extra Deck to a Monster Zone. " +
                    Trace(events, engine.NativeLogs));
            }
        }

        private static DuelChoice ChooseSummonPath(
            DuelPrompt prompt,
            uint extraDeckMonster,
            uint activatingSpell)
        {
            if (prompt.Message == CoreMessage.SelectIdleCommand)
            {
                if (activatingSpell != 0)
                {
                    DuelChoice activation = prompt.Choices.FirstOrDefault(choice =>
                        choice.CardCode == activatingSpell &&
                        choice.Label.StartsWith("Ativar", StringComparison.OrdinalIgnoreCase));
                    if (activation != null)
                        return activation;
                }

                DuelChoice specialSummon = prompt.Choices.FirstOrDefault(choice =>
                    choice.CardCode == extraDeckMonster &&
                    choice.Label.IndexOf("especial", StringComparison.OrdinalIgnoreCase) >= 0);
                if (specialSummon != null)
                    return specialSummon;
            }

            if ((prompt.Message == CoreMessage.SelectYesNo ||
                 prompt.Message == CoreMessage.SelectEffectYesNo) &&
                prompt.Choices.Count > 0)
            {
                DuelChoice yes = prompt.Choices.FirstOrDefault(choice =>
                    choice.Response != null &&
                    choice.Response.Length == 4 &&
                    choice.Response[0] == 1 &&
                    choice.Response[1] == 0 &&
                    choice.Response[2] == 0 &&
                    choice.Response[3] == 0);
                if (yes != null)
                    return yes;
            }

            return DeterministicDuelPolicy.Choose(prompt);
        }

        private static string Trace(
            IEnumerable<DuelEvent> events,
            IReadOnlyList<string> nativeLogs)
        {
            return "events=" + string.Join(
                       " | ",
                       events.TakeLast(12).Select(duelEvent =>
                           $"{duelEvent.RawMessage}:{duelEvent.Message}:{duelEvent.Code}:{duelEvent.Detail}")) +
                   "; native=" + string.Join(" | ", nativeLogs.TakeLast(6));
        }
    }
}