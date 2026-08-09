using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ArcaneDuel.DuelEngine.Protocol;
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
        [UnityTearDown]
        public IEnumerator DestroyPersistentOnlineSessionAfterEachTest()
        {
            MonoBehaviour[] sessions = Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .Where(component =>
                    component != null &&
                    component.GetType().FullName ==
                    "ArcaneArena.Multiplayer.DuelOnlineSession")
                .ToArray();
            foreach (MonoBehaviour session in sessions)
            {
                if (session != null)
                    UnityEngine.Object.Destroy(session.gameObject);
            }

            // NetworkManager.OnDestroy performs the synchronous NGO cleanup.
            // Let Unity complete that destruction before the next test or the
            // PlayMode domain is closed, so Netcode never disposes it twice.
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitLobbyExitClearsStateAndCanRunAgain()
        {
            Type sessionType = TypeByName(
                "ArcaneArena.Multiplayer.DuelOnlineSession");
            object session = sessionType.GetMethod(
                    "EnsureInstance",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
            Assert.That(session, Is.Not.Null);

            BindingFlags fields = BindingFlags.Instance |
                                  BindingFlags.NonPublic;
            sessionType.GetField("roomCode", fields)
                ?.SetValue(session, "ABC123");
            sessionType.GetField("currentMatchId", fields)
                ?.SetValue(session, "stale-match");
            sessionType.GetField("matchStarted", fields)
                ?.SetValue(session, true);
            FieldInfo role = sessionType.GetField("role", fields);
            role?.SetValue(
                session,
                Enum.Parse(role.FieldType, "Host"));

            MethodInfo leave = sessionType.GetMethod(
                "LeaveRoom",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(leave, Is.Not.Null);
            leave.Invoke(session, null);
            for (int frame = 0; frame < 120 &&
                 (bool)sessionType.GetField(
                         "connectionOperationInProgress",
                         fields)
                     .GetValue(session);
                 frame++)
            {
                yield return null;
            }

            Assert.That(role.GetValue(session).ToString(), Is.EqualTo("None"));
            Assert.That(
                sessionType.GetField("roomCode", fields)?.GetValue(session),
                Is.EqualTo(string.Empty));
            Assert.That(
                sessionType.GetField("currentMatchId", fields)?.GetValue(session),
                Is.EqualTo(string.Empty));
            Assert.That(
                sessionType.GetField("matchStarted", fields)?.GetValue(session),
                Is.False);
            Assert.That(
                sessionType.GetField("connectionOperationInProgress", fields)
                    ?.GetValue(session),
                Is.False);

            leave.Invoke(session, null);
            yield return null;
            Assert.That(
                sessionType.GetField("connectionOperationInProgress", fields)
                    ?.GetValue(session),
                Is.False,
                "Leaving an already cleared room must remain idempotent.");
        }

        [UnityTest]
        public IEnumerator OnlineArenaDoesNotStartASecondLocalDuel()
        {
            DuelOnlineBridge.BeginOnlineArenaTransition();
            SceneManager.LoadScene("DuelArena");
            yield return null;
            yield return null;
            yield return null;

            try
            {
                MonoBehaviour arena = Resources
                    .FindObjectsOfTypeAll<MonoBehaviour>()
                    .First(component =>
                        component != null &&
                        component.gameObject.activeInHierarchy &&
                        component.GetType().Name == "CardArenaBootstrap");
                DuelArenaController controller =
                    arena.GetComponent<DuelArenaController>();

                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.PresentationState, Is.Not.Null);
                Assert.That(controller.CurrentPrompt, Is.Null);
                Assert.That(
                    controller.PresentationState.Players[0].Hand,
                    Is.Empty,
                    "The online arena must not deal a temporary local hand.");
                Assert.That(
                    controller.PresentationState.Players[1].Hand,
                    Is.Empty,
                    "Only the Relay authority may deal the opening hands.");

                controller.ConfigureNetworkReplica(0);
                Assert.That(controller.IsNetworkReplica, Is.True);
            }
            finally
            {
                DuelOnlineBridge.CompleteOnlineArenaTransition();
            }
        }

        [UnityTest]
        public IEnumerator SlowRelayResyncRebasesPendingResponseVersion()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .First(component =>
                    component != null &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "CardArenaBootstrap");
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            controller.ConfigureNetworkReplica(0);

            var prompt = new DuelPrompt();
            SetProperty(prompt, nameof(DuelPrompt.RequestId), 4401UL);
            SetProperty(prompt, nameof(DuelPrompt.Message),
                CoreMessage.SelectCard);
            SetProperty(prompt, nameof(DuelPrompt.Player), (byte)0);
            SetProperty(prompt, nameof(DuelPrompt.Title), "Choose");

            Type protocolType = TypeByName(
                "ArcaneArena.Multiplayer.DuelNetworkProtocol");
            object networkState = protocolType.GetMethod(
                    "CreateState",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[]
                {
                    controller.PresentationState,
                    prompt,
                    (byte)0,
                    2,
                    "slow relay"
                });
            Assert.That(networkState, Is.Not.Null);
            networkState.GetType().GetField("stateVersion")
                ?.SetValue(networkState, 7UL);

            Type sessionType = TypeByName(
                "ArcaneArena.Multiplayer.DuelOnlineSession");
            object session = sessionType.GetMethod(
                    "EnsureInstance",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, null);
            BindingFlags hidden = BindingFlags.Instance |
                                  BindingFlags.NonPublic;
            sessionType.GetField("replicaController", hidden)
                ?.SetValue(session, controller);
            sessionType.GetField("pendingResponseRequestId", hidden)
                ?.SetValue(session, 4401UL);
            sessionType.GetField("pendingResponseBytes", hidden)
                ?.SetValue(session, new byte[] { 1 });
            sessionType.GetField("pendingCommandId", hidden)
                ?.SetValue(session, 9UL);
            sessionType.GetField("pendingClientSequence", hidden)
                ?.SetValue(session, 1U);
            sessionType.GetField("pendingExpectedStateVersion", hidden)
                ?.SetValue(session, 3UL);

            sessionType.GetMethod("ApplyReplicaState", hidden)
                ?.Invoke(session, new[] { networkState });

            Assert.That(
                sessionType.GetField("pendingExpectedStateVersion", hidden)
                    ?.GetValue(session),
                Is.EqualTo(7UL),
                "A retry must use the newest confirmed host version.");
            Assert.That(controller.PresentationDecisionLocked, Is.True);
            sessionType.GetMethod("ResetMatchState", hidden)
                ?.Invoke(session, new object[] { false });
        }

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
        public IEnumerator OptionsExposeActivationModesSelfChainAndSortOrder()
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
            MethodInfo showResponses = frontend.GetType().GetMethod(
                "ShowDuelResponseOptions",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(showResponses, Is.Not.Null);
            showResponses.Invoke(frontend, null);
            yield return null;

            Transform panel = FindDescendant(
                frontend.transform,
                "Respostas e Correntes");
            Assert.That(panel, Is.Not.Null);
            string[] labels = panel.GetComponentsInChildren<Text>(true)
                .Select(text => text.text)
                .ToArray();
            Assert.That(labels, Does.Contain("ON"));
            Assert.That(labels, Does.Contain("AUTO"));
            Assert.That(labels, Does.Contain("OFF"));
            Assert.That(
                labels.Any(label =>
                    label == "ATIVADO" || label == "DESATIVADO"),
                Is.True);
            Assert.That(
                labels.Any(label => label == "MANUAL" || label == "CORE"),
                Is.True);
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
            Assert.That(manager.NetworkConfig.ProtocolVersion, Is.EqualTo(11));
            Assert.That(manager.NetworkConfig.TickRate, Is.EqualTo(20));
            Assert.That(transport.HeartbeatTimeoutMS, Is.EqualTo(1000));
            Assert.That(transport.DisconnectTimeoutMS, Is.EqualTo(120000));
            Assert.That(manager.NetworkConfig.ConnectionApproval, Is.True);
            Assert.That(manager.NetworkConfig.ForceSamePrefabs, Is.False);
            Assert.That(manager.NetworkConfig.EnableSceneManagement, Is.False);
            Assert.That(manager.ConnectionApprovalCallback, Is.Not.Null);
            Assert.That(
                sessionType.GetField("sessionCoordinator", fields)
                    ?.GetValue(session),
                Is.Not.Null);

            FieldInfo protocol = sessionType.GetField(
                "ProtocolVersion",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(protocol, Is.Not.Null);
            Assert.That(
                protocol.GetRawConstantValue(),
                Is.EqualTo("arcane-duel-online-v11"));
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
            // A banished card without a face-down position is public to both
            // seats. Only face-down banished cards are redacted by v4.
            Assert.That(remote.Players[1].Banished, Is.EqualTo(new[] { 701u }));
        }

        [UnityTest]
        public IEnumerator InitialSnapshotBuildsBothPlayerPerspectivesAndRoutesActions()
        {
            SceneManager.LoadScene(ProjectIdentity.DuelScene);
            yield return null;
            yield return null;
            yield return null;

            MonoBehaviour arena = Resources
                .FindObjectsOfTypeAll<MonoBehaviour>()
                .First(component =>
                    component != null &&
                    component.gameObject.activeInHierarchy &&
                    component.GetType().Name == "CardArenaBootstrap");
            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            Assert.That(controller, Is.Not.Null);
            DuelPresentationState authoritative =
                controller.PresentationState;
            Assert.That(authoritative, Is.Not.Null);
            Assert.That(authoritative.Players[0].Hand.Count, Is.GreaterThan(0));
            Assert.That(authoritative.Players[1].Hand.Count, Is.GreaterThan(0));
            Assert.That(
                controller.CurrentPrompt,
                Is.Not.Null,
                "The authoritative host must own the playable Core prompt.");

            uint[] playerOneHand = authoritative.Players[0].Hand.ToArray();
            uint[] playerTwoHand = authoritative.Players[1].Hand.ToArray();

            Type protocolType = TypeByName(
                "ArcaneArena.Multiplayer.DuelNetworkProtocol");
            MethodInfo createState = protocolType.GetMethod(
                    "CreateState",
                    BindingFlags.Public | BindingFlags.Static);
            object playerOneState = createState?.Invoke(
                    null,
                    new object[]
                    {
                        authoritative,
                        controller.CurrentPrompt,
                        (byte)0,
                        1,
                        "player one snapshot applied"
                    });
            object playerTwoState = createState?.Invoke(
                    null,
                    new object[]
                    {
                        authoritative,
                        controller.CurrentPrompt,
                        (byte)1,
                        1,
                        "player two snapshot applied"
                    });
            Assert.That(playerOneState, Is.Not.Null);
            Assert.That(playerTwoState, Is.Not.Null);

            controller.ConfigureNetworkReplica(0);
            controller.ApplyNetworkState((IDuelNetworkState)playerOneState);
            yield return null;
            yield return null;

            DuelPresentationState playerOneReplica =
                controller.PresentationState;
            Assert.That(playerOneReplica.Players[0].DeckCount, Is.GreaterThan(0));
            Assert.That(playerOneReplica.Players[1].DeckCount, Is.GreaterThan(0));
            Assert.That(playerOneReplica.Players[0].Hand, Is.EqualTo(playerOneHand));
            Assert.That(playerOneReplica.Players[1].Hand.All(code => code == 0u));
            Assert.That(
                VisibleHandCount(arena),
                Is.EqualTo(playerOneHand.Length),
                "Player one must see every card in their local hand.");

            // Each recipient receives a perspective-rotated snapshot where
            // their own cards are logical P0, matching the production client.
            controller.ConfigureNetworkReplica(0);
            controller.ApplyNetworkState((IDuelNetworkState)playerTwoState);
            yield return null;
            yield return null;

            DuelPresentationState playerTwoReplica =
                controller.PresentationState;
            Assert.That(playerTwoReplica.Players[0].DeckCount, Is.GreaterThan(0));
            Assert.That(playerTwoReplica.Players[1].DeckCount, Is.GreaterThan(0));
            Assert.That(playerTwoReplica.Players[0].Hand, Is.EqualTo(playerTwoHand));
            Assert.That(playerTwoReplica.Players[1].Hand.All(code => code == 0u));
            Assert.That(
                VisibleHandCount(arena),
                Is.EqualTo(playerTwoHand.Length),
                "Player two must see every card in their local hand.");

            byte[] forwardedResponse = null;
            ulong forwardedRequestId = 0;
            Action<byte[], ulong> previousBridge =
                DuelOnlineBridge.SubmitReplicaResponse;
            try
            {
                DuelOnlineBridge.SubmitReplicaResponse = (response, requestId) =>
                {
                    forwardedResponse = response;
                    forwardedRequestId = requestId;
                };
                byte[] response = { 1, 2, 3 };
                Assert.That(
                    controller.SubmitCoreResponse(response, 77),
                    Is.True,
                    "The remote player action must enter the network bridge.");
                Assert.That(forwardedResponse, Is.EqualTo(response));
                Assert.That(forwardedRequestId, Is.EqualTo(77));
            }
            finally
            {
                DuelOnlineBridge.SubmitReplicaResponse = previousBridge;
            }
        }

        [Test]
        public void DrawPresentationKeepsTheAuthoritativeDrawPhase()
        {
            var state = new DuelPresentationState(null);
            state.Apply(PresentationEvent(CoreMessage.NewTurn, 1));
            state.Apply(PresentationEvent(
                CoreMessage.NewPhase,
                value: 0x001U));
            DuelEvent draw = PresentationEvent(
                CoreMessage.Draw,
                1,
                codes: new[] { 97268402U });

            Type protocolType = TypeByName(
                "ArcaneArena.Multiplayer.DuelNetworkProtocol");
            MethodInfo create = protocolType.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                    method.Name == "CreatePresentationEvent" &&
                    method.GetParameters().Length == 6);
            object wire = create.Invoke(
                null,
                new object[]
                {
                    draw,
                    state,
                    (byte)1,
                    7,
                    12,
                    "draw-phase-test"
                });
            state.Apply(PresentationEvent(
                CoreMessage.NewPhase,
                value: 0x004U));
            DuelEvent replay = (DuelEvent)protocolType.GetMethod(
                    "ToPresentationEvent",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new[] { wire });

            Assert.That(
                Field<uint>(wire, "presentationPhase"),
                Is.EqualTo(0x001U));
            Assert.That(replay.PresentationPhase, Is.EqualTo(0x001U));
            Assert.That(replay.Player, Is.EqualTo(0));
            Assert.That(replay.Codes, Is.EqualTo(new[] { 97268402U }));
        }

        [Test]
        public void V4HandshakeRejectsDifferentCardContentRevision()
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

            Assert.That(accepted, Is.False);
            Assert.That(arguments[1] as string,
                Does.Contain("mesma versão ONLINE v11"));
        }

        [Test]
        public void OnlineStartHasDeckStartAndArenaAcknowledgements()
        {
            Type sessionType = TypeByName(
                "ArcaneArena.Multiplayer.DuelOnlineSession");
            BindingFlags hidden = BindingFlags.NonPublic |
                                  BindingFlags.Static;
            Assert.That(
                sessionType.GetField("HelloRequestMessage", hidden)
                    ?.GetRawConstantValue(),
                Is.EqualTo("arcane.duel.hello-request.v4"));
            Assert.That(
                sessionType.GetField("ClientReadyMessage", hidden)
                    ?.GetRawConstantValue(),
                Is.EqualTo("arcane.duel.client-ready.v4"));
            Assert.That(
                sessionType.GetField("WirePacketMessage", hidden)
                    ?.GetRawConstantValue(),
                Is.EqualTo("arcane.duel.wire-packet.v4"));

            Type responseType = sessionType.GetNestedType(
                "ResponsePayload",
                BindingFlags.NonPublic);
            Assert.That(responseType?.GetField("commandId"), Is.Not.Null);
            Assert.That(responseType?.GetField("clientSequence"), Is.Not.Null);
            Assert.That(responseType?.GetField("expectedStateVersion"), Is.Not.Null);
            Assert.That(responseType?.GetField("matchId"), Is.Not.Null);

            Type readyType = sessionType.GetNestedType(
                "ClientReadyPayload",
                BindingFlags.NonPublic);
            Assert.That(readyType?.GetField("matchId"), Is.Not.Null);
            Assert.That(readyType?.GetField("deckReady"), Is.Not.Null);
            Assert.That(readyType?.GetField("startReceived"), Is.Not.Null);
            Assert.That(readyType?.GetField("arenaReady"), Is.Not.Null);

            MethodInfo matchIds = sessionType.GetMethod(
                "MatchIdsAreCompatible",
                hidden);
            Assert.That(matchIds, Is.Not.Null);
            Assert.That(
                sessionType.GetMethod(
                    "OnDuelSceneLoaded",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "A sessão persistente deve observar o carregamento da arena.");
            Assert.That(
                sessionType.GetMethod(
                    "AttachArenaAfterSceneInitialization",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "A arena deve ser anexada após concluir a inicialização da cena.");
            Assert.That(
                sessionType.GetMethod(
                    "MaintainArenaReadyHandshake",
                    BindingFlags.Instance | BindingFlags.NonPublic),
                Is.Not.Null,
                "O cliente deve repetir a confirmação até receber o snapshot inicial.");
            Assert.That(
                (bool)matchIds.Invoke(null, new object[] { "match-a", "match-a" }),
                Is.True);
            Assert.That(
                (bool)matchIds.Invoke(null, new object[] { "match-a", "match-b" }),
                Is.False);
            Assert.That(
                (bool)matchIds.Invoke(null, new object[] { "match-a", string.Empty }),
                Is.False);
        }

        [Test]
        public void ProjectionHashVerifiesExactPrivatePayloadWithoutLeakingIt()
        {
            Type protocolType = TypeByName(
                "ArcaneArena.Multiplayer.DuelNetworkProtocol");
            var state = new DuelPresentationState(null);
            state.ConfigureDeckCounts(35, 8, 37, 12);
            state.Players[0].Hand.AddRange(new[] { 100u, 101u });
            state.Players[1].Hand.AddRange(new[] { 200u, 201u });
            state.Players[0].MonsterZones[0] = 300u;
            state.Players[0].MonsterPositions[0] = 0x2u;
            state.Players[1].MonsterZones[1] = 400u;
            state.Players[1].MonsterPositions[1] = 0x1u;

            MethodInfo create = protocolType.GetMethod(
                "CreateState",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo hash = protocolType.GetMethod(
                "ComputePublicProjectionHash",
                BindingFlags.Public | BindingFlags.Static);
            object hostView = create?.Invoke(
                null,
                new object[] { state, null, (byte)0, 1, "ok" });
            object clientView = create?.Invoke(
                null,
                new object[] { state, null, (byte)1, 1, "ok" });
            hostView?.GetType().GetField("matchId")?.SetValue(hostView, "match");
            clientView?.GetType().GetField("matchId")?.SetValue(clientView, "match");

            ulong hostHash = (ulong)hash.Invoke(null, new[] { hostView });
            ulong clientHash = (ulong)hash.Invoke(null, new[] { clientView });
            Assert.That(hostHash, Is.Not.Zero);
            Assert.That(
                clientHash,
                Is.Not.EqualTo(hostHash),
                "Each recipient acknowledges the exact privacy-filtered payload it received.");

            state.Players[0].Hand[0] = 999999u;
            object changedPrivateView = create.Invoke(
                null,
                new object[] { state, null, (byte)0, 2, "ok" });
            changedPrivateView.GetType().GetField("matchId")
                ?.SetValue(changedPrivateView, "match");
            ulong changedPrivateHash = (ulong)hash.Invoke(
                null,
                new[] { changedPrivateView });
            Assert.That(
                changedPrivateHash,
                Is.Not.EqualTo(hostHash),
                "Changing the owner's visible private hand must change that owner's payload hash.");

            object unchangedOpponentView = create.Invoke(
                null,
                new object[] { state, null, (byte)1, 2, "ok" });
            unchangedOpponentView.GetType().GetField("matchId")
                ?.SetValue(unchangedOpponentView, "match");
            ulong unchangedOpponentHash = (ulong)hash.Invoke(
                null,
                new[] { unchangedOpponentView });
            Assert.That(
                unchangedOpponentHash,
                Is.EqualTo(clientHash),
                "A hidden opponent hand identity must not enter the recipient's payload hash.");
        }

        [Test]
        public void V4LogicalPayloadCompressesAndRoundTripsLargeSnapshots()
        {
            Type sessionType = TypeByName(
                "ArcaneArena.Multiplayer.DuelOnlineSession");
            Type logicalType = sessionType.GetNestedType(
                "LogicalMessage",
                BindingFlags.NonPublic);
            object stateMessage = Enum.Parse(logicalType, "State");
            byte[] source = System.Text.Encoding.UTF8.GetBytes(
                "{\"snapshot\":\"" + new string('A', 8192) + "\"}");
            MethodInfo encode = sessionType.GetMethod(
                "EncodeLogicalPayload",
                BindingFlags.Static | BindingFlags.NonPublic);
            byte[] encoded = encode?.Invoke(
                null,
                new object[] { stateMessage, source }) as byte[];

            Assert.That(encoded, Is.Not.Null);
            Assert.That(encoded.Length, Is.LessThan(source.Length));
            Assert.That(encoded[1], Is.EqualTo(1));

            MethodInfo decode = sessionType.GetMethod(
                "TryDecodeLogicalJson",
                BindingFlags.Static | BindingFlags.NonPublic);
            object[] arguments = { encoded, null, null, null };
            bool decoded = (bool)decode.Invoke(null, arguments);
            Assert.That(decoded, Is.True, arguments[3] as string);
            Assert.That(arguments[2] as string,
                Is.EqualTo(System.Text.Encoding.UTF8.GetString(source)));
        }

        private static T Field<T>(object instance, string name)
        {
            return (T)instance.GetType().GetField(name)?.GetValue(instance);
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

        private static DuelEvent PresentationEvent(
            CoreMessage message,
            byte player = 0,
            uint value = 0,
            uint[] codes = null)
        {
            var duelEvent = new DuelEvent();
            BindingFlags properties = BindingFlags.Instance |
                                      BindingFlags.Public |
                                      BindingFlags.NonPublic;
            typeof(DuelEvent).GetProperty("Message", properties)
                ?.SetValue(duelEvent, message);
            typeof(DuelEvent).GetProperty("Player", properties)
                ?.SetValue(duelEvent, player);
            typeof(DuelEvent).GetProperty("Value", properties)
                ?.SetValue(duelEvent, value);
            typeof(DuelEvent).GetProperty("Codes", properties)
                ?.SetValue(duelEvent, codes);
            return duelEvent;
        }

        private static int VisibleHandCount(MonoBehaviour arena)
        {
            object value = arena.GetType().GetField(
                    "handViews",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(arena);
            Assert.That(value, Is.InstanceOf<System.Collections.ICollection>());
            return ((System.Collections.ICollection)value).Count;
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
