using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ArcaneDuel.DuelEngine.Core;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    /// <summary>
    /// Regression coverage for the wire snapshot repairs. The multiplayer
    /// protocol belongs to the predefined Assembly-CSharp, which an asmdef
    /// test assembly cannot reference directly, so its public entry points
    /// and wire DTO fields are exercised through reflection.
    /// </summary>
    public sealed class MultiplayerStateRepairEditModeTests
    {
        private const uint FaceUpAttack = 0x1;
        private const uint FaceDownDefense = 0x8;
        private const uint HostXyz = 96471335;
        private const uint FirstMaterial = 46986414;
        private const uint SecondMaterial = 97268402;
        private const uint HiddenMonster = 89631139;
        private const uint HiddenSpell = 53129443;

        private Type protocolType;

        [SetUp]
        public void ResolveProtocol()
        {
            protocolType = FindType(
                "ArcaneArena.Multiplayer.DuelNetworkProtocol");
            Assert.That(protocolType, Is.Not.Null);
        }

        [Test]
        public void ApplyingSameNetworkSnapshotTwicePreservesRuntimeIds()
        {
            DuelPresentationState host = CreatePopulatedHostState();
            object networkState = CreateState(host, null);
            var replica = new DuelPresentationState(null);

            Apply(networkState, replica);
            ulong visibleMonster =
                replica.Players[0].MonsterInstances[2].RuntimeId;
            ulong[] overlayIds = replica.Players[0].OverlayInstances[2]
                .Select(instance => instance.RuntimeId)
                .ToArray();
            ulong hiddenMonster =
                replica.Players[1].MonsterInstances[2].RuntimeId;
            ulong hiddenSpell =
                replica.Players[1].SpellTrapInstances[3].RuntimeId;

            Assert.That(visibleMonster, Is.Not.Zero);
            Assert.That(overlayIds, Has.All.Not.Zero);
            Assert.That(hiddenMonster, Is.Not.Zero);
            Assert.That(hiddenSpell, Is.Not.Zero);

            Apply(networkState, replica);

            Assert.That(
                replica.Players[0].MonsterInstances[2].RuntimeId,
                Is.EqualTo(visibleMonster));
            Assert.That(
                replica.Players[0].OverlayInstances[2]
                    .Select(instance => instance.RuntimeId),
                Is.EqualTo(overlayIds));
            Assert.That(
                replica.Players[1].MonsterInstances[2].RuntimeId,
                Is.EqualTo(hiddenMonster));
            Assert.That(
                replica.Players[1].SpellTrapInstances[3].RuntimeId,
                Is.EqualTo(hiddenSpell));
        }

        [Test]
        public void SummonAttemptIsPendingUntilCoreConfirmationOnBothPeers()
        {
            var host = new DuelPresentationState(null);
            ApplyMove(
                host,
                HiddenMonster,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                FaceUpAttack);
            host.Apply(SummonEvent(
                CoreMessage.Summoning,
                HiddenMonster,
                0,
                1,
                FaceUpAttack));

            Assert.That(host.PendingSummon, Is.Not.Null);
            Assert.That(host.PendingSummon.Status, Is.EqualTo(DuelSummonStatus.Pending));
            Assert.That(host.LastSummon, Is.Null);

            var replica = new DuelPresentationState(null);
            Apply(CreateState(host, null), replica);
            Assert.That(replica.PendingSummon, Is.Not.Null);
            Assert.That(replica.PendingSummon.CardCode, Is.EqualTo(HiddenMonster));
            Assert.That(replica.LastSummon, Is.Null);
            var opponentReplica = new DuelPresentationState(null);
            Apply(CreateState(host, null, 1), opponentReplica);
            Assert.That(opponentReplica.PendingSummon.Controller, Is.EqualTo(1),
                "The same global summon must map to the opponent side for recipient seat 1.");
            Assert.That(opponentReplica.PendingSummon.CardCode,
                Is.EqualTo(HiddenMonster));

            host.Apply(Decode(CoreMessage.Summoned, new List<byte>()));
            Apply(CreateState(host, null), replica);

            Assert.That(host.PendingSummon, Is.Null);
            Assert.That(replica.PendingSummon, Is.Null);
            Assert.That(replica.LastSummon, Is.Not.Null);
            Assert.That(replica.LastSummon.Status,
                Is.EqualTo(DuelSummonStatus.Confirmed));
            Assert.That(replica.LastSummon.CardCode, Is.EqualTo(HiddenMonster));
        }

        [TestCase(CoreMessage.SpecialSummoning, CoreMessage.SpecialSummoned)]
        [TestCase(CoreMessage.FlipSummoning, CoreMessage.FlipSummoned)]
        public void SpecialAndFlipSummonsUseTheSameAuthoritativeConfirmationGate(
            CoreMessage attemptMessage,
            CoreMessage confirmationMessage)
        {
            var state = new DuelPresentationState(null);
            ApplyMove(
                state,
                HiddenMonster,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                FaceUpAttack);
            state.Apply(SummonEvent(
                attemptMessage,
                HiddenMonster,
                0,
                0,
                FaceUpAttack));
            Assert.That(state.PendingSummon.Status,
                Is.EqualTo(DuelSummonStatus.Pending));
            Assert.That(state.LastSummon, Is.Null);

            state.Apply(Decode(confirmationMessage, new List<byte>()));

            Assert.That(state.PendingSummon, Is.Null);
            Assert.That(state.LastSummon.Status,
                Is.EqualTo(DuelSummonStatus.Confirmed));
            Assert.That(state.LastSummon.Message, Is.EqualTo(attemptMessage));
        }

        [Test]
        public void SummonMovedOffFieldBeforeConfirmationIsNegatedOnBothPeers()
        {
            var host = new DuelPresentationState(null);
            ApplyMove(
                host,
                HiddenMonster,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                FaceUpAttack);
            host.Apply(SummonEvent(
                CoreMessage.Summoning,
                HiddenMonster,
                0,
                1,
                FaceUpAttack));
            ApplyMove(
                host,
                HiddenMonster,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                0,
                (byte)DuelLocation.Graveyard,
                0,
                FaceUpAttack);

            Assert.That(host.PendingSummon, Is.Null);
            Assert.That(host.LastSummon.Status,
                Is.EqualTo(DuelSummonStatus.Negated));

            var replica = new DuelPresentationState(null);
            Apply(CreateState(host, null), replica);
            Assert.That(replica.LastSummon.Status,
                Is.EqualTo(DuelSummonStatus.Negated));
            Assert.That(replica.Players[0].MonsterZones[1], Is.Zero);
            Assert.That(replica.Players[0].Graveyard, Does.Contain(HiddenMonster));
        }

        [Test]
        public void XyzDetachPreservesMaterialIdentityWithoutOccupyingMonsterZone()
        {
            DuelPresentationState host = CreatePopulatedHostState();
            ulong detachedRuntimeId = host.Players[0].OverlayInstances[2][0]
                .RuntimeId;
            ApplyMove(
                host,
                FirstMaterial,
                0,
                (byte)DuelLocation.Overlay,
                2,
                0,
                (byte)DuelLocation.Graveyard,
                0,
                FaceUpAttack);

            Assert.That(host.Players[0].MonsterZones[2], Is.EqualTo(HostXyz));
            Assert.That(host.Players[0].OverlayInstances[2], Has.Count.EqualTo(1));
            Assert.That(host.Players[0].GraveyardInstances, Has.Count.EqualTo(1));
            Assert.That(host.Players[0].GraveyardInstances[0].RuntimeId,
                Is.EqualTo(detachedRuntimeId));
            Assert.That(host.Players[0].MonsterZones,
                Has.None.EqualTo(FirstMaterial));

            var replica = new DuelPresentationState(null);
            Apply(CreateState(host, null), replica);
            Assert.That(replica.Players[0].OverlayInstances[2], Has.Count.EqualTo(1));
            Assert.That(replica.Players[0].GraveyardInstances[0].RuntimeId,
                Is.EqualTo(detachedRuntimeId));
        }

        [Test]
        public void FaceUpPendulumExtraIdentityIsPublicButHiddenExtraRemainsPrivate()
        {
            var host = new DuelPresentationState(null);
            host.ConfigureDeckCounts(0, 0, 0, 0);
            ApplyMove(
                host,
                HiddenMonster,
                1,
                0,
                0,
                1,
                (byte)DuelLocation.MonsterZone,
                0,
                FaceUpAttack);
            ulong pendulumRuntimeId =
                host.Players[1].MonsterInstances[0].RuntimeId;
            ApplyMove(
                host,
                HiddenMonster,
                1,
                (byte)DuelLocation.MonsterZone,
                0,
                1,
                (byte)DuelLocation.Extra,
                0,
                FaceUpAttack);
            ApplyMove(
                host,
                HiddenSpell,
                1,
                0,
                0,
                1,
                (byte)DuelLocation.Extra,
                1,
                FaceDownDefense);

            var replica = new DuelPresentationState(null);
            Apply(CreateState(host, null), replica);

            Assert.That(replica.Players[1].ExtraDeckCards[0],
                Is.EqualTo(HiddenMonster));
            Assert.That(replica.Players[1].ExtraDeckInstances[0].Position,
                Is.EqualTo(FaceUpAttack));
            Assert.That(replica.Players[1].ExtraDeckInstances[0].RuntimeId,
                Is.EqualTo(pendulumRuntimeId),
                "The authoritative field-to-face-up-Extra event must preserve the physical card identity.");
            Assert.That(replica.Players[1].ExtraDeckCards[1], Is.Zero);
            Assert.That(replica.Players[1].ExtraDeckInstances[1].RuntimeId,
                Is.Not.Zero);
        }

        [Test]
        public void OpponentFaceDownZonesRemainOccupiedWithOpaqueTokens()
        {
            DuelPresentationState host = CreatePopulatedHostState();
            ulong realMonsterId =
                host.Players[1].MonsterInstances[2].RuntimeId;
            ulong realSpellId =
                host.Players[1].SpellTrapInstances[3].RuntimeId;
            var replica = new DuelPresentationState(null);

            Apply(CreateState(host, null), replica);

            DuelistState opponent = replica.Players[1];
            Assert.That(opponent.MonsterZones[2], Is.Zero);
            Assert.That(opponent.SpellTrapZones[3], Is.Zero);
            Assert.That(opponent.MonsterInstances[2], Is.Not.Null);
            Assert.That(opponent.SpellTrapInstances[3], Is.Not.Null);
            Assert.That(
                opponent.MonsterInstances[2].DefinitionCode,
                Is.Zero);
            Assert.That(
                opponent.SpellTrapInstances[3].DefinitionCode,
                Is.Zero);

            ulong monsterToken = opponent.MonsterInstances[2].RuntimeId;
            ulong spellToken = opponent.SpellTrapInstances[3].RuntimeId;
            Assert.That(monsterToken, Is.Not.Zero);
            Assert.That(spellToken, Is.Not.Zero);
            Assert.That(monsterToken, Is.Not.EqualTo(realMonsterId));
            Assert.That(spellToken, Is.Not.EqualTo(realSpellId));
            Assert.That(monsterToken, Is.Not.EqualTo(spellToken));
        }

        [Test]
        public void CoreRepairDoesNotUndoShuffleSetCardAnonymity()
        {
            DuelPresentationState state = CreatePopulatedHostState();
            var shuffle = new DuelEvent();
            SetProperty(
                shuffle,
                nameof(DuelEvent.Message),
                CoreMessage.ShuffleSetCard);
            SetProperty(
                shuffle,
                nameof(DuelEvent.PreviousLocations),
                new[]
                {
                    CardLocationAt(
                        1,
                        (byte)DuelLocation.SpellTrapZone,
                        3,
                        FaceDownDefense)
                });
            SetProperty(
                shuffle,
                nameof(DuelEvent.CurrentLocations),
                new[] { new CardLocation() });
            state.Apply(shuffle);

            CardInstanceState opaque =
                state.Players[1].SpellTrapInstances[3];
            Assert.That(opaque, Is.Not.Null);
            Assert.That(opaque.IdentityOpaque, Is.True);
            Assert.That(opaque.DefinitionCode, Is.Zero);
            ulong opaqueId = opaque.RuntimeId;

            OcgFieldSnapshot coreSnapshot = FieldSnapshotWithSpell(
                HiddenSpell,
                FaceDownDefense);
            state.ReconcileFromCore(coreSnapshot);

            Assert.That(state.Players[1].SpellTrapZones[3], Is.Zero);
            Assert.That(
                state.Players[1].SpellTrapInstances[3].RuntimeId,
                Is.EqualTo(opaqueId));
            Assert.That(
                state.Players[1].SpellTrapInstances[3].IdentityOpaque,
                Is.True);

            coreSnapshot = FieldSnapshotWithSpell(HiddenSpell, FaceUpAttack);
            state.ReconcileFromCore(coreSnapshot);
            Assert.That(
                state.Players[1].SpellTrapZones[3],
                Is.EqualTo(HiddenSpell));
            Assert.That(
                state.Players[1].SpellTrapInstances[3].IdentityOpaque,
                Is.False);
        }

        [Test]
        public void JsonRoundTripPreservesOverlayStacksAndRuntimeIds()
        {
            DuelPresentationState host = CreatePopulatedHostState();
            uint[] expectedCards = host.Players[0].OverlayInstances[2]
                .Select(instance => instance.DefinitionCode)
                .ToArray();
            ulong[] expectedIds = host.Players[0].OverlayInstances[2]
                .Select(instance => instance.RuntimeId)
                .ToArray();
            object original = CreateState(host, null);

            string json = JsonUtility.ToJson(original);
            object roundTripped = JsonUtility.FromJson(
                json,
                original.GetType());

            Assert.That(roundTripped, Is.Not.Null);
            object snapshot = Field<object>(roundTripped, "snapshot");
            Array players = Field<Array>(snapshot, "players");
            Array overlays = Field<Array>(players.GetValue(0), "overlays");
            Assert.That(overlays, Is.Not.Null);
            Assert.That(overlays.Length, Is.GreaterThan(2));
            object zone = overlays.GetValue(2);
            Assert.That(zone, Is.Not.Null);
            Assert.That(
                Field<uint[]>(zone, "cards"),
                Is.EqualTo(expectedCards));
            Assert.That(
                Field<ulong[]>(zone, "runtimeIds"),
                Is.EqualTo(expectedIds));

            var replica = new DuelPresentationState(null);
            Apply(roundTripped, replica);
            Assert.That(
                replica.Players[0].OverlayInstances[2]
                    .Select(instance => instance.DefinitionCode),
                Is.EqualTo(expectedCards));
            Assert.That(
                replica.Players[0].OverlayInstances[2]
                    .Select(instance => instance.RuntimeId),
                Is.EqualTo(expectedIds));
        }

        [TestCase((byte)0)]
        [TestCase((byte)1)]
        public void JsonRoundTripPreservesProjectionHashForEachRecipient(
            byte recipient)
        {
            DuelPresentationState host = CreatePopulatedHostState();
            object original = CreateState(host, null, recipient);
            original.GetType().GetField("matchId")
                ?.SetValue(original, "crossplay-hash-round-trip");
            MethodInfo computeHash = protocolType.GetMethod(
                "ComputePublicProjectionHash",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(computeHash, Is.Not.Null);
            ulong expected = (ulong)computeHash.Invoke(null, new[] { original });
            original.GetType().GetField("publicStateHash")
                ?.SetValue(original, expected);

            string json = JsonUtility.ToJson(original);
            object received = JsonUtility.FromJson(json, original.GetType());
            ulong transported = Field<ulong>(received, "publicStateHash");
            ulong recomputed = (ulong)computeHash.Invoke(
                null,
                new[] { received });

            Assert.That(transported, Is.EqualTo(expected),
                "The integrity value itself must survive PC/Android JSON transport.");
            Assert.That(recomputed, Is.EqualTo(expected),
                "The received snapshot must hash exactly like the host payload.");

            var replica = new DuelPresentationState(null);
            DuelPrompt restoredPrompt = Apply(received, replica);
            Assert.That(restoredPrompt, Is.Null,
                "JsonUtility's empty placeholder must not become a phantom prompt.");
            Assert.That(replica.PendingSummon, Is.Null);
            Assert.That(replica.LastSummon, Is.Null);
        }

        [Test]
        public void PrivatePromptRedactsMetadataButPreservesResponseBytes()
        {
            DuelPresentationState host = CreatePopulatedHostState();
            byte[] response = { 0x13, 0x37, 0xA5, 0x5A };
            DuelPrompt prompt = CreatePrivateOpponentPrompt(response);
            object networkState = CreateState(host, prompt);

            object wirePrompt = Field<object>(networkState, "prompt");
            Array choices = Field<Array>(wirePrompt, "choices");
            Assert.That(choices, Has.Length.EqualTo(1));
            object wireChoice = choices.GetValue(0);
            Assert.That(Field<uint>(wireChoice, "cardCode"), Is.Zero);
            Assert.That(Field<ulong>(wireChoice, "descriptionId"), Is.Zero);
            Assert.That(Field<uint>(wireChoice, "sumValue"), Is.Zero);
            Assert.That(
                Convert.FromBase64String(
                    Field<string>(wireChoice, "responseBase64")),
                Is.EqualTo(response));

            var replica = new DuelPresentationState(null);
            DuelPrompt restored = Apply(networkState, replica);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Choices, Has.Count.EqualTo(1));
            DuelChoice restoredChoice = restored.Choices[0];
            Assert.That(restoredChoice.CardCode, Is.Zero);
            Assert.That(restoredChoice.DescriptionId, Is.Zero);
            Assert.That(restoredChoice.SumValue, Is.Zero);
            Assert.That(restoredChoice.Response, Is.EqualTo(response));
        }

        [Test]
        public void PublicPromptPreservesDistinctEffectsAndResponseBytes()
        {
            const uint jioh = 92962242;
            ulong firstId = (ulong)jioh << 20;
            ulong secondId = firstId | 4;
            var prompt = new DuelPrompt();
            SetProperty(prompt, nameof(DuelPrompt.RequestId), 1234UL);
            SetProperty(prompt, nameof(DuelPrompt.Message),
                CoreMessage.SelectChain);
            SetProperty(prompt, nameof(DuelPrompt.Player), (byte)0);
            SetProperty(prompt, nameof(DuelPrompt.Title),
                "Escolha o efeito");
            SetProperty(prompt, nameof(DuelPrompt.MinimumSelections), 1U);
            SetProperty(prompt, nameof(DuelPrompt.MaximumSelections), 1U);

            byte[] firstResponse = { 0, 0, 0, 0 };
            byte[] secondResponse = { 1, 0, 0, 0 };
            prompt.Choices.Add(EffectChoice(
                1234UL, 7001UL, 0, jioh, firstId, firstResponse));
            prompt.Choices.Add(EffectChoice(
                1234UL, 7001UL, 1, jioh, secondId, secondResponse));

            DuelPrompt restored = Apply(
                CreateState(CreatePopulatedHostState(), prompt),
                new DuelPresentationState(null));

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Choices, Has.Count.EqualTo(2));
            Assert.That(restored.Choices.Select(choice =>
                    choice.DescriptionId),
                Is.EqualTo(new[] { firstId, secondId }));
            Assert.That(restored.Choices[0].Response,
                Is.EqualTo(firstResponse));
            Assert.That(restored.Choices[1].Response,
                Is.EqualTo(secondResponse));
            Assert.That(restored.Choices.Select(choice =>
                    choice.CandidateIndex),
                Is.EqualTo(new[] { 0, 1 }));
            Assert.That(restored.Choices.Select(choice =>
                    choice.RuntimeId),
                Is.EqualTo(new[] { 7001UL, 7001UL }));
        }

        [Test]
        public void ChainingEventKeepsPublicEffectIdentityForRemotePlayer()
        {
            const ulong descriptionId = ((ulong)92962242 << 20) | 4;
            var source = new DuelEvent();
            SetProperty(source, nameof(DuelEvent.Message),
                CoreMessage.Chaining);
            SetProperty(source, nameof(DuelEvent.RawMessage),
                (byte)CoreMessage.Chaining);
            SetProperty(source, nameof(DuelEvent.Player), (byte)0);
            SetProperty(source, nameof(DuelEvent.Code), 92962242U);
            SetProperty(source, nameof(DuelEvent.DescriptionId),
                descriptionId);
            SetProperty(source, nameof(DuelEvent.Current),
                CardLocationAt(0, (byte)DuelLocation.Hand, 2, 0));

            MethodInfo create = protocolType.GetMethod(
                "CreatePresentationEvent",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[]
                {
                    typeof(DuelEvent), typeof(byte), typeof(int),
                    typeof(int), typeof(string)
                },
                null);
            Assert.That(create, Is.Not.Null);
            object wire = create.Invoke(null, new object[]
            {
                source, (byte)1, 7, 3, "effect-test"
            });

            Assert.That(Field<uint>(wire, "code"), Is.EqualTo(92962242U));
            Assert.That(Field<ulong>(wire, "descriptionId"),
                Is.EqualTo(descriptionId));

            MethodInfo restore = protocolType.GetMethod(
                "ToPresentationEvent",
                BindingFlags.Public | BindingFlags.Static);
            DuelEvent replica = (DuelEvent)restore.Invoke(
                null,
                new[] { wire });
            Assert.That(replica.Code, Is.EqualTo(92962242U));
            Assert.That(replica.DescriptionId, Is.EqualTo(descriptionId));
        }

        [Test]
        public void SnapshotRoundTripPreservesChainCountersRelationsAndDisabledZones()
        {
            DuelPresentationState host = CreatePopulatedHostState();
            ApplyMove(
                host,
                HiddenSpell,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.SpellTrapZone,
                1,
                FaceUpAttack);
            CardLocation monster = CardLocationAt(
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                FaceUpAttack);
            CardLocation spell = CardLocationAt(
                0,
                (byte)DuelLocation.SpellTrapZone,
                1,
                FaceUpAttack);

            var disabled = new DuelEvent();
            SetProperty(disabled, nameof(DuelEvent.Message),
                CoreMessage.FieldDisabled);
            SetProperty(disabled, nameof(DuelEvent.Value),
                (1U << 2) | (1U << 9));
            host.Apply(disabled);

            var counter = new DuelEvent();
            SetProperty(counter, nameof(DuelEvent.Message),
                CoreMessage.AddCounter);
            SetProperty(counter, nameof(DuelEvent.Current), monster);
            SetProperty(counter, nameof(DuelEvent.CounterType),
                (ushort)0x1234);
            SetProperty(counter, nameof(DuelEvent.Value), 3U);
            host.Apply(counter);

            var equip = new DuelEvent();
            SetProperty(equip, nameof(DuelEvent.Message), CoreMessage.Equip);
            SetProperty(equip, nameof(DuelEvent.Previous), spell);
            SetProperty(equip, nameof(DuelEvent.Current), monster);
            host.Apply(equip);

            var target = new DuelEvent();
            SetProperty(target, nameof(DuelEvent.Message),
                CoreMessage.CardTarget);
            SetProperty(target, nameof(DuelEvent.Previous), monster);
            SetProperty(target, nameof(DuelEvent.Current), spell);
            host.Apply(target);

            const ulong descriptionId = 0x1122334455667788UL;
            var chaining = new DuelEvent();
            SetProperty(chaining, nameof(DuelEvent.Message),
                CoreMessage.Chaining);
            SetProperty(chaining, nameof(DuelEvent.Player), (byte)0);
            SetProperty(chaining, nameof(DuelEvent.Code), HostXyz);
            SetProperty(chaining, nameof(DuelEvent.Value), 1U);
            SetProperty(chaining, nameof(DuelEvent.DescriptionId),
                descriptionId);
            SetProperty(chaining, nameof(DuelEvent.Current), monster);
            host.Apply(chaining);

            object before = CreateState(host, null);
            ulong hashBefore = Field<ulong>(before, "publicStateHash");
            var replica = new DuelPresentationState(null);
            Apply(before, replica);

            Assert.That(replica.DisabledFieldMask,
                Is.EqualTo((1U << 2) | (1U << 9)));
            Assert.That(replica.ChainLinks, Has.Count.EqualTo(1));
            Assert.That(replica.ChainLinks[0].DescriptionId,
                Is.EqualTo(descriptionId));
            CardInstanceState replicaMonster =
                replica.Players[0].MonsterInstances[2];
            CardInstanceState replicaSpell =
                replica.Players[0].SpellTrapInstances[1];
            Assert.That(replicaMonster.Counters[0x1234], Is.EqualTo(3));
            Assert.That(replicaSpell.EquippedToRuntimeId,
                Is.EqualTo(replicaMonster.RuntimeId));
            Assert.That(replicaMonster.TargetRuntimeIds,
                Does.Contain(replicaSpell.RuntimeId));

            SetProperty(counter, nameof(DuelEvent.Value), 1U);
            host.Apply(counter);
            object after = CreateState(host, null);
            Assert.That(Field<ulong>(after, "publicStateHash"),
                Is.Not.EqualTo(hashBefore));
        }

        [Test]
        public void StructuredPromptMetadataSurvivesWireRoundTrip()
        {
            var prompt = new DuelPrompt();
            SetProperty(prompt, nameof(DuelPrompt.RequestId), 321UL);
            SetProperty(prompt, nameof(DuelPrompt.Message),
                CoreMessage.SelectCounter);
            SetProperty(prompt, nameof(DuelPrompt.Player), (byte)0);
            SetProperty(prompt, nameof(DuelPrompt.Title), "Counters");
            SetProperty(prompt, nameof(DuelPrompt.MaximumSelections), 4U);
            SetProperty(prompt, nameof(DuelPrompt.RequiresOrderedSelection),
                true);
            SetProperty(prompt, nameof(DuelPrompt.RequiresMaskSelection),
                true);
            SetProperty(prompt, nameof(DuelPrompt.CounterType),
                (ushort)0x55AA);
            SetProperty(prompt, nameof(DuelPrompt.RequiredCounterCount),
                (ushort)7);
            SetProperty(prompt, nameof(DuelPrompt.MaskWidth), (byte)64);

            DuelPrompt restored = Apply(
                CreateState(CreatePopulatedHostState(), prompt),
                new DuelPresentationState(null));

            Assert.That(restored.RequiresOrderedSelection, Is.True);
            Assert.That(restored.RequiresMaskSelection, Is.True);
            Assert.That(restored.CounterType, Is.EqualTo(0x55AA));
            Assert.That(restored.RequiredCounterCount, Is.EqualTo(7));
            Assert.That(restored.MaskWidth, Is.EqualTo(64));
        }

        [Test]
        public void PublicHashChangesWhenPendingPromptChanges()
        {
            DuelPresentationState host = CreatePopulatedHostState();
            byte[] response = { 1, 0, 0, 0 };
            DuelPrompt first = CreatePrivateOpponentPrompt(response);
            object firstState = CreateState(host, first);
            ulong firstHash = Field<ulong>(firstState, "publicStateHash");

            SetProperty(first, nameof(DuelPrompt.RequestId), 992UL);
            foreach (DuelChoice choice in first.Choices)
                SetProperty(choice, nameof(DuelChoice.RequestId), 992UL);
            object secondState = CreateState(host, first);
            ulong secondHash = Field<ulong>(secondState, "publicStateHash");

            Assert.That(secondHash, Is.Not.EqualTo(firstHash));
        }

        [Test]
        public void GoldenTraceChainLifecycleClearsOnlyTransientState()
        {
            DuelPresentationState state = CreatePopulatedHostState();
            CardLocation monster = CardLocationAt(
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                FaceUpAttack);

            var becomeTarget = new DuelEvent();
            SetProperty(becomeTarget, nameof(DuelEvent.Message),
                CoreMessage.BecomeTarget);
            SetProperty(becomeTarget, nameof(DuelEvent.CurrentLocations),
                new[] { monster });
            state.Apply(becomeTarget);

            var counter = new DuelEvent();
            SetProperty(counter, nameof(DuelEvent.Message),
                CoreMessage.AddCounter);
            SetProperty(counter, nameof(DuelEvent.Current), monster);
            SetProperty(counter, nameof(DuelEvent.CounterType), (ushort)1);
            SetProperty(counter, nameof(DuelEvent.Value), 2U);
            state.Apply(counter);

            foreach ((CoreMessage message, DuelChainLinkStatus status) in new[]
                     {
                         (CoreMessage.Chaining,
                             DuelChainLinkStatus.Chaining),
                         (CoreMessage.Chained,
                             DuelChainLinkStatus.Chained),
                         (CoreMessage.ChainSolving,
                             DuelChainLinkStatus.Solving),
                         (CoreMessage.ChainSolved,
                             DuelChainLinkStatus.Solved)
                     })
            {
                var chainEvent = new DuelEvent();
                SetProperty(chainEvent, nameof(DuelEvent.Message), message);
                SetProperty(chainEvent, nameof(DuelEvent.Value), 1U);
                SetProperty(chainEvent, nameof(DuelEvent.Player), (byte)0);
                SetProperty(chainEvent, nameof(DuelEvent.Code), HostXyz);
                SetProperty(chainEvent, nameof(DuelEvent.Current), monster);
                state.Apply(chainEvent);
                Assert.That(state.ChainLinks[0].Status,
                    Is.EqualTo(status));
            }

            var chainEnd = new DuelEvent();
            SetProperty(chainEnd, nameof(DuelEvent.Message),
                CoreMessage.ChainEnd);
            state.Apply(chainEnd);

            CardInstanceState card = state.Players[0].MonsterInstances[2];
            Assert.That(state.ChainEndPendingReconciliation, Is.True);
            Assert.That(state.ChainLinks, Has.Count.EqualTo(1));
            Assert.That(card.IsTemporaryTarget, Is.True);
            Assert.That(card.Counters[1], Is.EqualTo(2));

            state.CompleteChainEndReconciliation();
            Assert.That(state.ChainEndPendingReconciliation, Is.False);
            Assert.That(state.ChainLinks, Is.Empty);
            Assert.That(card.IsTemporaryTarget, Is.False);

            DuelPresentationSnapshot snapshot = state.CaptureSnapshot();
            var restored = new DuelPresentationState(null);
            restored.RestoreSnapshot(snapshot);
            Assert.That(restored.ChainLinks, Is.Empty);
            Assert.That(
                restored.Players[0].MonsterInstances[2].Counters[1],
                Is.EqualTo(2));
        }

        [Test]
        public void GoldenTraceFourLinkChainResolvesBackwardsAndDistinguishesOutcomes()
        {
            DuelPresentationState state = CreatePopulatedHostState();
            CardLocation monster = CardLocationAt(
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                FaceUpAttack);

            for (uint chainIndex = 1; chainIndex <= 4; chainIndex++)
            {
                var chaining = new DuelEvent();
                SetProperty(chaining, nameof(DuelEvent.Message),
                    CoreMessage.Chaining);
                SetProperty(chaining, nameof(DuelEvent.Value), chainIndex);
                SetProperty(chaining, nameof(DuelEvent.Player),
                    (byte)(chainIndex % 2));
                SetProperty(chaining, nameof(DuelEvent.Code), HostXyz);
                SetProperty(chaining, nameof(DuelEvent.DescriptionId),
                    0x1000000000000000UL + chainIndex);
                SetProperty(chaining, nameof(DuelEvent.Current), monster);
                state.Apply(chaining);

                var chained = new DuelEvent();
                SetProperty(chained, nameof(DuelEvent.Message),
                    CoreMessage.Chained);
                SetProperty(chained, nameof(DuelEvent.Value), chainIndex);
                state.Apply(chained);
            }

            Assert.That(
                state.ChainLinks.Select(link => link.ChainIndex),
                Is.EqualTo(new uint[] { 1, 2, 3, 4 }));
            Assert.That(
                state.ChainLinks.Select(link => link.DescriptionId),
                Is.EqualTo(new ulong[]
                {
                    0x1000000000000001UL,
                    0x1000000000000002UL,
                    0x1000000000000003UL,
                    0x1000000000000004UL
                }));

            var solvingOrder = new List<uint>();
            for (uint chainIndex = 4; chainIndex >= 1; chainIndex--)
            {
                var solving = new DuelEvent();
                SetProperty(solving, nameof(DuelEvent.Message),
                    CoreMessage.ChainSolving);
                SetProperty(solving, nameof(DuelEvent.Value), chainIndex);
                state.Apply(solving);
                solvingOrder.Add(chainIndex);
                Assert.That(
                    state.ChainLinks.Single(link =>
                        link.ChainIndex == chainIndex).Status,
                    Is.EqualTo(DuelChainLinkStatus.Solving));

                var outcome = new DuelEvent();
                CoreMessage message = chainIndex == 2
                    ? CoreMessage.ChainNegated
                    : chainIndex == 3
                        ? CoreMessage.ChainDisabled
                        : CoreMessage.ChainSolved;
                SetProperty(outcome, nameof(DuelEvent.Message), message);
                SetProperty(outcome, nameof(DuelEvent.Value), chainIndex);
                state.Apply(outcome);
            }

            Assert.That(solvingOrder,
                Is.EqualTo(new uint[] { 4, 3, 2, 1 }));
            Assert.That(state.ChainLinks.Single(link =>
                    link.ChainIndex == 2).Status,
                Is.EqualTo(DuelChainLinkStatus.Negated));
            Assert.That(state.ChainLinks.Single(link =>
                    link.ChainIndex == 3).Status,
                Is.EqualTo(DuelChainLinkStatus.Disabled));
            Assert.That(
                state.Players[0].MonsterZones[2],
                Is.EqualTo(HostXyz),
                "Negating/disabling an effect must not imply destruction.");
        }

        [Test]
        public void PhaseStateChangesOnlyWhenTheCorePublishesNewPhase()
        {
            var state = new DuelPresentationState(null);
            var prompt = new DuelPrompt();
            SetProperty(prompt, nameof(DuelPrompt.Message),
                CoreMessage.SelectIdleCommand);
            SetProperty(prompt, nameof(DuelPrompt.Player), (byte)0);
            var waiting = new DuelEvent();
            SetProperty(waiting, nameof(DuelEvent.Message),
                CoreMessage.SelectIdleCommand);
            SetProperty(waiting, nameof(DuelEvent.Prompt), prompt);
            state.Apply(waiting);

            Assert.That(state.Phase, Is.EqualTo(0));

            var newPhase = new DuelEvent();
            SetProperty(newPhase, nameof(DuelEvent.Message),
                CoreMessage.NewPhase);
            SetProperty(newPhase, nameof(DuelEvent.Value), 0x04U);
            state.Apply(newPhase);

            Assert.That(state.Phase, Is.EqualTo(0x04U));
        }

        [Test]
        public void AuthoritativeQueryParserReadsPersistentCardMetadata()
        {
            var buffer = new List<byte> { 0, 0, 0, 0 };
            void Block(uint flag, params byte[] data)
            {
                UInt16(buffer, checked((ushort)(4 + data.Length)));
                UInt32(buffer, flag);
                buffer.AddRange(data);
            }
            byte[] U32(uint value)
            {
                var result = new List<byte>();
                UInt32(result, value);
                return result.ToArray();
            }
            byte[] Address(
                byte controller,
                byte location,
                uint sequence,
                uint position)
            {
                var result = new List<byte>();
                Location(
                    result,
                    controller,
                    location,
                    sequence,
                    position);
                return result.ToArray();
            }

            Block(0x1, U32(HostXyz));
            Block(0x2, U32(FaceUpAttack));
            Block(0x4000, Address(
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                FaceUpAttack));
            var targets = new List<byte>();
            UInt32(targets, 1);
            targets.AddRange(Address(
                1,
                (byte)DuelLocation.SpellTrapZone,
                3,
                FaceDownDefense));
            Block(0x8000, targets.ToArray());
            var counters = new List<byte>();
            UInt32(counters, 2);
            UInt32(counters, 0x1234 | (3U << 16));
            UInt32(counters, 0x5678 | (9U << 16));
            Block(0x20000, counters.ToArray());
            Block(0x40000, new byte[] { 1 });
            Block(0x80000, U32(0xA5A5));
            Block(0x100000, new byte[] { 1 });
            var link = new List<byte>();
            UInt32(link, 4);
            UInt32(link, 0x55);
            Block(0x800000, link.ToArray());
            Block(0x80000000);
            byte[] encoded = buffer.ToArray();
            uint length = checked((uint)encoded.Length - 4);
            encoded[0] = (byte)length;
            encoded[1] = (byte)(length >> 8);
            encoded[2] = (byte)(length >> 16);
            encoded[3] = (byte)(length >> 24);

            MethodInfo parser = typeof(OcgDuelEngine).GetMethod(
                "TryReadLocationQuery",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(parser, Is.Not.Null);
            object[] arguments = { encoded, null };

            Assert.That((bool)parser.Invoke(null, arguments), Is.True);
            var cards = (OcgFieldCardSnapshot[])arguments[1];
            Assert.That(cards, Has.Length.EqualTo(1));
            OcgFieldCardSnapshot card = cards[0];
            Assert.That(card.Code, Is.EqualTo(HostXyz));
            Assert.That(card.Owner, Is.EqualTo(1));
            Assert.That(card.IsPublic, Is.True);
            Assert.That(card.Status, Is.EqualTo(0xA5A5));
            Assert.That(card.LinkRating, Is.EqualTo(4));
            Assert.That(card.LinkMarkers, Is.EqualTo(0x55));
            Assert.That(card.CounterTypes,
                Is.EqualTo(new ushort[] { 0x1234, 0x5678 }));
            Assert.That(card.CounterAmounts,
                Is.EqualTo(new uint[] { 3, 9 }));
            Assert.That(card.EquipTarget.Sequence, Is.EqualTo(1));
            Assert.That(card.TargetCards[0].Controller, Is.EqualTo(1));
            Assert.That(card.TargetCards[0].Sequence, Is.EqualTo(3));
        }

        private object CreateState(
            DuelPresentationState state,
            DuelPrompt prompt,
            byte recipient = 0)
        {
            MethodInfo method = protocolType.GetMethod(
                "CreateState",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            object result = method.Invoke(
                null,
                new object[] { state, prompt, recipient, 17, "ok" });
            Assert.That(result, Is.Not.Null);
            return result;
        }

        private DuelPrompt Apply(
            object networkState,
            DuelPresentationState destination)
        {
            MethodInfo method = protocolType.GetMethod(
                "Apply",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            object[] arguments = { networkState, destination, null, null };
            method.Invoke(null, arguments);
            return arguments[3] as DuelPrompt;
        }

        private static DuelPresentationState CreatePopulatedHostState()
        {
            var state = new DuelPresentationState(null);

            ApplyMove(
                state,
                HostXyz,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                2,
                FaceUpAttack);
            ApplyMove(
                state,
                FirstMaterial,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                FaceUpAttack);
            ApplyMove(
                state,
                SecondMaterial,
                0,
                0,
                0,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                FaceUpAttack);
            ApplyMove(
                state,
                FirstMaterial,
                0,
                (byte)DuelLocation.MonsterZone,
                0,
                0,
                (byte)DuelLocation.Overlay,
                2,
                0);
            ApplyMove(
                state,
                SecondMaterial,
                0,
                (byte)DuelLocation.MonsterZone,
                1,
                0,
                (byte)DuelLocation.Overlay,
                2,
                1);

            ApplyMove(
                state,
                HiddenMonster,
                1,
                0,
                0,
                1,
                (byte)DuelLocation.MonsterZone,
                2,
                FaceDownDefense);
            ApplyMove(
                state,
                HiddenSpell,
                1,
                0,
                0,
                1,
                (byte)DuelLocation.SpellTrapZone,
                3,
                FaceDownDefense);

            return state;
        }

        private static DuelPrompt CreatePrivateOpponentPrompt(
            byte[] response)
        {
            var prompt = new DuelPrompt();
            SetProperty(prompt, nameof(DuelPrompt.RequestId), 991UL);
            SetProperty(prompt, nameof(DuelPrompt.Message),
                CoreMessage.SelectCard);
            SetProperty(prompt, nameof(DuelPrompt.Player), (byte)0);
            SetProperty(prompt, nameof(DuelPrompt.Title), "Choose");
            SetProperty(prompt, nameof(DuelPrompt.MinimumSelections), 1U);
            SetProperty(prompt, nameof(DuelPrompt.MaximumSelections), 1U);

            var choice = new DuelChoice();
            SetProperty(choice, nameof(DuelChoice.RequestId), 991UL);
            SetProperty(choice, nameof(DuelChoice.Label), "Secret card");
            SetProperty(choice, nameof(DuelChoice.CardCode), HiddenMonster);
            SetProperty(choice, nameof(DuelChoice.Response), response);
            SetProperty(choice, nameof(DuelChoice.HasLocation), true);
            SetProperty(choice, nameof(DuelChoice.Controller), (byte)1);
            SetProperty(
                choice,
                nameof(DuelChoice.Location),
                (byte)DuelLocation.MonsterZone);
            SetProperty(choice, nameof(DuelChoice.Sequence), 2U);
            SetProperty(choice, nameof(DuelChoice.ChoiceIndex), 4);
            SetProperty(
                choice,
                nameof(DuelChoice.DescriptionId),
                0x1122334455667788UL);
            SetProperty(choice, nameof(DuelChoice.SumValue), 73U);
            prompt.Choices.Add(choice);
            return prompt;
        }

        private static DuelChoice EffectChoice(
            ulong requestId,
            ulong runtimeId,
            int candidateIndex,
            uint cardCode,
            ulong descriptionId,
            byte[] response)
        {
            var choice = new DuelChoice();
            SetProperty(choice, nameof(DuelChoice.RequestId), requestId);
            SetProperty(choice, nameof(DuelChoice.RuntimeId), runtimeId);
            SetProperty(choice, nameof(DuelChoice.Label), "Ativar efeito");
            SetProperty(choice, nameof(DuelChoice.CardCode), cardCode);
            SetProperty(choice, nameof(DuelChoice.Response), response);
            SetProperty(choice, nameof(DuelChoice.HasLocation), true);
            SetProperty(choice, nameof(DuelChoice.Controller), (byte)0);
            SetProperty(choice, nameof(DuelChoice.Location),
                (byte)DuelLocation.MonsterZone);
            SetProperty(choice, nameof(DuelChoice.Sequence), 0U);
            SetProperty(
                choice,
                nameof(DuelChoice.ChoiceIndex),
                candidateIndex);
            SetProperty(choice, nameof(DuelChoice.DescriptionId),
                descriptionId);
            return choice;
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

        private static DuelEvent SummonEvent(
            CoreMessage message,
            uint code,
            byte controller,
            uint sequence,
            uint position)
        {
            var payload = new List<byte>();
            UInt32(payload, code);
            Location(
                payload,
                controller,
                (byte)DuelLocation.MonsterZone,
                sequence,
                position);
            return Decode(message, payload);
        }

        private static CardLocation CardLocationAt(
            byte controller,
            byte location,
            uint sequence,
            uint position)
        {
            var result = new CardLocation();
            SetProperty(result, nameof(CardLocation.Controller), controller);
            SetProperty(result, nameof(CardLocation.Location), location);
            SetProperty(result, nameof(CardLocation.Sequence), sequence);
            SetProperty(result, nameof(CardLocation.Position), position);
            return result;
        }

        private static OcgFieldSnapshot FieldSnapshotWithSpell(
            uint code,
            uint position)
        {
            var card = new OcgFieldCardSnapshot();
            SetProperty(card, nameof(OcgFieldCardSnapshot.Code), code);
            SetProperty(card, nameof(OcgFieldCardSnapshot.Position), position);
            SetProperty(card, nameof(OcgFieldCardSnapshot.Owner), (byte)1);
            var spells = new OcgFieldCardSnapshot[8];
            spells[3] = card;
            var player0 = new OcgDuelistFieldSnapshot();
            var player1 = new OcgDuelistFieldSnapshot();
            SetProperty(
                player1,
                nameof(OcgDuelistFieldSnapshot.Spells),
                spells);
            var snapshot = new OcgFieldSnapshot();
            SetProperty(
                snapshot,
                nameof(OcgFieldSnapshot.Players),
                new[] { player0, player1 });
            return snapshot;
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

        private static void UInt16(List<byte> bytes, ushort value)
        {
            bytes.Add((byte)(value & 0xFF));
            bytes.Add((byte)(value >> 8));
        }

        private static void SetProperty(
            object target,
            string name,
            object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance);
            Assert.That(property, Is.Not.Null);
            MethodInfo setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(target, new[] { value });
        }

        private static T Field<T>(object target, string name)
        {
            Assert.That(target, Is.Not.Null);
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(target);
        }

        private static Type FindType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type result = assembly.GetType(fullName, false);
                if (result != null)
                    return result;
            }
            return null;
        }
    }
}
