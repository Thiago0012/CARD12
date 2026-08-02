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

        private object CreateState(
            DuelPresentationState state,
            DuelPrompt prompt)
        {
            MethodInfo method = protocolType.GetMethod(
                "CreateState",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            object result = method.Invoke(
                null,
                new object[] { state, prompt, (byte)0, 17, "ok" });
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
