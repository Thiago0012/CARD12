using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class StabilizationRegressionEditModeTests
    {
        private const uint DarkMagician = 46986414;
        private const uint EbonIllusionMagician = 96471335;
        private const uint EffectVeiler = 97268402;
        private const uint ApprenticeIllusionMagician = 30603688;
        private const uint BlueEyesWhiteDragon = 89631139;
        private const uint FaceUpAttack = 0x1;

        [Test]
        public void IdenticalDefinitionsCreateDistinctPhysicalInstances()
        {
            DuelPresentationState state = NewState();
            ApplyDraw(
                state,
                0,
                DarkMagician,
                DarkMagician,
                DarkMagician);

            Assert.That(state.Players[0].HandInstances, Has.Count.EqualTo(3));
            Assert.That(
                state.Players[0].HandInstances
                    .Select(instance => instance.RuntimeId)
                    .Distinct()
                    .Count(),
                Is.EqualTo(3));
            Assert.That(
                state.Players[0].HandInstances
                    .Select(instance => instance.DefinitionCode),
                Has.All.EqualTo(DarkMagician));
            Assert.That(state.ValidateInstanceConsistency(), Is.Empty);
        }

        [Test]
        public void MovingSecondDuplicateNeverConsumesFirstCopy()
        {
            DuelPresentationState state = NewState();
            ApplyDraw(
                state,
                0,
                DarkMagician,
                DarkMagician,
                DarkMagician);
            ulong first = state.Players[0].HandInstances[0].RuntimeId;
            ulong second = state.Players[0].HandInstances[1].RuntimeId;

            ApplyMove(
                state,
                DarkMagician,
                0,
                (byte)DuelLocation.Hand,
                1,
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                FaceUpAttack);

            Assert.That(
                state.Players[0].HandInstances[0].RuntimeId,
                Is.EqualTo(first));
            Assert.That(
                state.Players[0].MonsterInstances[2].RuntimeId,
                Is.EqualTo(second));
            Assert.That(state.Players[0].Hand, Has.Count.EqualTo(2));
            Assert.That(state.ValidateInstanceConsistency(), Is.Empty);
        }

        [Test]
        public void DiscardAndReturnKeepTheSamePhysicalCopy()
        {
            DuelPresentationState state = NewState();
            ApplyDraw(state, 0, DarkMagician, DarkMagician);
            ulong discarded = state.Players[0].HandInstances[1].RuntimeId;

            ApplyMove(
                state,
                DarkMagician,
                0,
                (byte)DuelLocation.Hand,
                1,
                0,
                (byte)DuelLocation.Graveyard,
                0,
                0);
            Assert.That(
                state.Players[0].GraveyardInstances[0].RuntimeId,
                Is.EqualTo(discarded));

            ApplyMove(
                state,
                DarkMagician,
                0,
                (byte)DuelLocation.Graveyard,
                0,
                0,
                (byte)DuelLocation.Hand,
                1,
                0);
            Assert.That(
                state.Players[0].HandInstances[1].RuntimeId,
                Is.EqualTo(discarded));
            Assert.That(state.ValidateInstanceConsistency(), Is.Empty);
        }

        [Test]
        public void XyzMaterialsRemainBoundToTheirAuthoritativeHost()
        {
            DuelPresentationState state = NewState();
            ApplyDraw(state, 0, DarkMagician, DarkMagician);
            ulong first = state.Players[0].HandInstances[0].RuntimeId;
            ulong second = state.Players[0].HandInstances[1].RuntimeId;
            ApplyMove(
                state,
                DarkMagician,
                0,
                (byte)DuelLocation.Hand,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                FaceUpAttack);
            ApplyMove(
                state,
                DarkMagician,
                0,
                (byte)DuelLocation.Hand,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                FaceUpAttack);

            ApplyMove(
                state,
                DarkMagician,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                0,
                (byte)DuelLocation.Overlay,
                2,
                0);
            ApplyMove(
                state,
                DarkMagician,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                0,
                (byte)DuelLocation.Overlay,
                2,
                1);

            Assert.That(state.Players[0].MonsterZones[0], Is.Zero);
            Assert.That(state.Players[0].MonsterZones[1], Is.Zero);
            Assert.That(
                state.Players[0].OverlayInstances[2]
                    .Select(instance => instance.RuntimeId),
                Is.EqualTo(new[] { first, second }));
            Assert.That(
                state.Players[0].OverlayInstances[2]
                    .Select(instance => instance.Sequence),
                Has.All.EqualTo(2));
            Assert.That(state.ValidateInstanceConsistency(), Is.Empty);
        }

        [Test]
        public void CoreActionsBindToTheSelectedDuplicateSequence()
        {
            DuelPresentationState state = NewState();
            ApplyDraw(state, 0, DarkMagician, DarkMagician);
            DuelPrompt prompt = DuplicateIdlePrompt();

            IReadOnlyList<DuelChoice> first =
                CoreCardActionBinding.ChoicesFor(
                    prompt,
                    state.Players[0].HandInstances[0].Key);
            IReadOnlyList<DuelChoice> second =
                CoreCardActionBinding.ChoicesFor(
                    prompt,
                    state.Players[0].HandInstances[1].Key);

            Assert.That(first.Count, Is.EqualTo(1));
            Assert.That(second.Count, Is.EqualTo(1));
            Assert.That(first[0].Sequence, Is.Zero);
            Assert.That(second[0].Sequence, Is.EqualTo(1));
            Assert.That(first[0].Response, Is.Not.EqualTo(second[0].Response));

            // Cancelling the local inspection changes neither the state nor
            // the binding: selecting the second copy still resolves sequence 1.
            Assert.That(
                CoreCardActionBinding.FirstChoiceFor(
                    prompt,
                    state.Players[0].HandInstances[1].Key),
                Is.SameAs(second[0]));
        }

        [Test]
        public void ShuffleHandReordersDefinitionsInstancesAndCoreAddresses()
        {
            DuelPresentationState state = NewState();
            ApplyDraw(
                state,
                0,
                DarkMagician,
                EffectVeiler,
                ApprenticeIllusionMagician);
            ulong darkMagician =
                state.Players[0].HandInstances[0].RuntimeId;
            ulong effectVeiler =
                state.Players[0].HandInstances[1].RuntimeId;
            ulong apprentice =
                state.Players[0].HandInstances[2].RuntimeId;

            ApplyShuffleHand(
                state,
                0,
                ApprenticeIllusionMagician,
                DarkMagician,
                EffectVeiler);

            Assert.That(
                state.Players[0].Hand,
                Is.EqualTo(new[]
                {
                    ApprenticeIllusionMagician,
                    DarkMagician,
                    EffectVeiler
                }));
            Assert.That(
                state.Players[0].HandInstances
                    .Select(instance => instance.RuntimeId),
                Is.EqualTo(new[]
                {
                    apprentice,
                    darkMagician,
                    effectVeiler
                }));
            Assert.That(
                state.Players[0].HandInstances
                    .Select(instance => instance.Sequence),
                Is.EqualTo(new uint[] { 0, 1, 2 }));

            DuelPrompt prompt =
                SingleIdlePrompt(ApprenticeIllusionMagician, 0);
            DuelChoice action =
                CoreCardActionBinding.FirstChoiceFor(
                    prompt,
                    state.Players[0].HandInstances[0].Key);
            Assert.That(action, Is.Not.Null);
            Assert.That(action.CardCode, Is.EqualTo(
                ApprenticeIllusionMagician));
            Assert.That(action.Sequence, Is.Zero);
            Assert.That(state.ValidateInstanceConsistency(), Is.Empty);
        }

        [Test]
        public void ChoiceFromAnExpiredRequestIsRejected()
        {
            DuelPrompt previous = DuplicateIdlePrompt();
            DuelPrompt current = DuplicateIdlePrompt();
            SetRequest(previous, 41);
            SetRequest(current, 42);

            Assert.That(
                CoreCardActionBinding.BelongsToRequest(
                    current,
                    previous.Choices[0]),
                Is.False);
            Assert.That(
                CoreCardActionBinding.BelongsToRequest(
                    current,
                    current.Choices[0]),
                Is.True);
        }

        [Test]
        public void RepeatedNoProgressStateStopsOverPrioritizingXyz()
        {
            DuelPresentationState state = NewState();
            ApplyMove(
                state,
                DarkMagician,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                FaceUpAttack);
            ApplyMove(
                state,
                DarkMagician,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                FaceUpAttack);
            DuelPrompt prompt = XyzOrEndPrompt();
            var agent = new TacticalOpponentAgent();
            var choices = new List<DuelChoice>();

            for (int index = 0; index < 4; index++)
                choices.Add(agent.Choose(
                    prompt,
                    state,
                    CardDatabase.LoadDefault()));

            Assert.That(choices, Has.All.Not.Null);
            Assert.That(
                choices.Skip(1).Any(choice =>
                    choice.CardCode != EbonIllusionMagician),
                Is.True,
                "The bot must leave a repeated no-progress Xyz decision.");
        }

        [Test]
        public void CoreAssignsMonotonicRequestIdsToLivePrompts()
        {
            uint[] deck = Enumerable.Repeat(DarkMagician, 40).ToArray();
            var configuration = new DuelConfiguration
            {
                StartingHand = 5,
                Seed = 0x514E7A11UL,
                ShuffleMainDecks = false,
                SimpleOpponentAi = false,
                PlayerDeck = (uint[])deck.Clone(),
                OpponentDeck = (uint[])deck.Clone(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>()
            };
            string root = Path.Combine(
                Application.streamingAssetsPath,
                "Ygo");

            using (var engine = new OcgDuelEngine(
                       CardDatabase.LoadDefault(),
                       root,
                       configuration))
            {
                engine.Start();
                DuelPrompt first = engine.CurrentPrompt;
                Assert.That(first, Is.Not.Null);
                Assert.That(first.RequestId, Is.GreaterThan(0));
                engine.SubmitResponse(
                    DeterministicDuelPolicy.Choose(first).Response);
                DuelPrompt second = engine.CurrentPrompt;
                Assert.That(second, Is.Not.Null);
                Assert.That(
                    second.RequestId,
                    Is.GreaterThan(first.RequestId));
                Assert.That(
                    second.Choices,
                    Has.All.Matches<DuelChoice>(
                        choice => choice.RequestId == second.RequestId));
            }
        }

        [Test]
        public void NormalSummonLegalityAndResolutionComeFromCore()
        {
            AssertCoreNormalSummon(EffectVeiler, 0);
        }

        [Test]
        public void TributeSummonLegalityAndMaterialsComeFromCore()
        {
            AssertCoreNormalSummon(DarkMagician, 2);
        }

        [Test]
        public void CurrentCombatStatsComeFromCoreAfterFusilierSummon()
        {
            AssertCoreNormalSummon(51632798, 0, 1400, 1000);
        }

        private static DuelPresentationState NewState()
        {
            return new DuelPresentationState(CardDatabase.LoadDefault());
        }

        private static void AssertCoreNormalSummon(
            uint summonedCard,
            int tributeCount,
            int? expectedAttack = null,
            int? expectedDefense = null)
        {
            uint[] deck = Enumerable
                .Repeat(BlueEyesWhiteDragon, 40)
                .ToArray();
            var configuration = new DuelConfiguration
            {
                StartingHand = 0,
                Seed = 0x7A1B07E000000000UL ^ summonedCard,
                ShuffleMainDecks = false,
                SimpleOpponentAi = false,
                PlayerDeck = (uint[])deck.Clone(),
                OpponentDeck = (uint[])deck.Clone(),
                PlayerExtraDeck = Array.Empty<uint>(),
                OpponentExtraDeck = Array.Empty<uint>()
            };
            string root = Path.Combine(
                Application.streamingAssetsPath,
                "Ygo");
            bool movedToField = false;
            bool summonCompleted = false;
            uint summonedSequence = 0;
            int? queriedAttack = null;
            int? queriedDefense = null;
            string queryTrace = string.Empty;
            int retries = 0;
            int decisions = 0;

            using (var engine = new OcgDuelEngine(
                       CardDatabase.LoadDefault(),
                       root,
                       configuration))
            {
                for (int index = 0; index < tributeCount; index++)
                {
                    engine.AddCardAt(
                        0,
                        EffectVeiler,
                        DuelLocation.MonsterZone,
                        (uint)index,
                        FaceUpAttack);
                }
                engine.AddCardAt(
                    0,
                    summonedCard,
                    DuelLocation.Hand,
                    0,
                    0);
                engine.EventReceived += duelEvent =>
                {
                    if (duelEvent.Message == CoreMessage.Retry)
                        retries++;
                    if (duelEvent.Message == CoreMessage.Move &&
                        duelEvent.Code == summonedCard &&
                        duelEvent.Previous != null &&
                        duelEvent.Previous.Location == DuelLocation.Hand &&
                        duelEvent.Current != null &&
                        duelEvent.Current.Location ==
                        DuelLocation.MonsterZone)
                    {
                        movedToField = true;
                        summonedSequence = duelEvent.Current.Sequence;
                    }
                    if (duelEvent.Message == CoreMessage.Summoned)
                        summonCompleted = true;
                };

                engine.Start();
                while (!engine.IsFinished &&
                       (!movedToField ||
                        (expectedAttack.HasValue && !summonCompleted)) &&
                       decisions++ < 40)
                {
                    DuelPrompt prompt = engine.CurrentPrompt;
                    Assert.That(prompt, Is.Not.Null);
                    DuelChoice choice = null;
                    if (prompt.Message ==
                        CoreMessage.SelectIdleCommand)
                    {
                        choice = prompt.Choices.FirstOrDefault(candidate =>
                            candidate.CardCode == summonedCard &&
                            candidate.Label.StartsWith(
                                "Invocar",
                                StringComparison.OrdinalIgnoreCase) &&
                            candidate.Label.IndexOf(
                                "especial",
                                StringComparison.OrdinalIgnoreCase) < 0);
                    }
                    choice ??= DeterministicDuelPolicy.Choose(prompt);
                    Assert.That(choice, Is.Not.Null);
                    engine.SubmitResponse(choice.Response);
                }
                if (movedToField && expectedAttack.HasValue)
                {
                    Assert.That(
                        engine.TryGetCurrentCombatStats(
                            0,
                            DuelLocation.MonsterZone,
                            summonedSequence,
                            out int attack,
                            out int defense),
                        Is.True,
                        "The Core must expose the current field stats.");
                    queriedAttack = attack;
                    queriedDefense = defense;
                    queryTrace =
                        "prompt=" + engine.CurrentPrompt?.Message + "; " +
                        "choices=" + string.Join(",", engine.CurrentPrompt?
                            .Choices.Select(candidate => candidate.Label) ??
                            Array.Empty<string>()) + "; events=" +
                        string.Join(",", engine.EventHistory
                            .TakeLast(12)
                            .Select(duelEvent => duelEvent.Message.ToString()));
                }
            }
            Assert.That(
                retries,
                Is.Zero,
                "The Core rejected its own legal summon path.");
            Assert.That(
                movedToField,
                Is.True,
                "The summon must be completed only through Core prompts.");
            if (expectedAttack.HasValue)
            {
                Assert.That(
                    queriedAttack,
                    Is.EqualTo(expectedAttack),
                    queryTrace);
                Assert.That(
                    queriedDefense,
                    Is.EqualTo(expectedDefense),
                    queryTrace);
            }
        }

        private static void ApplyDraw(
            DuelPresentationState state,
            byte player,
            params uint[] codes)
        {
            var payload = new List<byte> { player };
            UInt32(payload, (uint)codes.Length);
            foreach (uint code in codes)
            {
                UInt32(payload, code);
                UInt32(payload, 0);
            }
            state.Apply(Decode(CoreMessage.Draw, payload));
        }

        private static void ApplyShuffleHand(
            DuelPresentationState state,
            byte player,
            params uint[] codes)
        {
            var payload = new List<byte> { player };
            UInt32(payload, (uint)codes.Length);
            foreach (uint code in codes)
                UInt32(payload, code);
            state.Apply(Decode(CoreMessage.ShuffleHand, payload));
        }

        private static void ApplyMove(
            DuelPresentationState state,
            uint code,
            byte previousController,
            byte previousLocation,
            uint previousSequence,
            byte currentController,
            byte currentLocation,
            uint currentSequence,
            uint currentPosition)
        {
            var payload = new List<byte>();
            UInt32(payload, code);
            Location(
                payload,
                previousController,
                previousLocation,
                previousSequence,
                0);
            Location(
                payload,
                currentController,
                currentLocation,
                currentSequence,
                currentPosition);
            UInt32(payload, 0);
            state.Apply(Decode(CoreMessage.Move, payload));
        }

        private static DuelPrompt DuplicateIdlePrompt()
        {
            var payload = new List<byte> { 0 };
            UInt32(payload, 2);
            CommandCard(payload, DarkMagician, 0);
            CommandCard(payload, DarkMagician, 1);
            for (int category = 0; category < 5; category++)
                UInt32(payload, 0);
            payload.Add(0);
            payload.Add(1);
            payload.Add(0);
            return Decode(CoreMessage.SelectIdleCommand, payload).Prompt;
        }

        private static DuelPrompt SingleIdlePrompt(
            uint code,
            uint sequence)
        {
            var payload = new List<byte> { 0 };
            UInt32(payload, 1);
            CommandCard(payload, code, sequence);
            for (int category = 0; category < 5; category++)
                UInt32(payload, 0);
            payload.Add(0);
            payload.Add(1);
            payload.Add(0);
            return Decode(CoreMessage.SelectIdleCommand, payload).Prompt;
        }

        private static DuelPrompt XyzOrEndPrompt()
        {
            var payload = new List<byte> { 1 };
            UInt32(payload, 0);
            UInt32(payload, 1);
            UInt32(payload, EbonIllusionMagician);
            payload.Add(1);
            payload.Add((byte)DuelLocation.Extra);
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 0);
            UInt32(payload, 0);
            payload.Add(0);
            payload.Add(1);
            payload.Add(0);
            return Decode(CoreMessage.SelectIdleCommand, payload).Prompt;
        }

        private static void CommandCard(
            List<byte> payload,
            uint code,
            uint sequence)
        {
            UInt32(payload, code);
            payload.Add(0);
            payload.Add((byte)DuelLocation.Hand);
            UInt32(payload, sequence);
        }

        private static DuelEvent Decode(
            CoreMessage message,
            List<byte> payload)
        {
            var framed = new List<byte>();
            UInt32(framed, (uint)payload.Count + 1);
            framed.Add((byte)message);
            framed.AddRange(payload);
            return CoreMessageDecoder.Decode(framed.ToArray())[0];
        }

        private static void Location(
            List<byte> bytes,
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            bytes.Add(controller);
            bytes.Add(location);
            UInt32(bytes, sequence);
            UInt32(bytes, position);
        }

        private static void UInt32(List<byte> bytes, uint value)
        {
            bytes.Add((byte)(value & 0xFF));
            bytes.Add((byte)((value >> 8) & 0xFF));
            bytes.Add((byte)((value >> 16) & 0xFF));
            bytes.Add((byte)((value >> 24) & 0xFF));
        }

        private static void SetRequest(
            DuelPrompt prompt,
            ulong requestId)
        {
            PropertyInfo promptProperty = typeof(DuelPrompt).GetProperty(
                nameof(DuelPrompt.RequestId));
            promptProperty.SetValue(prompt, requestId);
            foreach (DuelChoice choice in prompt.Choices)
            {
                PropertyInfo choiceProperty =
                    typeof(DuelChoice).GetProperty(
                        nameof(DuelChoice.RequestId));
                choiceProperty.SetValue(choice, requestId);
            }
        }
    }
}
