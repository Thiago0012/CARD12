using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ArcaneDuel.Tests.PlayMode
{
    public sealed class MultiplayerCrossplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator OptionsExposeAllFiveGraphicsLevels()
        {
            SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
            yield return null;
            yield return null;

            MonoBehaviour frontend = Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .First(component =>
                    component != null &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "GameFrontendBootstrap");
            frontend.GetType().GetMethod(
                    "ShowAnimationOptions",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(frontend, null);
            yield return null;

            Transform row = FindDescendant(
                frontend.transform,
                "Qualidade gráfica");
            Assert.That(row, Is.Not.Null);
            Assert.That(
                row.GetComponentsInChildren<Button>(true).Length,
                Is.EqualTo(5));
            string[] labels = row.GetComponentsInChildren<Text>(true)
                .Select(text => text.text)
                .ToArray();
            Assert.That(labels, Does.Contain("M. BAIXO"));
            Assert.That(labels, Does.Contain("M. ALTO"));
        }

        [UnityTest]
        public IEnumerator OnlineSessionUsesVersionedLowFrequencyRelayConfig()
        {
            Type sessionType = TypeByName(
                "ArcaneArena.Multiplayer.DuelOnlineSession");
            object session = sessionType.GetMethod(
                    "EnsureInstance",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
            yield return null;

            Assert.That(session, Is.Not.Null);
            BindingFlags fields = BindingFlags.Instance |
                                  BindingFlags.NonPublic;
            var manager = sessionType.GetField("networkManager", fields)
                ?.GetValue(session) as NetworkManager;
            var transport = sessionType.GetField("transport", fields)
                ?.GetValue(session) as UnityTransport;
            Assert.That(manager, Is.Not.Null);
            Assert.That(transport, Is.Not.Null);
            Assert.That(manager.NetworkConfig.NetworkTransport, Is.SameAs(transport));
            Assert.That(manager.NetworkConfig.ProtocolVersion, Is.EqualTo(2));
            Assert.That(manager.NetworkConfig.TickRate, Is.EqualTo(20));
            Assert.That(manager.NetworkConfig.ConnectionApproval, Is.False);
            Assert.That(manager.NetworkConfig.EnableSceneManagement, Is.False);

            FieldInfo protocol = sessionType.GetField(
                "ProtocolVersion",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(protocol, Is.Not.Null);
            Assert.That(
                protocol.GetRawConstantValue(),
                Is.EqualTo("arcane-duel-online-v2"));
        }

        [Test]
        public void NetworkSnapshotRotatesPerspectiveAndHidesOpponentHand()
        {
            Type protocolType = TypeByName(
                "ArcaneArena.Multiplayer.DuelNetworkProtocol");
            var state = new DuelPresentationState(null);
            state.ConfigureDeckCounts(35, 8, 37, 12);
            state.Players[0].Hand.Add(100u);
            state.Players[0].Hand.Add(101u);
            state.Players[1].Hand.Add(200u);
            state.Players[1].MonsterZones[0] = 300u;

            object networkState = protocolType.GetMethod(
                    "CreateState",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(
                    null,
                    new object[] { state, null, (byte)1, 7, "ok" });
            Assert.That(networkState, Is.Not.Null);
            object snapshot = networkState.GetType()
                .GetField("snapshot")
                ?.GetValue(networkState);
            Array players = snapshot?.GetType()
                .GetField("players")
                ?.GetValue(snapshot) as Array;
            Assert.That(players, Is.Not.Null);
            Assert.That(players.Length, Is.EqualTo(2));

            uint[] localHand = Field<uint[]>(players.GetValue(0), "hand");
            uint[] opponentHand = Field<uint[]>(players.GetValue(1), "hand");
            Assert.That(localHand, Is.EqualTo(new[] { 200u }));
            Assert.That(opponentHand, Is.EqualTo(new[] { 0u, 0u }));
            Assert.That(
                Field<int>(players.GetValue(0), "deckCount"),
                Is.EqualTo(37));
            Assert.That(
                Field<int>(players.GetValue(1), "deckCount"),
                Is.EqualTo(35));
        }

        [Test]
        public void AuthoritativeSnapshotRebuildsTheRemoteField()
        {
            var host = new DuelPresentationState(null);
            host.ConfigureDeckCounts(44, 6, 48, 9);
            host.Players[0].Hand.AddRange(new[] { 101u, 102u });
            host.Players[1].Hand.AddRange(new[] { 201u, 202u });
            host.Players[0].MonsterZones[1] = 301u;
            host.Players[0].MonsterPositions[1] = 0x2u;
            host.Players[0].SpellTrapZones[2] = 302u;
            host.Players[0].SpellTrapPositions[2] = 0x2u;
            host.Players[1].MonsterZones[3] = 401u;
            host.Players[1].MonsterPositions[3] = 0x1u;
            host.Players[1].SpellTrapZones[4] = 402u;
            host.Players[1].SpellTrapPositions[4] = 0x1u;
            host.Players[0].Graveyard.Add(501u);
            host.Players[1].Graveyard.Add(601u);
            host.Players[0].Banished.Add(701u);
            host.Players[1].Banished.Add(801u);

            Type protocolType = TypeByName(
                "ArcaneArena.Multiplayer.DuelNetworkProtocol");
            object message = protocolType.GetMethod(
                    "CreateState",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(
                    null,
                    new object[] { host, null, (byte)1, 12, "estado confirmado" });
            var remote = new DuelPresentationState(null);

            object[] applyArguments = { message, remote, null, null };
            protocolType.GetMethod(
                    "Apply",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, applyArguments);

            Assert.That(remote.Players[0].DeckCount, Is.EqualTo(48));
            Assert.That(remote.Players[0].ExtraDeckCount, Is.EqualTo(9));
            Assert.That(remote.Players[0].Hand, Is.EqualTo(new[] { 201u, 202u }));
            Assert.That(remote.Players[0].MonsterZones[3], Is.EqualTo(401u));
            Assert.That(remote.Players[0].SpellTrapZones[4], Is.EqualTo(402u));
            Assert.That(remote.Players[0].Graveyard, Is.EqualTo(new[] { 601u }));
            Assert.That(remote.Players[0].Banished, Is.EqualTo(new[] { 801u }));
            Assert.That(remote.Players[1].Hand, Is.EqualTo(new[] { 0u, 0u }));
            Assert.That(remote.Players[1].MonsterZones[1], Is.EqualTo(0u));
            Assert.That(remote.Players[1].SpellTrapZones[2], Is.EqualTo(0u));
            Assert.That(remote.Players[1].Graveyard, Is.EqualTo(new[] { 501u }));
            Assert.That(remote.Players[1].Banished, Is.EqualTo(new[] { 0u }));
        }

        [Test]
        public void V2HandshakeAllowsBuildsWithDifferentContentRevisions()
        {
            Type sessionType = TypeByName(
                "ArcaneArena.Multiplayer.DuelOnlineSession");
            Type helloType = sessionType.GetNestedType(
                "HelloPayload",
                BindingFlags.NonPublic);
            object hello = Activator.CreateInstance(helloType, true);
            string protocol = (string)sessionType.GetField(
                    "ProtocolVersion",
                    BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetRawConstantValue();

            helloType.GetField("protocolVersion")?.SetValue(hello, protocol);
            helloType.GetField("compatibility")?.SetValue(
                hello,
                "a-build-different-content-revision");
            helloType.GetField("coreApiVersion")?.SetValue(
                hello,
                ProjectIdentity.CoreApiVersion);
            helloType.GetField("coreCommit")?.SetValue(
                hello,
                "different-core-commit-with-same-api");
            Type loadoutType = TypeByName(
                "ArcaneArena.Frontend.DuelDeckLoadout");
            object loadout = Activator.CreateInstance(loadoutType, true);
            loadoutType.GetField("mainDeckCardIds")?.SetValue(
                loadout,
                Enumerable.Repeat("10000001", 40).ToList());
            helloType.GetField("loadout")?.SetValue(hello, loadout);

            MethodInfo validate = sessionType.GetMethod(
                "ValidateHello",
                BindingFlags.Static | BindingFlags.NonPublic);
            object[] arguments = { hello, null };

            bool accepted = (bool)validate.Invoke(null, arguments);

            Assert.That(accepted, Is.True, arguments[1] as string);
        }

        private static T Field<T>(object instance, string name)
        {
            return (T)instance.GetType().GetField(name)?.GetValue(instance);
        }

        private static Type TypeByName(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .First(type => type != null);
        }

        private static Transform FindDescendant(
            Transform parent,
            string name)
        {
            if (parent == null)
                return null;
            if (parent.name == name)
                return parent;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform found = FindDescendant(
                    parent.GetChild(index),
                    name);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
