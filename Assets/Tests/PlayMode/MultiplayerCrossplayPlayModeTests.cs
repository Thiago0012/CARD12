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
            Assert.That(manager.NetworkConfig.ConnectionApproval, Is.True);
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
