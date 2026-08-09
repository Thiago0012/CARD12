using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
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
        private const uint BlueEyesChaosMaxDragon = 55410871;
        private const uint ChaosForm = 21082832;
        private const uint OddEyesPendulumDragon = 16178681;
        private const uint DddSuperDoomKingBrightArmageddon = 72402069;
        private const uint AshBlossom = 14558127;
        private const uint FaceUpAttack = 0x1;

        [Test]
        public void FusionSummonUsesPrintedMaterialsAndPolymerization()
        {
            AssertExtraSummon(
                TheDarkMagicians,
                new[] { DarkMagician },
                Polymerization,
                new[] { ApprenticeIllusionMagician });
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
        public void RitualSummonUsesTheOfficialSpellAndExactLevelMaterial()
        {
            uint[] fillerDeck = Enumerable.Repeat(BlueEyesWhiteDragon, 40)
                .ToArray();
            var configuration = new DuelConfiguration
            {
                StartingHand = 0,
                Seed = 0xA17A1C0A50000001UL,
                ShuffleMainDecks = false,
                SimpleOpponentAi = false,
                PlayerDeck = (uint[])fillerDeck.Clone(),
                OpponentDeck = (uint[])fillerDeck.Clone(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>()
            };

            string root = Path.Combine(Application.streamingAssetsPath, "Ygo");
            CardDatabase database = CardDatabase.LoadDefault();
            var state = new DuelPresentationState(database);
            var events = new List<DuelEvent>();
            var responses = new List<(ulong RequestId, CoreMessage Message, byte[] Payload)>();
            bool ritualMoved = false;
            int decisions = 0;

            using (var engine = new OcgDuelEngine(
                       database,
                       root,
                       configuration))
            {
                engine.AddCardAt(
                    0,
                    BlueEyesChaosMaxDragon,
                    DuelLocation.Hand,
                    0,
                    0);
                engine.AddCardAt(
                    0,
                    ChaosForm,
                    DuelLocation.Hand,
                    1,
                    0);
                engine.AddCardAt(
                    0,
                    BlueEyesWhiteDragon,
                    DuelLocation.Hand,
                    2,
                    0);
                engine.EventReceived += duelEvent =>
                {
                    events.Add(duelEvent);
                    state.Apply(duelEvent);
                    if (duelEvent.Message == CoreMessage.Move &&
                        duelEvent.Code == BlueEyesChaosMaxDragon &&
                        duelEvent.Previous?.Location == DuelLocation.Hand &&
                        duelEvent.Current?.Location == DuelLocation.MonsterZone)
                    {
                        ritualMoved = true;
                    }
                };

                engine.Start();
                while (!engine.IsFinished && !ritualMoved && decisions++ < 80)
                {
                    DuelPrompt prompt = engine.CurrentPrompt;
                    Assert.That(prompt, Is.Not.Null, Trace(events, engine.NativeLogs));
                    DuelChoice choice = ChooseRitualPath(prompt);
                    Assert.That(choice, Is.Not.Null, Trace(events, engine.NativeLogs));
                    Assert.That(
                        prompt.Choices.Contains(choice),
                        Is.True,
                        "Every ritual response must originate in the current Core prompt.");
                    responses.Add((
                        prompt.RequestId,
                        prompt.Message,
                        (byte[])choice.Response.Clone()));
                    engine.SubmitResponse(choice.Response);
                }

                Assert.That(
                    engine.TryCaptureFieldSnapshot(out OcgFieldSnapshot snapshot),
                    Is.True);
                state.ReconcileFromCore(snapshot);
            }

            AssertSummonTrace(
                events,
                responses,
                state,
                BlueEyesChaosMaxDragon,
                "Ritual");
            Assert.That(
                events.Any(duelEvent =>
                    duelEvent.Message == CoreMessage.Move &&
                    duelEvent.Code == BlueEyesWhiteDragon &&
                    duelEvent.Current?.Location == DuelLocation.Graveyard),
                Is.True,
                "The exact Level 8 ritual material must be consumed by the Core.");
        }

        [Test]
        public void PendulumSummonUsesTheOfficialScaleWindowAndCoreSelection()
        {
            uint[] fillerDeck = Enumerable.Repeat(BlueEyesWhiteDragon, 40)
                .ToArray();
            var configuration = new DuelConfiguration
            {
                StartingHand = 0,
                Seed = 0xA17AE0D010000001UL,
                ShuffleMainDecks = false,
                SimpleOpponentAi = false,
                PlayerDeck = (uint[])fillerDeck.Clone(),
                OpponentDeck = (uint[])fillerDeck.Clone(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>()
            };

            string root = Path.Combine(Application.streamingAssetsPath, "Ygo");
            CardDatabase database = CardDatabase.LoadDefault();
            Assert.That(database.Get(DddSuperDoomKingBrightArmageddon).LeftScale,
                Is.EqualTo(1));
            Assert.That(database.Get(OddEyesPendulumDragon).RightScale,
                Is.EqualTo(4));
            Assert.That(database.Get(AshBlossom).Level, Is.EqualTo(3));

            var state = new DuelPresentationState(database);
            var events = new List<DuelEvent>();
            var responses = new List<(ulong RequestId, CoreMessage Message, byte[] Payload)>();
            bool pendulumMoved = false;
            int decisions = 0;

            using (var engine = new OcgDuelEngine(
                       database,
                       root,
                       configuration))
            {
                engine.AddCardAt(
                    0,
                    DddSuperDoomKingBrightArmageddon,
                    DuelLocation.SpellTrapZone,
                    0,
                    FaceUpAttack);
                engine.AddCardAt(
                    0,
                    OddEyesPendulumDragon,
                    DuelLocation.SpellTrapZone,
                    4,
                    FaceUpAttack);
                engine.AddCardAt(
                    0,
                    AshBlossom,
                    DuelLocation.Hand,
                    0,
                    0);
                engine.EventReceived += duelEvent =>
                {
                    events.Add(duelEvent);
                    state.Apply(duelEvent);
                    if (duelEvent.Message == CoreMessage.Move &&
                        duelEvent.Code == AshBlossom &&
                        duelEvent.Previous?.Location == DuelLocation.Hand &&
                        duelEvent.Current?.Location == DuelLocation.MonsterZone)
                    {
                        pendulumMoved = true;
                    }
                };

                engine.Start();
                while (!engine.IsFinished && !pendulumMoved && decisions++ < 80)
                {
                    DuelPrompt prompt = engine.CurrentPrompt;
                    Assert.That(prompt, Is.Not.Null, Trace(events, engine.NativeLogs));
                    DuelChoice choice = ChoosePendulumPath(prompt);
                    Assert.That(choice, Is.Not.Null, Trace(events, engine.NativeLogs));
                    Assert.That(
                        prompt.Choices.Contains(choice),
                        Is.True,
                        "Every Pendulum response must originate in the current Core prompt.");
                    responses.Add((
                        prompt.RequestId,
                        prompt.Message,
                        (byte[])choice.Response.Clone()));
                    engine.SubmitResponse(choice.Response);
                }

                Assert.That(
                    engine.TryCaptureFieldSnapshot(out OcgFieldSnapshot snapshot),
                    Is.True);
                state.ReconcileFromCore(snapshot);
            }

            AssertSummonTrace(
                events,
                responses,
                state,
                AshBlossom,
                "Pendulum");
            Assert.That(
                state.Players[0].SpellTrapZones[0],
                Is.EqualTo(DddSuperDoomKingBrightArmageddon));
            Assert.That(
                state.Players[0].SpellTrapZones[4],
                Is.EqualTo(OddEyesPendulumDragon));
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
            uint activatingSpell = 0,
            IReadOnlyList<uint> handMaterials = null)
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
            var responses = new List<(ulong RequestId, CoreMessage Message, byte[] Payload)>();
            var state = new DuelPresentationState(CardDatabase.LoadDefault());
            int retries = 0;
            int unknown = 0;
            int decisions = 0;
            bool summonStarted = false;
            bool movedFromExtra = false;
            bool summonConfirmed = false;

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
                for (int index = 0;
                     index < (handMaterials?.Count ?? 0);
                     index++)
                {
                    engine.AddCardAt(
                        0,
                        handMaterials[index],
                        DuelLocation.Hand,
                        (uint)(index + (activatingSpell == 0 ? 0 : 1)),
                        0);
                }

                engine.EventReceived += duelEvent =>
                {
                    events.Add(duelEvent);
                    state.Apply(duelEvent);
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
                    if (duelEvent.Message == CoreMessage.SpecialSummoned)
                        summonConfirmed = true;
                };

                engine.Start();
                while (!engine.IsFinished &&
                       !summonConfirmed &&
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
                    Assert.That(
                        prompt.Choices.Contains(choice),
                        Is.True,
                        "Every summon response must originate in the current Core prompt.");
                    responses.Add((
                        prompt.RequestId,
                        prompt.Message,
                        (byte[])choice.Response.Clone()));
                    engine.SubmitResponse(choice.Response);
                }

                Assert.That(
                    engine.TryCaptureFieldSnapshot(out OcgFieldSnapshot snapshot),
                    Is.True);
                state.ReconcileFromCore(snapshot);

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
                Assert.That(
                    summonConfirmed,
                    Is.True,
                    "The summon must not be presented as complete before the Core confirmation. " +
                    Trace(events, engine.NativeLogs));
            }


            AssertSummonTrace(
                events,
                responses,
                state,
                extraDeckMonster,
                "Extra Deck");
            Assert.That(state.PendingSummon, Is.Null);
            Assert.That(state.LastSummon, Is.Not.Null);
            Assert.That(state.LastSummon.Status,
                Is.EqualTo(DuelSummonStatus.Confirmed));
            if (extraDeckMonster == EbonIllusionMagician)
            {
                CardInstanceState xyz = state.Players[0].MonsterInstances
                    .First(instance => instance != null &&
                        instance.DefinitionCode == extraDeckMonster);
                Assert.That(
                    state.Players[0].OverlayInstances[xyz.Sequence]
                        .Select(instance => instance.DefinitionCode),
                    Is.EquivalentTo(faceUpMaterials));
            }
            else
            {
                Assert.That(
                    state.Players[0].Graveyard,
                    Is.SupersetOf(faceUpMaterials.Concat(
                        handMaterials ?? Array.Empty<uint>())),
                    "Fusion/Synchro/Link materials from every Core-selected zone must reach the authoritative destination.");
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

        private static DuelChoice ChooseRitualPath(DuelPrompt prompt)
        {
            if (prompt.Message == CoreMessage.SelectIdleCommand)
            {
                DuelChoice activation = prompt.Choices.FirstOrDefault(choice =>
                    choice.CardCode == ChaosForm &&
                    choice.Label.StartsWith(
                        "Ativar",
                        StringComparison.OrdinalIgnoreCase));
                if (activation != null)
                    return activation;
            }

            DuelChoice ritualMonster = prompt.Choices.FirstOrDefault(choice =>
                choice.CardCode == BlueEyesChaosMaxDragon);
            if (ritualMonster != null)
                return ritualMonster;
            DuelChoice material = prompt.Choices.FirstOrDefault(choice =>
                choice.CardCode == BlueEyesWhiteDragon);
            return material ?? DeterministicDuelPolicy.Choose(prompt);
        }

        private static DuelChoice ChoosePendulumPath(
            DuelPrompt prompt,
            uint targetCode = AshBlossom)
        {
            if (prompt.Message == CoreMessage.SelectIdleCommand)
            {
                DuelChoice pendulum = prompt.Choices.FirstOrDefault(choice =>
                    choice.CardCode == targetCode &&
                    choice.Label.IndexOf(
                        "especial",
                        StringComparison.OrdinalIgnoreCase) >= 0) ??
                    prompt.Choices.FirstOrDefault(choice =>
                        choice.Label.IndexOf(
                            "especial",
                            StringComparison.OrdinalIgnoreCase) >= 0);
                if (pendulum != null)
                    return pendulum;
            }

            DuelChoice target = prompt.Choices.FirstOrDefault(choice =>
                choice.CardCode == targetCode);
            return target ?? DeterministicDuelPolicy.Choose(prompt);
        }

        private static void AssertSummonTrace(
            IReadOnlyCollection<DuelEvent> events,
            IReadOnlyCollection<(ulong RequestId, CoreMessage Message, byte[] Payload)> responses,
            DuelPresentationState state,
            uint summonedCard,
            string procedure)
        {
            Assert.That(
                events.Any(duelEvent => duelEvent.Message == CoreMessage.Retry),
                Is.False,
                $"The Core rejected the legal {procedure} path.");
            Assert.That(
                events.Any(duelEvent => duelEvent.IsUnknown),
                Is.False,
                $"The {procedure} trace contains an untyped Core message.");
            Assert.That(
                events.All(duelEvent =>
                    duelEvent.RawMessage == (byte)duelEvent.Message),
                Is.True,
                $"The raw and typed {procedure} message identities must agree.");
            Assert.That(responses, Is.Not.Empty);
            Assert.That(
                responses.All(response =>
                    response.RequestId > 0 &&
                    response.Payload != null &&
                    response.Payload.Length > 0),
                Is.True,
                $"Every {procedure} prompt must retain its request and exact response payload.");
            Assert.That(
                responses.Select(response => response.RequestId),
                Is.Ordered,
                $"The {procedure} request sequence must be monotonic.");

            CardInstanceState instance = state.Players[0]
                .MonsterInstances
                .FirstOrDefault(candidate =>
                    candidate != null &&
                    candidate.DefinitionCode == summonedCard);
            Assert.That(
                instance,
                Is.Not.Null,
                $"The authoritative {procedure} summon must exist in the projected field.");
            Assert.That(instance.RuntimeId, Is.Not.Zero);
            Assert.That(instance.Owner, Is.EqualTo(0));
            Assert.That(instance.Controller, Is.EqualTo(0));
            Assert.That(instance.Location, Is.EqualTo((byte)DuelLocation.MonsterZone));
            Assert.That(instance.Position, Is.Not.Zero);
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
