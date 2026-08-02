using System;
using System.Collections;
using System.Text;
using System.Threading.Tasks;
using ArcaneArena;
using ArcaneArena.Frontend;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Persistent 1v1 Relay session. The host owns the only OCG Core. The
    /// joining client receives a perspective-filtered presentation mirror and
    /// can only submit responses for the prompt addressed to it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DuelOnlineSession : MonoBehaviour
    {
        private const string DuelArenaScene = "DuelArena";
        private const string ProtocolVersion = "arcane-duel-online-v2";
        private const string HelloMessage = "arcane.duel.hello.v2";
        private const string HelloAcceptedMessage = "arcane.duel.hello-accepted.v2";
        private const string HelloRejectedMessage = "arcane.duel.hello-rejected.v2";
        private const string StartMessage = "arcane.duel.start.v2";
        private const string StateMessage = "arcane.duel.state.v2";
        private const string ResponseMessage = "arcane.duel.response.v2";
        private const int MaxWireBytes = 96 * 1024;
        private const ushort NgoProtocolVersion = 2;
        private const uint NetworkTickRate = 20;
        private const int MaximumHandshakeAttempts = 12;
        private const float HandshakeRetrySeconds = 0.75f;

        private enum SessionRole
        {
            None,
            Host,
            Client
        }

        [Serializable]
        private sealed class HelloPayload
        {
            public string protocolVersion;
            public string compatibility;
            public string coreApiVersion;
            public string coreCommit;
            public DuelDeckLoadout loadout;
        }

        [Serializable]
        private sealed class ResponsePayload
        {
            public ulong requestId;
            public string responseBase64;
        }

        [Serializable]
        private sealed class StartPayload
        {
            public string protocolVersion;
            public string compatibility;
        }

        [Serializable]
        private sealed class HelloAcceptedPayload
        {
            public string protocolVersion;
            public string compatibility;
        }

        [Serializable]
        private sealed class HelloRejectedPayload
        {
            public string reason;
        }

        public static DuelOnlineSession Instance { get; private set; }

        private NetworkManager networkManager;
        private UnityTransport transport;
        private SessionRole role;
        private DuelDeckLoadout localLoadout;
        private DuelDeckLoadout remoteLoadout;
        private DuelArenaController hostController;
        private DuelArenaController replicaController;
        private DuelNetworkState pendingReplicaState;
        private ulong remoteClientId = ulong.MaxValue;
        private int nextStateSequence;
        private int lastReplicaSequence;
        private bool matchStarted;
        private Coroutine pendingStateBroadcast;
        private Coroutine helloRetry;
        private bool helloAccepted;
        private bool connectionOperationInProgress;
        private bool handlersRegistered;
        private bool showPanel;
        private bool focusJoinCode;
        private bool requestJoinFocus;
        private string joinCode = string.Empty;
        private string roomCode = string.Empty;
        private string relayRegion = string.Empty;
        private string relayRegionDescription = string.Empty;
        private string disconnectReason = string.Empty;
        private string status = string.Empty;

        public bool IsOnlineDuelActive =>
            role != SessionRole.None && networkManager != null &&
            (networkManager.IsClient || networkManager.IsServer);

        public bool IsHost => role == SessionRole.Host;
        public string Status => status;
        public string RoomCode => roomCode;
        public string RelayRegion => relayRegion;

        /// <summary>
        /// RTT measured by the active Unity Transport connection. This is the
        /// round trip through Relay, not the latency of an HTTP request made
        /// by the main menu.
        /// </summary>
        public int RelayRoundTripTimeMs
        {
            get
            {
                if (!IsOnlineDuelActive || transport == null)
                    return -1;

                ulong peerClientId;
                if (role == SessionRole.Host)
                {
                    if (remoteClientId == ulong.MaxValue)
                        return -1;
                    peerClientId = remoteClientId;
                }
                else if (role == SessionRole.Client)
                {
                    peerClientId = NetworkManager.ServerClientId;
                }
                else
                {
                    return -1;
                }

                ulong roundTrip = transport.GetCurrentRtt(peerClientId);
                return roundTrip == 0 || roundTrip > int.MaxValue
                    ? -1
                    : (int)roundTrip;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateOnStartup()
        {
            EnsureInstance();
        }

        public static DuelOnlineSession EnsureInstance()
        {
            if (Instance != null)
                return Instance;
            var root = new GameObject("Arcane Duel Online Session");
            Instance = root.AddComponent<DuelOnlineSession>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            EnsureNetworkManager();
            DuelOnlineBridge.SubmitReplicaChoice = SubmitRemoteChoice;
            DuelOnlineBridge.SubmitReplicaResponse = SubmitRemoteResponse;
        }

        private void OnDestroy()
        {
            if (helloRetry != null)
                StopCoroutine(helloRetry);
            UnregisterHandlers();
            if (hostController != null)
                hostController.CoreEventPresented -= OnHostCoreEvent;
            if (DuelOnlineBridge.SubmitReplicaChoice == SubmitRemoteChoice)
                DuelOnlineBridge.SubmitReplicaChoice = null;
            if (DuelOnlineBridge.SubmitReplicaResponse == SubmitRemoteResponse)
                DuelOnlineBridge.SubmitReplicaResponse = null;
            if (networkManager != null)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnected;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            if (Instance == this)
                Instance = null;
        }

        public void ShowPanel(bool join = false)
        {
            showPanel = true;
            focusJoinCode = join;
            requestJoinFocus = join;
            status = IsOnlineDuelActive
                ? status
                : "Escolha um deck válido e conecte-se por Relay.";
        }

        public void AttachOnlineArena(CardArenaBootstrap arena)
        {
            if (!IsOnlineDuelActive || arena == null)
                return;

            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            if (controller == null)
                return;

            if (IsHost)
            {
                if (hostController != null && hostController != controller)
                    hostController.CoreEventPresented -= OnHostCoreEvent;
                hostController = controller;
                hostController.ConfigureRemotePlayerOneAuthority(true);
                hostController.CoreEventPresented -= OnHostCoreEvent;
                hostController.CoreEventPresented += OnHostCoreEvent;
                DuelTestPerspectiveController.Instance?.ConfigureClientSwitching(
                    false,
                    DuelPlayerSide.PlayerOne);
                TryStartHostDuel();
                return;
            }

            replicaController = controller;
            // DuelNetworkProtocol rotates the snapshot before sending it, so
            // the local player's state is always slot P0 in this arena.
            replicaController.ConfigureNetworkReplica(0);
            DuelTestPerspectiveController.Instance?.ConfigureClientSwitching(
                false,
                DuelPlayerSide.PlayerOne);
            if (pendingReplicaState != null)
                ApplyReplicaState(pendingReplicaState);
            status = "Conectado. Aguardando o host confirmar os decks.";
        }

        public void SubmitRemoteChoice(DuelChoice choice)
        {
            if (choice == null)
                return;
            SubmitRemoteResponse(choice.Response, choice.RequestId);
        }

        public void SubmitRemoteResponse(byte[] response, ulong requestId)
        {
            if (role != SessionRole.Client || networkManager == null ||
                !networkManager.IsConnectedClient || response == null ||
                response.Length == 0 || response.Length > 2048 ||
                replicaController?.PresentationDecisionLocked == true)
            {
                return;
            }
            // The replica keeps displaying the last confirmed prompt until
            // the host processes this response. Lock it here so a double tap
            // cannot submit two different answers for the same request.
            replicaController?.SetPresentationDecisionLocked(true);
            status = "Resposta enviada. Aguardando confirmação do anfitrião...";
            SendToServer(ResponseMessage, new ResponsePayload
            {
                requestId = requestId,
                responseBase64 = Convert.ToBase64String(response)
            });
        }

        private void EnsureNetworkManager()
        {
            if (networkManager != null)
                return;

            transport = GetComponent<UnityTransport>() ??
                        gameObject.AddComponent<UnityTransport>();
            networkManager = GetComponent<NetworkManager>() ??
                             NetworkManager.Singleton ??
                             gameObject.AddComponent<NetworkManager>();
            if (networkManager == null)
            {
                throw new InvalidOperationException(
                    "Não foi possível criar o NetworkManager da sessão online.");
            }

            networkManager.NetworkConfig ??= new NetworkConfig();
            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.ProtocolVersion =
                NgoProtocolVersion;
            // Relay already limits this allocation to a single guest. An
            // extra NGO approval round trip only makes mobile connections
            // slower and can expire before the deck handshake begins.
            networkManager.NetworkConfig.ConnectionApproval = false;
            networkManager.NetworkConfig.TickRate = NetworkTickRate;
            networkManager.NetworkConfig.ClientConnectionBufferTimeout = 10;
            // Each peer deliberately opens the arena locally after the Relay
            // handshake. This card game has no spawned scene objects, so NGO
            // scene replication would only add races.
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void RegisterHandlers()
        {
            if (handlersRegistered)
                return;
            if (networkManager?.CustomMessagingManager == null)
                throw new InvalidOperationException(
                    "O canal de mensagens online ainda não foi inicializado.");
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                HelloMessage,
                OnHelloMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                HelloAcceptedMessage,
                OnHelloAcceptedMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                HelloRejectedMessage,
                OnHelloRejectedMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                StartMessage,
                OnStartMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                StateMessage,
                OnStateMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                ResponseMessage,
                OnResponseMessage);
            handlersRegistered = true;
        }

        private void UnregisterHandlers()
        {
            if (!handlersRegistered ||
                networkManager?.CustomMessagingManager == null)
            {
                handlersRegistered = false;
                return;
            }

            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                HelloMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                HelloAcceptedMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                HelloRejectedMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                StartMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                StateMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                ResponseMessage);
            handlersRegistered = false;
        }

        private async void BeginHosting()
        {
            if (connectionOperationInProgress)
            {
                status = "Uma conexão já está sendo preparada. Aguarde.";
                return;
            }
            if (!TryGetLocalLoadout(out DuelDeckLoadout loadout, out string error))
            {
                status = error;
                return;
            }
            if (IsOnlineDuelActive)
            {
                status = "Já existe uma sessão online em andamento.";
                return;
            }
            if (networkManager != null && networkManager.ShutdownInProgress)
            {
                status = "A sessão anterior ainda está sendo encerrada. Aguarde.";
                return;
            }

            connectionOperationInProgress = true;
            try
            {
                status = "Autenticando na Unity e criando a sala...";
                localLoadout = loadout;
                await InitializeServices();
                Allocation allocation = await RelayService.Instance
                    .CreateAllocationAsync(1);
                relayRegion = allocation.Region ?? string.Empty;
                _ = ResolveRelayRegionDescription(relayRegion);
                transport.SetRelayServerData(
                    AllocationUtils.ToRelayServerData(allocation, "dtls"));
                roomCode = await RelayService.Instance
                    .GetJoinCodeAsync(allocation.AllocationId);
                role = SessionRole.Host;
                if (!networkManager.StartHost())
                    throw new InvalidOperationException(
                        "O NetworkManager não iniciou como host.");
                RegisterHandlers();

                status = $"Sala criada na região Relay {GetRelayRegionLabel()}. " +
                    "Compartilhe o código e aguarde o rival.";
                showPanel = true;
            }
            catch (Exception exception)
            {
                ResetAfterFailedConnection(
                    $"Não foi possível criar a sala: {exception.GetBaseException().Message}");
            }
            finally
            {
                connectionOperationInProgress = false;
            }
        }

        private async void BeginJoining()
        {
            if (connectionOperationInProgress)
            {
                status = "Uma conexão já está sendo preparada. Aguarde.";
                return;
            }
            if (!TryGetLocalLoadout(out DuelDeckLoadout loadout, out string error))
            {
                status = error;
                return;
            }
            string normalizedCode = (joinCode ?? string.Empty).Trim().ToUpperInvariant();
            if (normalizedCode.Length < 6 || normalizedCode.Length > 12)
            {
                status = "Informe o código da sala com 6 a 12 caracteres.";
                return;
            }
            if (IsOnlineDuelActive)
            {
                status = "Já existe uma sessão online em andamento.";
                return;
            }
            if (networkManager != null && networkManager.ShutdownInProgress)
            {
                status = "A sessão anterior ainda está sendo encerrada. Aguarde.";
                return;
            }

            connectionOperationInProgress = true;
            try
            {
                status = "Autenticando e entrando na sala...";
                localLoadout = loadout;
                await InitializeServices();
                JoinAllocation allocation = await RelayService.Instance
                    .JoinAllocationAsync(normalizedCode);
                relayRegion = allocation.Region ?? string.Empty;
                _ = ResolveRelayRegionDescription(relayRegion);
                transport.SetRelayServerData(
                    AllocationUtils.ToRelayServerData(allocation, "dtls"));
                roomCode = normalizedCode;
                role = SessionRole.Client;
                helloAccepted = false;
                if (!networkManager.StartClient())
                    throw new InvalidOperationException(
                        "O NetworkManager não iniciou como cliente.");
                RegisterHandlers();
                status = "Conectando ao host...";
            }
            catch (Exception exception)
            {
                ResetAfterFailedConnection(
                    $"Não foi possível entrar na sala: {exception.GetBaseException().Message}");
            }
            finally
            {
                connectionOperationInProgress = false;
            }
        }

        private static async Task InitializeServices()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            bool hasVacancy = networkManager != null &&
                              networkManager.ConnectedClientsIds.Count < 2;
            response.Approved = hasVacancy;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (!hasVacancy)
                response.Reason = "Esta sala privada já possui dois duelistas.";
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager == null)
                return;
            if (networkManager.IsServer)
            {
                if (clientId == NetworkManager.ServerClientId)
                    return;
                remoteClientId = clientId;
                status = "Rival conectado. Validando o deck recebido...";
                return;
            }

            if (role == SessionRole.Client &&
                clientId == networkManager.LocalClientId)
            {
                status = "Conectado. Enviando o deck para validação do host...";
                StartClientDeckHandshake();
            }
        }

        private void StartClientDeckHandshake()
        {
            if (role != SessionRole.Client || networkManager == null ||
                !networkManager.IsConnectedClient || localLoadout == null ||
                helloAccepted)
            {
                return;
            }

            if (helloRetry != null)
                StopCoroutine(helloRetry);
            helloRetry = StartCoroutine(SendHelloUntilAccepted());
        }

        private IEnumerator SendHelloUntilAccepted()
        {
            // The Netcode client-connected callback can happen in the same
            // frame as the messaging channel becomes usable. Retrying the
            // idempotent deck handshake closes that timing gap and also makes
            // a transient Relay packet loss harmless.
            yield return null;

            int attempts = 0;
            while (attempts < MaximumHandshakeAttempts &&
                   !helloAccepted && role == SessionRole.Client &&
                   networkManager != null && networkManager.IsConnectedClient &&
                   localLoadout != null)
            {
                attempts++;
                SendToServer(HelloMessage, new HelloPayload
                {
                    protocolVersion = ProtocolVersion,
                    compatibility = ProjectIdentity.MultiplayerCompatibility,
                    coreApiVersion = ProjectIdentity.CoreApiVersion,
                    coreCommit = ProjectIdentity.CoreCommit,
                    loadout = localLoadout
                });

                status = attempts == 1
                    ? "Lobby conectado. Enviando o deck ao anfitriao..."
                    : $"Aguardando confirmacao do anfitriao. Reenviando deck ({attempts})...";
                yield return new WaitForSecondsRealtime(HandshakeRetrySeconds);
            }

            if (!helloAccepted && role == SessionRole.Client &&
                networkManager != null && networkManager.IsConnectedClient)
            {
                status = "Deck enviado. Aguarde o anfitriao iniciar ou confirme que ambos usam o protocolo online v2.";
            }
            helloRetry = null;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (networkManager == null ||
                clientId != networkManager.LocalClientId &&
                clientId != remoteClientId)
            {
                return;
            }

            if (clientId == remoteClientId && IsHost)
            {
                remoteClientId = ulong.MaxValue;
                remoteLoadout = null;
                status = "O rival desconectou. O duelo online foi interrompido.";
                hostController?.SetPresentationDecisionLocked(true);
                return;
            }

            if (clientId == networkManager.LocalClientId && !networkManager.IsServer)
            {
                helloAccepted = false;
                UnregisterHandlers();
                status = string.IsNullOrWhiteSpace(disconnectReason)
                    ? "A conexão com o host foi encerrada."
                    : disconnectReason;
                replicaController?.SetPresentationDecisionLocked(true);
            }
        }

        private void OnHelloMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!IsHost || senderClientId != remoteClientId ||
                !TryRead(reader, out HelloPayload hello))
            {
                return;
            }

            if (!ValidateHello(hello, out string rejection))
            {
                status = rejection;
                SendToClient(senderClientId, HelloRejectedMessage,
                    new HelloRejectedPayload { reason = rejection });
                StartCoroutine(DisconnectRejectedClient(senderClientId));
                return;
            }
            remoteLoadout = hello.loadout;
            status = "Rival conectado e deck validado. O anfitriao pode iniciar a partida.";
            SendToClient(senderClientId, HelloAcceptedMessage,
                new HelloAcceptedPayload
                {
                    protocolVersion = ProtocolVersion,
                    compatibility = ProjectIdentity.MultiplayerCompatibility
                });
        }

        private void OnHelloAcceptedMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                !TryRead(reader, out HelloAcceptedPayload accepted) ||
                accepted.protocolVersion != ProtocolVersion)
            {
                return;
            }

            helloAccepted = true;
            if (helloRetry != null)
            {
                StopCoroutine(helloRetry);
                helloRetry = null;
            }
            status = "Deck validado. Aguardando o anfitriao iniciar a partida.";
        }

        private void OnHelloRejectedMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                !TryRead(reader, out HelloRejectedPayload rejection))
            {
                return;
            }

            disconnectReason = string.IsNullOrWhiteSpace(rejection.reason)
                ? "O anfitriao recusou o deck enviado."
                : rejection.reason;
            status = disconnectReason;
            if (helloRetry != null)
            {
                StopCoroutine(helloRetry);
                helloRetry = null;
            }
        }

        private IEnumerator DisconnectRejectedClient(ulong clientId)
        {
            // Give the reliable rejection packet enough time to leave the
            // host before releasing the reserved Relay slot.
            yield return new WaitForSecondsRealtime(1f);
            if (networkManager != null && networkManager.IsServer)
                networkManager.DisconnectClient(clientId);
        }

        private void BeginHostMatch()
        {
            if (!IsHost || matchStarted || remoteLoadout == null)
                return;

            status = "Abrindo a arena para os dois duelistas...";
            showPanel = false;
            OpenDuelArena();
        }

        private void OnStartMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                !TryRead(reader, out StartPayload start) ||
                start.protocolVersion != ProtocolVersion ||
                matchStarted)
            {
                return;
            }

            matchStarted = true;
            helloAccepted = true;
            if (helloRetry != null)
            {
                StopCoroutine(helloRetry);
                helloRetry = null;
            }
            status = "Decks validados. Abrindo a arena...";
            showPanel = false;
            OpenDuelArena();
        }

        private void OnStateMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                !TryRead(reader, out DuelNetworkState networkState) ||
                networkState.sequence <= lastReplicaSequence)
            {
                return;
            }
            lastReplicaSequence = networkState.sequence;
            pendingReplicaState = networkState;
            ApplyReplicaState(networkState);
        }

        private void OnResponseMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!IsHost || senderClientId != remoteClientId ||
                !TryRead(reader, out ResponsePayload response) ||
                !TryValidateRemoteResponse(response, out byte[] bytes))
            {
                return;
            }
            hostController.SubmitCoreResponse(bytes, response.requestId);
        }

        private void TryStartHostDuel()
        {
            if (!IsHost || matchStarted || hostController == null ||
                localLoadout == null || remoteLoadout == null)
            {
                return;
            }

            uint[] localMain = ParseCardCodes(localLoadout.mainDeckCardIds);
            uint[] localExtra = ParseCardCodes(localLoadout.extraDeckCardIds);
            uint[] remoteMain = ParseCardCodes(remoteLoadout.mainDeckCardIds);
            uint[] remoteExtra = ParseCardCodes(remoteLoadout.extraDeckCardIds);
            if (localMain.Length < 40 || remoteMain.Length < 40)
            {
                status = "Um dos decks não possui 40 cartas válidas no catálogo local.";
                return;
            }

            try
            {
                hostController.ConfigureRemotePlayerOneAuthority(true);
                hostController.RestartExternalDuel(
                    localMain,
                    localExtra,
                    remoteMain,
                    remoteExtra);
                matchStarted = true;
                status = "Duelo online ativo. O host valida todas as jogadas.";
                SendToClient(remoteClientId, StartMessage, new StartPayload
                {
                    protocolVersion = ProtocolVersion,
                    compatibility =
                        ProjectIdentity.MultiplayerCompatibility
                });
                BroadcastState();
            }
            catch (Exception exception)
            {
                status = $"Falha ao iniciar o duelo online: {exception.GetBaseException().Message}";
                Debug.LogException(exception);
            }
        }

        private void OnHostCoreEvent(DuelEvent duelEvent)
        {
            if (!matchStarted || pendingStateBroadcast != null)
                return;
            pendingStateBroadcast = StartCoroutine(
                BroadcastLatestStateAtEndOfFrame());
        }

        private IEnumerator BroadcastLatestStateAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            pendingStateBroadcast = null;
            BroadcastState();
        }

        private void BroadcastState()
        {
            if (!IsHost || hostController == null ||
                remoteClientId == ulong.MaxValue || !matchStarted)
            {
                return;
            }
            DuelNetworkState state = DuelNetworkProtocol.CreateState(
                hostController.PresentationState,
                hostController.CurrentPrompt,
                1,
                ++nextStateSequence,
                status);
            SendToClient(remoteClientId, StateMessage, state);
        }

        private void ApplyReplicaState(DuelNetworkState state)
        {
            if (replicaController != null)
                replicaController.ApplyNetworkState(state);
        }

        private bool TryValidateRemoteResponse(
            ResponsePayload response,
            out byte[] bytes)
        {
            bytes = null;
            DuelPrompt prompt = hostController?.CurrentPrompt;
            if (prompt == null || prompt.Player != 1 ||
                prompt.RequestId == 0 || prompt.RequestId != response.requestId ||
                string.IsNullOrWhiteSpace(response.responseBase64))
            {
                return false;
            }
            try
            {
                bytes = Convert.FromBase64String(response.responseBase64);
                if (bytes.Length == 0 || bytes.Length > 2048)
                    return false;
            }
            catch (FormatException)
            {
                return false;
            }
            // The current prompt, the player side and the request id are
            // checked here; ocgcore remains the final validator for every
            // protocol byte, including multi-card selections.
            return true;
        }

        private static bool ValidateHello(HelloPayload hello, out string rejection)
        {
            rejection = string.Empty;
            if (hello == null || hello.loadout == null ||
                hello.protocolVersion != ProtocolVersion)
            {
                rejection = "O rival usa um protocolo online incompatível. Ambos precisam usar o protocolo v2.";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(hello.coreApiVersion) &&
                hello.coreApiVersion != ProjectIdentity.CoreApiVersion)
            {
                rejection = "O rival usa uma API de regras incompatível com esta partida.";
                return false;
            }
            int mainCount = hello.loadout.mainDeckCardIds?.Count ?? 0;
            int extraCount = hello.loadout.extraDeckCardIds?.Count ?? 0;
            if (mainCount < 40 || mainCount > 60 || extraCount > 15)
            {
                rejection = "O deck remoto não respeita os limites de 40–60 e 15 cartas.";
                return false;
            }
            return true;
        }

        private static uint[] ParseCardCodes(System.Collections.Generic.IEnumerable<string> values)
        {
            if (values == null)
                return Array.Empty<uint>();
            var cards = new System.Collections.Generic.List<uint>();
            foreach (string value in values)
            {
                if (uint.TryParse(value, out uint code) && code != 0)
                    cards.Add(code);
            }
            return cards.ToArray();
        }

        private bool TryGetLocalLoadout(
            out DuelDeckLoadout loadout,
            out string error)
        {
            loadout = null;
            error = string.Empty;
            GameFrontendBootstrap frontend = GameFrontendBootstrap.Instance;
            if (frontend == null || !frontend.TryGetSelectedDuelLoadout(
                    out loadout,
                    out error))
            {
                error = string.IsNullOrWhiteSpace(error)
                    ? "Escolha e valide um deck antes de abrir uma sala."
                    : error;
                return false;
            }
            return true;
        }

        private void SendToServer<T>(string messageName, T payload)
        {
            Send(NetworkManager.ServerClientId, messageName, payload);
        }

        private void SendToClient<T>(ulong clientId, string messageName, T payload)
        {
            Send(clientId, messageName, payload);
        }

        private void Send<T>(ulong target, string messageName, T payload)
        {
            if (networkManager == null ||
                networkManager.CustomMessagingManager == null)
            {
                return;
            }
            string json = JsonUtility.ToJson(payload);
            int bytes = Encoding.UTF8.GetByteCount(json);
            if (bytes <= 0 || bytes > MaxWireBytes)
            {
                Debug.LogError($"[Arcane Duel Online] Mensagem '{messageName}' fora do limite: {bytes} bytes.");
                return;
            }
            using var writer = new FastBufferWriter(
                Math.Min(1024, bytes + 16),
                Allocator.Temp,
                MaxWireBytes);
            writer.WriteValueSafe(json);
            networkManager.CustomMessagingManager.SendNamedMessage(
                messageName,
                target,
                writer,
                // Deck lists and perspective snapshots commonly exceed the
                // single-packet Relay payload. NGO reassembles these chunks
                // before invoking the named-message handler.
                NetworkDelivery.ReliableFragmentedSequenced);
        }

        private static bool TryRead<T>(FastBufferReader reader, out T result)
        {
            result = default;
            try
            {
                reader.ReadValueSafe(out string json);
                if (string.IsNullOrWhiteSpace(json) ||
                    Encoding.UTF8.GetByteCount(json) > MaxWireBytes)
                {
                    return false;
                }
                result = JsonUtility.FromJson<T>(json);
                return !ReferenceEquals(result, null);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OpenDuelArena()
        {
            if (SceneManager.GetActiveScene().name != DuelArenaScene)
                SceneManager.LoadScene(DuelArenaScene);
        }

        private void ResetAfterFailedConnection(string failure)
        {
            status = failure;
            roomCode = string.Empty;
            relayRegion = string.Empty;
            relayRegionDescription = string.Empty;
            disconnectReason = string.Empty;
            role = SessionRole.None;
            localLoadout = null;
            remoteLoadout = null;
            remoteClientId = ulong.MaxValue;
            matchStarted = false;
            pendingStateBroadcast = null;
            helloAccepted = false;
            UnregisterHandlers();
            if (helloRetry != null)
            {
                StopCoroutine(helloRetry);
                helloRetry = null;
            }
            if (networkManager != null &&
                (networkManager.IsClient || networkManager.IsServer))
            {
                networkManager.Shutdown();
            }
        }

        private void OnGUI()
        {
            if (!showPanel)
                return;
            const float width = 640f;
            const float height = 500f;
            Color originalColor = GUI.color;
            GUI.color = new Color(0.008f, 0.022f, 0.045f, 0.985f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = originalColor;
            Matrix4x4 previousMatrix = GUI.matrix;
            float scale = CalculateLobbyScale(
                Screen.width,
                Screen.height);
            GUI.matrix = Matrix4x4.Scale(
                new Vector3(scale, scale, 1f));
            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            var area = new Rect(
                (logicalWidth - width) * 0.5f,
                (logicalHeight - height) * 0.5f,
                width,
                height);
            Color originalBackground = GUI.backgroundColor;
            GUI.color = new Color(0.16f, 0.91f, 1f, 0.95f);
            GUI.DrawTexture(new Rect(area.x - 3f, area.y - 3f,
                area.width + 6f, area.height + 6f), Texture2D.whiteTexture);
            GUI.color = originalColor;
            GUI.backgroundColor = new Color(0.035f, 0.13f, 0.17f, 1f);
            GUI.ModalWindow(912701, area, DrawPanel, string.Empty);
            GUI.backgroundColor = originalBackground;
            GUI.matrix = previousMatrix;
        }

        private static float CalculateLobbyScale(
            float screenWidth,
            float screenHeight)
        {
            return Mathf.Clamp(
                Mathf.Min(screenWidth / 900f, screenHeight / 620f),
                0.70f,
                1.60f);
        }

        private void DrawPanel(int windowId)
        {
            EnsureLobbyStyles();
            const float margin = 38f;
            const float contentWidth = 564f;

            GUI.Label(new Rect(margin, 22f, contentWidth, 38f),
                "MULTIPLAYER ONLINE", lobbyHeadingStyle);
            GUI.Label(new Rect(margin, 64f, contentWidth, 28f),
                GetRelayLobbyInfo(),
                lobbySubheadingStyle);
            GUI.Label(new Rect(margin, 96f, contentWidth, 38f),
                status ?? string.Empty, lobbyStatusStyle);

            string code = IsOnlineDuelActive ? roomCode : string.Empty;
            GUI.Label(new Rect(margin, 140f, contentWidth, 34f),
                $"CODIGO DA SALA  •  {(string.IsNullOrWhiteSpace(code) ? "—" : code)}",
                lobbyCodeStyle);

            if (!string.IsNullOrWhiteSpace(code))
            {
                GUI.backgroundColor = new Color(0.22f, 0.52f, 1f, 1f);
                if (GUI.Button(new Rect(458f, 178f, 144f, 38f), "COPIAR", lobbyButtonStyle))
                    GUIUtility.systemCopyBuffer = code;
            }

            GUI.Label(new Rect(margin, 180f, 260f, 22f),
                "CODIGO PARA ENTRAR", lobbySmallLabelStyle);
            if (requestJoinFocus)
            {
                GUI.SetNextControlName("ArcaneJoinCode");
                requestJoinFocus = false;
            }
            bool canStartConnection = !IsOnlineDuelActive &&
                !connectionOperationInProgress &&
                (networkManager == null || !networkManager.ShutdownInProgress);
            GUI.enabled = canStartConnection;
            joinCode = GUI.TextField(
                new Rect(margin, 204f, 412f, 42f), joinCode ?? string.Empty,
                lobbyInputStyle).Trim().ToUpperInvariant();
            if (focusJoinCode)
                GUI.FocusControl("ArcaneJoinCode");

            GUI.backgroundColor = new Color(0.22f, 0.82f, 0.90f, 1f);
            if (GUI.Button(new Rect(464f, 204f, 138f, 42f), "CRIAR SALA", lobbyButtonStyle))
                BeginHosting();
            GUI.enabled = canStartConnection;
            GUI.backgroundColor = new Color(0.68f, 1f, 0.16f, 1f);
            if (GUI.Button(new Rect(464f, 254f, 138f, 42f), "ENTRAR", lobbyButtonStyle))
                BeginJoining();
            GUI.enabled = true;

            GUI.backgroundColor = new Color(0.035f, 0.12f, 0.21f, 1f);
            GUI.Box(new Rect(margin, 264f, 412f, 92f), GUIContent.none);
            int players = IsHost ? (remoteLoadout == null ? 1 : 2) :
                role == SessionRole.Client ? 2 : 0;
            string roleLabel = IsHost ? "ANFITRIAO / JOGADOR 1" :
                role == SessionRole.Client ? "JOGADOR 2" : "DESCONECTADO";
            GUI.Label(new Rect(54f, 274f, 380f, 22f),
                $"JOGADORES  •  {players} / 2    {roleLabel}", lobbySmallLabelStyle);
            string localDeck = localLoadout?.displayName ?? "AGUARDANDO";
            string remoteDeck = remoteLoadout?.displayName ?? "AGUARDANDO";
            GUI.Label(new Rect(54f, 300f, 380f, 42f),
                $"DECK JOGADOR 1  •  {(IsHost ? localDeck : remoteDeck)}\n" +
                $"DECK JOGADOR 2  •  {(IsHost ? remoteDeck : localDeck)}",
                lobbyDeckStyle);

            GUI.enabled = IsHost && remoteLoadout != null && !matchStarted;
            GUI.backgroundColor = GUI.enabled
                ? new Color(0.78f, 0.56f, 0.08f, 1f)
                : new Color(0.22f, 0.20f, 0.14f, 1f);
            if (GUI.Button(new Rect(margin, 366f, contentWidth, 42f),
                    IsHost && remoteLoadout != null
                        ? "INICIAR DUELO ONLINE"
                        : "AGUARDANDO JOGADORES E DECKS", lobbyButtonStyle))
            {
                BeginHostMatch();
            }
            GUI.enabled = true;

            if (IsOnlineDuelActive && !matchStarted)
            {
                GUI.backgroundColor = new Color(0.95f, 0.25f, 0.35f, 1f);
                if (GUI.Button(new Rect(margin, 420f, 220f, 34f),
                        "SAIR DA SALA", lobbyButtonStyle))
                {
                    ResetAfterFailedConnection("Sala cancelada.");
                    showPanel = false;
                }
            }
            GUI.backgroundColor = new Color(0.22f, 0.52f, 1f, 1f);
            if (GUI.Button(new Rect(398f, 420f, 204f, 34f),
                    "FECHAR PAINEL", lobbyButtonStyle))
            {
                if (IsOnlineDuelActive && !matchStarted)
                    ResetAfterFailedConnection("Sala cancelada.");
                showPanel = false;
            }
            GUI.backgroundColor = Color.white;
        }

        private string GetRelayLobbyInfo()
        {
            if (!IsOnlineDuelActive)
            {
                return "O Relay escolhe automaticamente a melhor região ao criar a sala.";
            }

            int roundTrip = RelayRoundTripTimeMs;
            string rtt = roundTrip < 0
                ? "medindo RTT..."
                : $"RTT real: {roundTrip} ms";
            return $"Relay: {GetRelayRegionLabel()}  •  {rtt}";
        }

        private string GetRelayRegionLabel()
        {
            if (!string.IsNullOrWhiteSpace(relayRegionDescription))
                return relayRegionDescription;

            return string.IsNullOrWhiteSpace(relayRegion)
                ? "selecionando..."
                : relayRegion.ToUpperInvariant();
        }

        private async Task ResolveRelayRegionDescription(string allocationRegion)
        {
            if (string.IsNullOrWhiteSpace(allocationRegion))
                return;
            if (string.Equals(
                    relayRegion,
                    allocationRegion,
                    StringComparison.OrdinalIgnoreCase))
            {
                relayRegionDescription = string.Empty;
            }

            try
            {
                foreach (Region region in await RelayService.Instance.ListRegionsAsync())
                {
                    if (string.Equals(
                            region.Id,
                            allocationRegion,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(
                                relayRegion,
                                allocationRegion,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            relayRegionDescription =
                                region.Description ?? string.Empty;
                        }
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // The allocation itself is valid. Keep the region identifier
                // visible if Relay's optional display-name lookup fails.
            }
        }

        private GUIStyle lobbyHeadingStyle;
        private GUIStyle lobbySubheadingStyle;
        private GUIStyle lobbyStatusStyle;
        private GUIStyle lobbyCodeStyle;
        private GUIStyle lobbySmallLabelStyle;
        private GUIStyle lobbyDeckStyle;
        private GUIStyle lobbyInputStyle;
        private GUIStyle lobbyButtonStyle;

        private void EnsureLobbyStyles()
        {
            if (lobbyHeadingStyle != null)
                return;

            lobbyHeadingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            lobbySubheadingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                normal = { textColor = new Color(0.66f, 0.75f, 0.82f, 1f) }
            };
            lobbyStatusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.72f, 1f, 0.1f, 1f) }
            };
            lobbyCodeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.2f, 0.88f, 1f, 1f) }
            };
            lobbySmallLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.62f, 0.7f, 0.77f, 1f) }
            };
            lobbyDeckStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            lobbyInputStyle = new GUIStyle(GUI.skin.textField)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            lobbyButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.black }
            };
        }
    }
}
