using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using ArcaneArena;
using ArcaneArena.Frontend;
using ArcaneArena.Multiplayer.Tournaments;
using ArcaneArena.Presentation;
using ArcaneDuel.DuelEngine.Diagnostics;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.Game;
using ArcaneDuel.Game.Competitive;
using ArcaneDuel.Game.Tournaments;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
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
        private const string ProtocolVersion = "arcane-duel-online-v13";
        private const string HelloMessage = "arcane.duel.hello.v4";
        private const string HelloRequestMessage = "arcane.duel.hello-request.v4";
        private const string HelloAcceptedMessage = "arcane.duel.hello-accepted.v4";
        private const string HelloRejectedMessage = "arcane.duel.hello-rejected.v4";
        private const string StartMessage = "arcane.duel.start.v4";
        private const string ClientReadyMessage = "arcane.duel.client-ready.v4";
        private const string StateMessage = "arcane.duel.state.v4";
        private const string ResponseMessage = "arcane.duel.response.v4";
        private const string ResponseFastMessage =
            "arcane.duel.response-fast.v1";
        private const string StateAckMessage = "arcane.duel.state-ack.v4";
        private const string ResyncRequestMessage = "arcane.duel.resync.v4";
        private const string BeginDuelMessage = "arcane.duel.begin.v8";
        private const string PreludeMessage = "arcane.duel.prelude.v1";
        private const string PreludeChoiceMessage =
            "arcane.duel.prelude-choice.v1";
        private const string PreludeResultMessage =
            "arcane.duel.prelude-result.v1";
        private const string MatchRewardMessage = "arcane.duel.match-result.v8";
        private const string PresentationEventMessage =
            "arcane.duel.presentation-event.v4";
        private const string WirePacketMessage = "arcane.duel.wire-packet.v4";
        private const int MaxWireBytes = DuelWireProtocol.MaximumPayloadBytes;
        private const int MaximumFastResponseBytes = 900;
        private const ushort NgoProtocolVersion = 13;
        private const uint NetworkTickRate = 20;
        private const int TransportHeartbeatMilliseconds = 1000;
        private const int TransportDisconnectTimeoutMilliseconds = 120000;
        private const int MaximumHandshakeAttempts = 40;
        private const int MaximumStartAttempts = 80;
        private const float HandshakeRetrySeconds = 0.75f;
        private const float StartRetrySeconds = 0.75f;
        private const float ArenaAttachTimeoutSeconds = 12f;
        private const float ArenaReadyRetrySeconds = 1f;
        private const float StateHeartbeatSeconds = 2f;
        private const float ResponseRetrySeconds = 0.75f;
        private const float ResponseResyncSeconds = 3f;
        private const float WireRetrySeconds = 0.35f;
        private const float WirePumpSeconds = 0.02f;
        private const float WireAssemblyTimeoutSeconds = 20f;
        private const float WireReceiptLifetimeSeconds = 120f;
        private const int WirePacketsPerTransferPump = 8;
        private const int WirePacketsPerFrame = 24;
        private const int MaximumConcurrentWireTransfers = 128;
        private const int CompressionThresholdBytes = 512;
        private const int CommandSchemaVersion = 1;
        private const float CommandRatePerSecond = 10f;
        private const float CommandBurstCapacity = 20f;
        private const float ResyncCooldownSeconds = 3f;
        private const string ReconnectSessionKey =
            "Arcane.Multiplayer.SessionId";
        private const string ReconnectRoomKey =
            "Arcane.Multiplayer.RoomCode";
        private const string ReconnectMatchKey =
            "Arcane.Multiplayer.MatchId";
        private const string ReconnectProtocolKey =
            "Arcane.Multiplayer.Protocol";
        private const string ReconnectStateVersionKey =
            "Arcane.Multiplayer.StateVersion";
        private const string ReconnectTimestampKey =
            "Arcane.Multiplayer.Timestamp";
        private const string ReconnectRewardEligibilityKey =
            "Arcane.Multiplayer.RewardEligibility";

        private enum SessionRole
        {
            None,
            Host,
            Client
        }

        private enum LogicalMessage : byte
        {
            Unknown = 0,
            Hello = 1,
            HelloRequest = 2,
            HelloAccepted = 3,
            HelloRejected = 4,
            Start = 5,
            ClientReady = 6,
            State = 7,
            Response = 8,
            PresentationEvent = 9,
            StateAck = 10,
            ResyncRequest = 11,
            MatchReward = 12,
            BeginDuel = 13,
            Prelude = 14,
            PreludeChoice = 15,
            PreludeResult = 16
        }

        [Serializable]
        private sealed class HelloPayload
        {
            public string protocolVersion;
            public string compatibility;
            public string coreApiVersion;
            public string coreCommit;
            public DuelDeckLoadout loadout;
            public CompetitivePolicy competitivePolicy;
            public RankPlayerSnapshot rankPlayer;
        }

        [Serializable]
        private sealed class ResponsePayload
        {
            public int schemaVersion;
            public string matchId;
            public string commandType;
            public ulong commandId;
            public uint clientSequence;
            public ulong expectedStateVersion;
            public ulong requestId;
            public string responseBase64;
        }

        [Serializable]
        private sealed class StateAckPayload
        {
            public string protocolVersion;
            public string matchId;
            public uint transitionEpoch;
            public ulong stateVersion;
            public ulong publicStateHash;
            public uint lastAcceptedClientSequence;
        }

        [Serializable]
        private sealed class ResyncRequestPayload
        {
            public string protocolVersion;
            public string matchId;
            public ulong lastStateVersion;
            public string reason;
        }

        [Serializable]
        private sealed class MatchRewardPayload
        {
            public string protocolVersion;
            public string matchId;
            public uint transitionEpoch;
            public ulong resultSequence;
            public int winnerSeat;
            public int loserSeat;
            public string endReason;
            public ulong finalStateVersion;
            public long finishedAtServerTick;
            public int damageDealt;
            public long statisticsDamageDealt;
            public long statisticsDamageReceived;
            public int completedRounds;
            public bool winner;
            public bool draw;
            public RankChangeReceipt rankReceipt;
        }

        [Serializable]
        private sealed class ApprovalPayload
        {
            public string p;
            public string v;
            public string c;
        }

        [Serializable]
        private sealed class StartPayload
        {
            public string protocolVersion;
            public string compatibility;
            public string matchId;
            public uint transitionEpoch;
            public bool duelAlreadyBegun;
            public RankedMatchSnapshot rankedMatch;
            public DuelIdentitySnapshot hostIdentity;
        }

        [Serializable]
        private sealed class BeginDuelPayload
        {
            public string protocolVersion;
            public string matchId;
            public uint transitionEpoch;
            public ulong initialStateVersion;
            public long serverStartTick;
        }

        [Serializable]
        private sealed class PreludePayload
        {
            public string protocolVersion;
            public string matchId;
            public uint transitionEpoch;
            public int round;
        }

        [Serializable]
        private sealed class PreludeChoicePayload
        {
            public string protocolVersion;
            public string matchId;
            public uint transitionEpoch;
            public int round;
            public int choice;
        }

        [Serializable]
        private sealed class PreludeResultPayload
        {
            public string protocolVersion;
            public string matchId;
            public uint transitionEpoch;
            public int round;
            public int hostChoice;
            public int clientChoice;
            public int winnerSeat;
            public bool tie;
        }

        [Serializable]
        private sealed class ProtocolPayload
        {
            public string protocolVersion;
        }

        [Serializable]
        private sealed class ClientReadyPayload
        {
            public string protocolVersion;
            public string compatibility;
            public string matchId;
            public uint transitionEpoch;
            public bool deckReady;
            public bool startReceived;
            public bool arenaReady;
            public bool beginApplied;
            public RankPlayerSnapshot rankPlayer;
        }

        [Serializable]
        private sealed class HelloAcceptedPayload
        {
            public string protocolVersion;
            public string compatibility;
            public string hostPlayerDisplayName;
            public string hostDeckDisplayName;
            public CompetitivePolicy competitivePolicy;
            public RankPlayerSnapshot rankPlayer;
            public DuelIdentitySnapshot hostIdentity;
        }

        [Serializable]
        private sealed class HelloRejectedPayload
        {
            public string reason;
        }

        private readonly struct WireTransferKey : IEquatable<WireTransferKey>
        {
            public readonly ulong PeerId;
            public readonly Guid TransferId;

            public WireTransferKey(ulong peerId, Guid transferId)
            {
                PeerId = peerId;
                TransferId = transferId;
            }

            public bool Equals(WireTransferKey other)
            {
                return PeerId == other.PeerId && TransferId == other.TransferId;
            }

            public override bool Equals(object value)
            {
                return value is WireTransferKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)PeerId * 397) ^ TransferId.GetHashCode();
                }
            }
        }

        private sealed class OutboundWireTransfer
        {
            public ulong Target;
            public LogicalMessage LogicalMessage;
            public NetworkDelivery Delivery;
            public DuelWireTransfer Transfer;
            public DuelWireAckTracker AckTracker;
            public int MissingCursor;
            public int SendRounds;
            public float NextSendTime;
        }

        private sealed class InboundWireTransfer
        {
            public readonly DuelWireReassembler Reassembler =
                new DuelWireReassembler();
            public float LastActivityTime;
        }

        private sealed class CompletedWireTransfer
        {
            public DuelWirePacket TransferAck;
            public float CompletedTime;
        }

        public static DuelOnlineSession Instance { get; private set; }

        private NetworkManager networkManager;
        private UnityTransport transport;
        private readonly MultiplayerSessionCoordinator sessionCoordinator =
            new MultiplayerSessionCoordinator();
        [SerializeField]
        private OnlineMatchFlowConfig flowConfig = new OnlineMatchFlowConfig();
        private readonly OnlineMatchReadinessBarrier readinessBarrier =
            new OnlineMatchReadinessBarrier();
        private OnlineLoadingScreenPresenter loadingPresenter;
        private OnlineDuelResultPresenter resultPresenter;
        private OnlineMatchFlowState flowState = OnlineMatchFlowState.Menu;
        private float flowStateEnteredAt;
        private uint transitionEpochCounter;
        private uint currentTransitionEpoch;
        private SessionRole role;
        private DuelDeckLoadout localLoadout;
        private DuelDeckLoadout remoteLoadout;
        private DuelIdentitySnapshot remoteDuelIdentity;
        private DuelArenaController hostController;
        private DuelArenaController replicaController;
        private DuelNetworkState pendingReplicaState;
        private ulong remoteClientId = ulong.MaxValue;
        private int nextStateSequence;
        private int lastReplicaSequence;
        private int nextPresentationEventSequence;
        private int lastPresentationEventSequence;
        private ulong pendingResponseRequestId;
        private byte[] pendingResponseBytes;
        private ulong pendingCommandId;
        private uint pendingClientSequence;
        private ulong pendingExpectedStateVersion;
        private ulong nextClientCommandId;
        private uint nextClientSequence;
        private float nextResponseRetryTime;
        private float pendingResponseStartedAt;
        private float nextPendingResponseResyncTime;
        private ulong lastAcknowledgedResponseRequestId;
        private ulong lastAcknowledgedCommandId;
        private uint lastAcceptedClientSequence;
        private ulong lastAcceptedCommandPayloadHash;
        private ulong authoritativeStateVersion;
        private ulong authoritativePublicStateHash;
        private ulong lastReplicaStateVersion;
        private ulong lastReplicaPublicStateHash;
        private ulong lastStateAckVersion;
        private bool clientSynchronizing;
        private float commandTokens = CommandBurstCapacity;
        private float lastCommandTokenTime;
        private float nextClientResyncTime;
        private float nextHostResyncTime;
        private bool reconnecting;
        private bool hostAwaitingReconnect;
        private bool hostAwaitingStateAckUnlock;
        private bool hostAwaitingLiveStateAck;
        private float reconnectDeadline;
        private Coroutine reconnectCoroutine;
        private Coroutine hostReconnectGraceCoroutine;
        private Coroutine persistedReconnectCoroutine;
        private Coroutine rankedBotFallbackCoroutine;
        private bool matchStarted;
        private bool hostCoreStarted;
        private Coroutine pendingStateBroadcast;
        private Coroutine arenaAttachRetry;
        private Coroutine helloRetry;
        private Coroutine helloRequestRetry;
        private Coroutine startRetry;
        private Coroutine stateHeartbeat;
        private Coroutine sceneTransitionRoutine;
        private Coroutine beginDuelRoutine;
        private Coroutine preludeResultRoutine;
        private bool helloAccepted;
        private bool clientDeckReady;
        private bool clientReceivedStart;
        private bool clientArenaReady;
        private bool localSceneReady;
        private bool localSceneLoadRequested;
        private bool beginDuelReceived;
        private bool beginDuelApplied;
        private bool clientBeginApplied;
        private int onlinePreludeRound;
        private DuelPreludeChoice hostPreludeChoice;
        private DuelPreludeChoice clientPreludeChoice;
        private byte onlineStartingPlayer;
        private bool onlinePreludeResolved;
        private bool diagnosticPreludeBypass;
        private float nextClientArenaReadyRetryTime;
        private string hostPlayerDisplayName = string.Empty;
        private string hostDeckDisplayName = string.Empty;
        private readonly Dictionary<WireTransferKey, OutboundWireTransfer>
            outboundWireTransfers =
                new Dictionary<WireTransferKey, OutboundWireTransfer>();
        private readonly Dictionary<WireTransferKey, InboundWireTransfer>
            inboundWireTransfers =
                new Dictionary<WireTransferKey, InboundWireTransfer>();
        private readonly Dictionary<WireTransferKey, CompletedWireTransfer>
            completedWireTransfers =
                new Dictionary<WireTransferKey, CompletedWireTransfer>();
        private readonly SortedDictionary<int, DuelNetworkPresentationEvent>
            pendingPresentationEvents =
                new SortedDictionary<int, DuelNetworkPresentationEvent>();
        private readonly Queue<DuelNetworkPresentationEvent>
            outgoingPresentationEvents =
                new Queue<DuelNetworkPresentationEvent>();
        private float nextWirePumpTime;
        private float nextWireCleanupTime;
        private bool connectionOperationInProgress;
        private bool handlersRegistered;
        private bool showPanel;
        private bool focusJoinCode;
        private bool requestJoinFocus;
        private string joinCode = string.Empty;
        private string roomCode = string.Empty;
        private string currentMatchId = string.Empty;
        private string relayRegion = string.Empty;
        private string relayRegionDescription = string.Empty;
        private string disconnectReason = string.Empty;
        private string status = string.Empty;
        private int hostRewardDamage;
        private int clientRewardDamage;
        private long hostStatisticsDamageDealt;
        private long hostStatisticsDamageReceived;
        private long clientStatisticsDamageDealt;
        private long clientStatisticsDamageReceived;
        private int completedRewardRounds;
        private int currentRewardTurnPlayer = -1;
        private bool rewardPlayerZeroTurnEnded;
        private bool rewardPlayerOneTurnEnded;
        private bool matchRewardFinalized;
        private bool resultLeaveInProgress;
        private ulong nextResultSequence;
        private ulong lastAppliedResultSequence;
        private DuelEvent pendingTerminalEvent;
        private MatchRewardPayload lastAuthoritativeResult;
        private CoinRewardEligibilitySnapshot
            localRewardEligibilityAtMatchStart;
        private string rewardResultMessage = string.Empty;
        private float rewardResultVisibleUntil;
        private TournamentDuelContext activeTournamentContext;
        private TournamentDuelMetricsCollector tournamentMetricsCollector;
        private Task tournamentResultReportTask;
        private bool tournamentLaunchRequested;
        private CompetitivePolicy competitivePolicy =
            CompetitivePolicy.Unranked;
        private bool automaticRankedMatchmaking;
        private bool rankedRoomCreationPanel;
        private bool rankedBotFallbackInProgress;
        private float rankedBotFallbackDeadline;
        private RankPlayerSnapshot localRankHandshake;
        private RankPlayerSnapshot remoteRankHandshake;
        private RankedMatchSnapshot sealedRankedMatch;
        private RankChangeReceipt localRankResultReceipt;

        public bool IsOnlineDuelActive =>
            role != SessionRole.None && networkManager != null &&
            (networkManager.IsClient || networkManager.IsServer);

        public bool IsHost => role == SessionRole.Host;
        public CompetitivePolicy CompetitivePolicy => competitivePolicy;
        public string CurrentMatchId => currentMatchId ?? string.Empty;
        public string Status => status;
        public string RoomCode => roomCode;
        public string RelayRegion => relayRegion;
        public OnlineMatchFlowState FlowState => flowState;
        public OnlineLoadingScreenPresenter TransitionPresenter =>
            loadingPresenter;
        public bool RequiresPresentationLock =>
            IsOnlineDuelActive &&
            (clientSynchronizing || hostAwaitingReconnect || reconnecting);
        public string InteractionWaitMessage
        {
            get
            {
                if (pendingResponseRequestId != 0)
                {
                    float elapsed = pendingResponseStartedAt > 0f
                        ? Mathf.Max(
                            0f,
                            Time.realtimeSinceStartup -
                            pendingResponseStartedAt)
                        : 0f;
                    int rtt = RelayRoundTripTimeMs;
                    string latency = rtt > 0
                        ? $" · RTT {rtt} ms"
                        : string.Empty;
                    return elapsed >= ResponseResyncSeconds
                        ? $"CONEXAO LENTA · ressincronizando a resposta " +
                          $"({Mathf.CeilToInt(elapsed)}s){latency}"
                        : $"RESPOSTA ENVIADA · aguardando o anfitriao{latency}";
                }
                if (clientSynchronizing)
                    return "SINCRONIZANDO O DUELO COM O ANFITRIAO...";
                return string.Empty;
            }
        }

        // Narrow diagnostics surface used by the opt-in two-process smoke
        // runner. Normal players never call these members and the regular
        // lobby/UI flow remains unchanged.
        internal bool DiagnosticConnectionInProgress =>
            connectionOperationInProgress;
        internal bool DiagnosticMatchStarted => matchStarted;
        internal bool DiagnosticCanBeginMatch =>
            IsHost && remoteClientId != ulong.MaxValue &&
            remoteLoadout != null && clientDeckReady && !matchStarted;
        internal bool DiagnosticArenaSynchronized => IsHost
            ? hostCoreStarted && hostController != null &&
              clientArenaReady && beginDuelApplied && clientBeginApplied &&
              !hostAwaitingStateAckUnlock &&
              !clientSynchronizing
            : role == SessionRole.Client && replicaController != null &&
              lastReplicaStateVersion > 0 && beginDuelApplied;
        internal ulong DiagnosticStateVersion => IsHost
            ? authoritativeStateVersion
            : lastReplicaStateVersion;
        internal uint DiagnosticAcceptedRemoteCommands =>
            lastAcceptedClientSequence;
        internal bool DiagnosticLocalCommandAcknowledged =>
            role == SessionRole.Client && nextClientSequence > 0 &&
            pendingResponseRequestId == 0;
        internal DuelArenaController DiagnosticController => IsHost
            ? hostController
            : replicaController;

        internal void BeginHostingForDiagnostics()
        {
            BeginHosting();
        }

        internal void BeginJoiningForDiagnostics(string code)
        {
            joinCode = (code ?? string.Empty).Trim().ToUpperInvariant();
            BeginJoining();
        }

        internal void BeginMatchForDiagnostics()
        {
            diagnosticPreludeBypass = true;
            BeginHostMatch();
        }

        public void BeginTournamentHosting(TournamentDuelContext context)
        {
            if (context == null || !context.IsValid ||
                !context.LocalPlayerHosts)
            {
                status = "Contexto do confronto de torneio inválido.";
                return;
            }
            activeTournamentContext = context;
            competitivePolicy = context.competitivePolicy;
            tournamentLaunchRequested = true;
            showPanel = false;
            BeginHosting();
        }

        public void BeginTournamentJoining(
            TournamentDuelContext context,
            string code)
        {
            if (context == null || !context.IsValid ||
                context.LocalPlayerHosts)
            {
                status = "Contexto do confronto de torneio inválido.";
                return;
            }
            activeTournamentContext = context;
            competitivePolicy = context.competitivePolicy;
            tournamentLaunchRequested = true;
            showPanel = false;
            joinCode = (code ?? string.Empty).Trim().ToUpperInvariant();
            BeginJoining();
        }

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
            flowConfig ??= new OnlineMatchFlowConfig();
            EnsureNetworkManager();
            loadingPresenter = GetComponent<OnlineLoadingScreenPresenter>() ??
                gameObject.AddComponent<OnlineLoadingScreenPresenter>();
            loadingPresenter.ConfigureMinimumVisible(
                flowConfig.MinimumBlackScreenSeconds);
            resultPresenter = GetComponent<OnlineDuelResultPresenter>() ??
                gameObject.AddComponent<OnlineDuelResultPresenter>();
            SetFlowState(
                SceneManager.GetActiveScene().name == ProjectIdentity.MainMenuScene
                    ? OnlineMatchFlowState.Menu
                    : OnlineMatchFlowState.InSessionWaiting);
            SceneManager.sceneLoaded += OnDuelSceneLoaded;
            DuelOnlineBridge.SubmitReplicaChoice = SubmitRemoteChoice;
            DuelOnlineBridge.SubmitReplicaResponse = SubmitRemoteResponse;
            persistedReconnectCoroutine = StartCoroutine(
                TryRestorePersistedClientSession());
        }

        private void OnApplicationPause(bool paused)
        {
            if (role == SessionRole.None || !sessionCoordinator.HasSession)
                return;

            PersistReconnectTicket();
            if (matchStarted && flowState != OnlineMatchFlowState.ResultScreen)
            {
                loadingPresenter?.Show(
                    paused ? "Conexão pausada" : "Reconectando...",
                    "Aguardando a conexão segura com o outro jogador.");
            }
            if (!paused && matchStarted &&
                flowState != OnlineMatchFlowState.InDuel)
            {
                flowStateEnteredAt = Time.realtimeSinceStartup;
            }
            _ = sessionCoordinator.SetPlayerStateAsync(
                paused ? "paused" : "connected",
                localLoadout != null);
            if (!paused && role == SessionRole.Client &&
                (networkManager == null || !networkManager.IsConnectedClient))
            {
                StartClientReconnect();
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused && role == SessionRole.Client &&
                sessionCoordinator.HasSession &&
                (networkManager == null || !networkManager.IsConnectedClient))
            {
                StartClientReconnect();
            }
        }

        private void OnDestroy()
        {
            DuelOnlineBridge.CompleteOnlineArenaTransition();
            SceneManager.sceneLoaded -= OnDuelSceneLoaded;
            if (arenaAttachRetry != null)
                StopCoroutine(arenaAttachRetry);
            if (helloRetry != null)
                StopCoroutine(helloRetry);
            if (helloRequestRetry != null)
                StopCoroutine(helloRequestRetry);
            if (startRetry != null)
                StopCoroutine(startRetry);
            if (sceneTransitionRoutine != null)
                StopCoroutine(sceneTransitionRoutine);
            if (beginDuelRoutine != null)
                StopCoroutine(beginDuelRoutine);
            if (stateHeartbeat != null)
                StopCoroutine(stateHeartbeat);
            if (pendingStateBroadcast != null)
                StopCoroutine(pendingStateBroadcast);
            if (reconnectCoroutine != null)
                StopCoroutine(reconnectCoroutine);
            if (hostReconnectGraceCoroutine != null)
                StopCoroutine(hostReconnectGraceCoroutine);
            if (persistedReconnectCoroutine != null)
                StopCoroutine(persistedReconnectCoroutine);
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
                if (networkManager.ConnectionApprovalCallback ==
                    ApproveConnection)
                {
                    networkManager.ConnectionApprovalCallback = null;
                }
            }
            outboundWireTransfers.Clear();
            inboundWireTransfers.Clear();
            completedWireTransfers.Clear();
            pendingPresentationEvents.Clear();
            if (Instance == this)
                Instance = null;
        }

        public void ShowPanel(
            bool join = false,
            CompetitivePolicy policy = CompetitivePolicy.Unranked)
        {
            if (matchStarted &&
                SceneManager.GetActiveScene().name == DuelArenaScene)
            {
                showPanel = false;
                return;
            }
            showPanel = true;
            competitivePolicy = policy;
            rankedRoomCreationPanel =
                policy == CompetitivePolicy.Ranked && !join;
            if (!IsOnlineDuelActive)
                automaticRankedMatchmaking = false;
            focusJoinCode = join;
            requestJoinFocus = join;
            status = IsOnlineDuelActive
                ? status
                : "Escolha um deck válido e conecte-se por Relay.";
        }

        public void StartRankedMatchmaking()
        {
            if (IsOnlineDuelActive)
            {
                showPanel = true;
                status = "Ja existe uma sessao online em andamento.";
                return;
            }
            competitivePolicy = CompetitivePolicy.Ranked;
            CancelRankedBotFallback();
            rankedBotFallbackDeadline = Time.realtimeSinceStartup +
                RankedBotFallbackPolicy.DelaySeconds(
                    UnityEngine.Random.value);
            showPanel = true;
            rankedRoomCreationPanel = false;
            // Select the dedicated queue presentation before validation or
            // asynchronous service setup. A validation error must never fall
            // back to the create/join-room window.
            automaticRankedMatchmaking = true;
            focusJoinCode = false;
            requestJoinFocus = false;
            status = "Preparando a busca por um rival ranqueado...";
            BeginAutomaticRankedMatchmaking();
        }

        public void LeaveRoom()
        {
            if (connectionOperationInProgress)
            {
                status = "A operacao online atual ainda esta terminando.";
                return;
            }
            connectionOperationInProgress = true;
            _ = LeaveRoomAsync();
        }

        public void ReturnToMenuAfterOnlineMatch()
        {
            if (resultLeaveInProgress)
                return;
            resultLeaveInProgress = true;
            connectionOperationInProgress = true;
            resultPresenter?.SetReturnButtonInteractable(false);
            loadingPresenter?.Show(
                "Voltando ao menu...",
                "Encerrando a sessão e salvando o resultado.");
            SetFlowState(OnlineMatchFlowState.Leaving);
            _ = ReturnToMenuAfterOnlineMatchAsync();
        }

        private async Task ReturnToMenuAfterOnlineMatchAsync()
        {
            try
            {
                if (tournamentResultReportTask != null)
                {
                    try
                    {
                        await tournamentResultReportTask;
                    }
                    catch (Exception reportException)
                    {
                        Debug.LogWarning(
                            "[Tournament] Resultado pendente ao sair: " +
                            reportException.GetBaseException().Message);
                    }
                }
                await LeaveRoomAsync();
                if (SceneManager.GetActiveScene().name !=
                        ProjectIdentity.MainMenuScene &&
                    Application.CanStreamedLevelBeLoaded(
                        ProjectIdentity.MainMenuScene))
                {
                    SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
                }
                resultPresenter?.Hide();
                loadingPresenter?.Hide();
                SetFlowState(OnlineMatchFlowState.Menu);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[MP] stage=result-exit local-cleanup=done error=" +
                    exception.GetBaseException().Message);
                if (SceneManager.GetActiveScene().name !=
                        ProjectIdentity.MainMenuScene &&
                    Application.CanStreamedLevelBeLoaded(
                        ProjectIdentity.MainMenuScene))
                {
                    SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
                }
                resultPresenter?.Hide();
                loadingPresenter?.Hide();
                SetFlowState(OnlineMatchFlowState.Menu);
            }
            finally
            {
                connectionOperationInProgress = false;
                resultLeaveInProgress = false;
            }
        }

        public void AttachOnlineArena(CardArenaBootstrap arena)
        {
            if (!IsOnlineDuelActive || arena == null)
                return;

            // The hello/loadout snapshots are sealed before the duel starts.
            // Only stable IDs and public profile presentation cross the wire;
            // reconnecting therefore restores the same two identities.
            arena.ApplyDuelIdentities(
                localLoadout?.identity,
                remoteDuelIdentity ?? remoteLoadout?.identity,
                currentMatchId,
                competitivePolicy == CompetitivePolicy.Ranked);

            DuelArenaController controller =
                arena.GetComponent<DuelArenaController>();
            if (controller == null)
                return;

            if (IsHost)
            {
                bool firstAttachment = hostController != controller;
                if (hostController != null && firstAttachment)
                    hostController.CoreEventPresented -= OnHostCoreEvent;
                hostController = controller;
                if (firstAttachment)
                    hostController.ConfigureRemotePlayerOneAuthority(true);
                hostController.CoreEventPresented -= OnHostCoreEvent;
                hostController.CoreEventPresented += OnHostCoreEvent;
                hostController.SetPresentationDecisionLocked(true);
                DuelTestPerspectiveController.Instance?.ConfigureClientSwitching(
                    false,
                    DuelPlayerSide.PlayerOne);
                localSceneReady = true;
                readinessBarrier.RegisterSceneReady(
                    currentMatchId,
                    currentTransitionEpoch,
                    0);
                loadingPresenter?.SetProgress(0.48f);
                SetFlowState(OnlineMatchFlowState.WaitingSceneReady);
                loadingPresenter?.SetText(
                    "Aguardando o outro jogador...",
                    "O campo local está pronto.");
                TryStartHostDuel();
                Debug.Log(
                    $"[MP] stage=arena-attached role=host " +
                    $"scene={arena.gameObject.scene.name} " +
                    $"coreStarted={hostCoreStarted}");
                return;
            }

            bool firstReplicaAttachment = replicaController != controller;
            replicaController = controller;
            if (firstReplicaAttachment)
            {
                // DuelNetworkProtocol rotates the snapshot before sending it,
                // so the local player's state is always slot P0 in this arena.
                replicaController.ConfigureNetworkReplica(0);
            }
            replicaController.SetPresentationDecisionLocked(true);
            localSceneReady = true;
            loadingPresenter?.SetProgress(0.48f);
            SetFlowState(OnlineMatchFlowState.WaitingSceneReady);
            DuelTestPerspectiveController.Instance?.ConfigureClientSwitching(
                false,
                DuelPlayerSide.PlayerOne);
            if (pendingReplicaState != null)
            {
                if (!ApplyReplicaState(pendingReplicaState))
                {
                    RequestResync("arena-attach-snapshot-apply-failed");
                    return;
                }
                SendStateAck(pendingReplicaState);
                if (beginDuelApplied)
                {
                    SetFlowState(OnlineMatchFlowState.InDuel);
                    DuelOnlineBridge.CompleteOnlineArenaTransition();
                    loadingPresenter?.Hide();
                }
                TryApplyPendingClientResult();
            }
            if (matchStarted)
            {
                SendClientReady(true, true);
                nextClientArenaReadyRetryTime =
                    Time.realtimeSinceStartup + ArenaReadyRetrySeconds;
                status = "Arena pronta. Sincronizando o campo com o host...";
                loadingPresenter?.SetText(
                    "Sincronizando partida...",
                    "Validando o snapshot inicial do anfitrião.");
            }
            else
            {
                status = "Conectado. Aguardando o host confirmar os decks.";
            }
            Debug.Log(
                $"[MP] stage=arena-attached role=client " +
                $"scene={arena.gameObject.scene.name} " +
                $"hasSnapshot={pendingReplicaState != null}");
        }

        private void OnDuelSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsOnlineDuelActive ||
                !string.Equals(
                    scene.name,
                    DuelArenaScene,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (arenaAttachRetry != null)
                StopCoroutine(arenaAttachRetry);
            arenaAttachRetry = StartCoroutine(
                AttachArenaAfterSceneInitialization(scene.handle));
            Debug.Log(
                $"[MP] stage=arena-scene-loaded handle={scene.handle} " +
                $"role={role}");
        }

        private IEnumerator AttachArenaAfterSceneInitialization(
            SceneHandle sceneHandle)
        {
            // The authored arena builds its controller and presentation over
            // the first frames. This mirrors the proven project and also
            // tolerates slower Android scene activation.
            yield return null;
            yield return null;
            yield return null;

            float deadline =
                Time.realtimeSinceStartup + ArenaAttachTimeoutSeconds;
            while (IsOnlineDuelActive &&
                   Time.realtimeSinceStartup < deadline)
            {
                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.handle != sceneHandle ||
                    !string.Equals(
                        activeScene.name,
                        DuelArenaScene,
                        StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                CardArenaBootstrap arena = FindOnlineArena(activeScene);
                if (arena != null)
                {
                    AttachOnlineArena(arena);
                    arenaAttachRetry = null;
                    yield break;
                }
                yield return null;
            }

            if (IsOnlineDuelActive)
            {
                status =
                    "A arena foi aberta, mas o campo ainda não ficou pronto. " +
                    "Tentando manter a conexão com o rival.";
                Debug.LogWarning(
                    "[MP] stage=arena-attach-timeout scene=" +
                    SceneManager.GetActiveScene().name);
            }
            arenaAttachRetry = null;
        }

        private static CardArenaBootstrap FindOnlineArena(Scene scene)
        {
            CardArenaBootstrap fallback = null;
            CardArenaBootstrap[] arenas =
                FindObjectsByType<CardArenaBootstrap>(
                    FindObjectsInactive.Include);
            foreach (CardArenaBootstrap arena in arenas)
            {
                if (arena == null || arena.gameObject.scene != scene ||
                    !arena.gameObject.activeInHierarchy)
                    continue;
                fallback ??= arena;
                if (arena.IsPrimaryDuelInterface)
                {
                    return arena;
                }
            }
            return fallback;
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
                requestId == 0 ||
                clientSynchronizing || lastReplicaStateVersion == 0 ||
                replicaController?.PresentationDecisionLocked == true)
            {
                return;
            }
            // The replica keeps displaying the last confirmed prompt until
            // the host processes this response. Lock it here so a double tap
            // cannot submit two different answers for the same request.
            replicaController?.SetPresentationDecisionLocked(true);
            pendingResponseRequestId = requestId;
            pendingResponseBytes = (byte[])response.Clone();
            pendingCommandId = ++nextClientCommandId;
            pendingClientSequence = ++nextClientSequence;
            pendingExpectedStateVersion = lastReplicaStateVersion;
            pendingResponseStartedAt = Time.realtimeSinceStartup;
            nextPendingResponseResyncTime =
                pendingResponseStartedAt + ResponseResyncSeconds;
            nextResponseRetryTime = 0f;
            status = "Resposta enviada. Aguardando confirmação do anfitrião...";
            SendPendingClientResponse();
        }

        private void EnsureNetworkManager()
        {
            if (networkManager != null)
                return;

            transport = GetComponent<UnityTransport>() ??
                        gameObject.AddComponent<UnityTransport>();
            // Android can temporarily stop pumping frames while loading the
            // arena and card assets. The UTP default drops a silent peer
            // after 30 seconds, which incorrectly starts the reconnect grace
            // while both players are still loading the duel scene.
            transport.HeartbeatTimeoutMS = TransportHeartbeatMilliseconds;
            transport.DisconnectTimeoutMS =
                TransportDisconnectTimeoutMilliseconds;
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
            networkManager.NetworkConfig.ConnectionApproval = true;
            networkManager.ConnectionApprovalCallback = ApproveConnection;
            // No NetworkObject or network prefab is spawned by this duel.
            // Keeping prefab hashes out of the NGO connection gate allows
            // editor, PC and mobile builds with the same wire protocol to
            // reach the explicit deck compatibility handshake below.
            networkManager.NetworkConfig.ForceSamePrefabs = false;
            networkManager.NetworkConfig.TickRate = NetworkTickRate;
            networkManager.NetworkConfig.ClientConnectionBufferTimeout = 30;
            // Each peer deliberately opens the arena locally after the Relay
            // handshake. This card game has no spawned scene objects, so NGO
            // scene replication would only add races.
            networkManager.NetworkConfig.EnableSceneManagement = false;
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
                WirePacketMessage,
                OnWirePacketMessage);
            networkManager.CustomMessagingManager.RegisterNamedMessageHandler(
                ResponseFastMessage,
                OnFastResponseMessage);
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
                WirePacketMessage);
            networkManager.CustomMessagingManager.UnregisterNamedMessageHandler(
                ResponseFastMessage);
            handlersRegistered = false;
        }

        private async void BeginHosting()
        {
            bool tournamentLaunch = tournamentLaunchRequested;
            tournamentLaunchRequested = false;
            if (!tournamentLaunch)
                ClearTournamentDuelContext();
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
                ResetMatchState(true);
                ClearReconnectTicket();
                roomCode = string.Empty;
                disconnectReason = string.Empty;
                status = "Autenticando na Unity e criando a sala...";
                localLoadout = loadout;
                localRankHandshake = CaptureLocalRankSnapshot();
                if (competitivePolicy == CompetitivePolicy.Ranked &&
                    (localRankHandshake == null ||
                     !localRankHandshake.IsValid))
                {
                    status = "O perfil ranqueado local não pôde ser carregado.";
                    return;
                }
                await InitializeServices();
                ConfigureConnectionIdentity();
                role = SessionRole.Host;
                IHostSession session = await sessionCoordinator.CreateAsync(
                    localLoadout,
                    ProtocolVersion);
                roomCode = session.Code;
                if (activeTournamentContext != null)
                {
                    tournamentResultReportTask = TournamentOnlineSession
                        .EnsureInstance()
                        .NotifyDuelRoomCreatedAsync(
                            activeTournamentContext,
                            roomCode);
                }
                relayRegion = "QoS automatico";
                relayRegionDescription =
                    "melhor regiao escolhida automaticamente";
                RegisterHandlers();
                await sessionCoordinator.SetPlayerStateAsync("connected", true);

                status = $"Sala criada na região Relay {GetRelayRegionLabel()}. " +
                    "Compartilhe o código e aguarde o rival.";
                showPanel = activeTournamentContext == null;
            }
            catch (Exception exception)
            {
                RuntimeDiagnosticRecorder.Record(
                    "F08",
                    "Multiplayer",
                    nameof(DuelOnlineSession),
                    "Host room creation failed.",
                    mode: "online-host",
                    exception: exception);
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
            bool tournamentLaunch = tournamentLaunchRequested;
            tournamentLaunchRequested = false;
            if (!tournamentLaunch)
                ClearTournamentDuelContext();
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
                ResetMatchState(true);
                ClearReconnectTicket();
                roomCode = string.Empty;
                disconnectReason = string.Empty;
                status = "Autenticando e entrando na sala...";
                localLoadout = loadout;
                localRankHandshake = CaptureLocalRankSnapshot();
                if (competitivePolicy == CompetitivePolicy.Ranked &&
                    (localRankHandshake == null ||
                     !localRankHandshake.IsValid))
                {
                    status = "O perfil ranqueado local não pôde ser carregado.";
                    return;
                }
                await InitializeServices();
                ConfigureConnectionIdentity();
                roomCode = normalizedCode;
                role = SessionRole.Client;
                helloAccepted = false;
                ISession session = await sessionCoordinator.JoinByCodeAsync(
                    normalizedCode,
                    localLoadout,
                    ProtocolVersion);
                roomCode = session.Code;
                relayRegion = "QoS automatico";
                relayRegionDescription =
                    "regiao Relay definida pelo anfitriao";
                RegisterHandlers();
                status = "Conectando ao host...";
                await sessionCoordinator.SetPlayerStateAsync("connected", false);
                PersistReconnectTicket();
                if (networkManager.IsConnectedClient)
                    StartClientDeckHandshake();
            }
            catch (Exception exception)
            {
                RuntimeDiagnosticRecorder.Record(
                    "F08",
                    "Multiplayer",
                    nameof(DuelOnlineSession),
                    "Client room join failed.",
                    mode: "online-client",
                    exception: exception);
                ResetAfterFailedConnection(
                    DescribeJoinFailure(exception));
            }
            finally
            {
                connectionOperationInProgress = false;
            }
        }

        private async void BeginAutomaticRankedMatchmaking()
        {
            ClearTournamentDuelContext();
            if (connectionOperationInProgress)
            {
                status = "Uma conexao ja esta sendo preparada. Aguarde.";
                return;
            }
            if (!TryGetLocalLoadout(out DuelDeckLoadout loadout, out string error))
            {
                status = error;
                return;
            }
            if (IsOnlineDuelActive)
            {
                status = "Ja existe uma sessao online em andamento.";
                return;
            }
            if (networkManager != null && networkManager.ShutdownInProgress)
            {
                status = "A sessao anterior ainda esta sendo encerrada. Aguarde.";
                return;
            }

            connectionOperationInProgress = true;
            try
            {
                ResetMatchState(true);
                automaticRankedMatchmaking = true;
                competitivePolicy = CompetitivePolicy.Ranked;
                ClearReconnectTicket();
                roomCode = string.Empty;
                disconnectReason = string.Empty;
                status = "Buscando um rival compativel no ranqueado...";
                localLoadout = loadout;
                localRankHandshake = CaptureLocalRankSnapshot();
                if (localRankHandshake == null ||
                    !localRankHandshake.IsValid)
                {
                    automaticRankedMatchmaking = false;
                    status = "O perfil ranqueado local nao pode ser carregado.";
                    return;
                }

                await InitializeServices();
                ConfigureConnectionIdentity();
                ISession session = await sessionCoordinator
                    .MatchmakeRankedAsync(localLoadout, ProtocolVersion);
                role = session.IsHost
                    ? SessionRole.Host
                    : SessionRole.Client;
                roomCode = session.Code;
                relayRegion = "QoS automatico";
                relayRegionDescription = session.IsHost
                    ? "melhor regiao escolhida automaticamente"
                    : "regiao Relay definida pelo matchmaking";
                RegisterHandlers();

                if (session.IsHost)
                {
                    await sessionCoordinator.SetPlayerStateAsync(
                        "connected",
                        true);
                    status = "Fila ranqueada criada. Aguardando um rival " +
                        "compativel...";
                    ScheduleRankedBotFallback();
                }
                else
                {
                    helloAccepted = false;
                    await sessionCoordinator.SetPlayerStateAsync(
                        "connected",
                        false);
                    status = "Rival encontrado. Validando os dois decks...";
                    PersistReconnectTicket();
                    if (networkManager.IsConnectedClient)
                        StartClientDeckHandshake();
                }
                showPanel = true;
            }
            catch (Exception exception)
            {
                RuntimeDiagnosticRecorder.Record(
                    "F08",
                    "RankedMatchmaking",
                    nameof(DuelOnlineSession),
                    "Automatic ranked matchmaking failed.",
                    mode: "ranked-matchmaking",
                    exception: exception);
                ResetAfterFailedConnection(
                    "Nao foi possivel iniciar o ranqueado: " +
                    exception.GetBaseException().Message);
                showPanel = true;
            }
            finally
            {
                connectionOperationInProgress = false;
            }
        }

        private void ScheduleRankedBotFallback()
        {
            CancelRankedBotFallback();
            if (rankedBotFallbackDeadline <= 0f)
            {
                rankedBotFallbackDeadline = Time.realtimeSinceStartup +
                    RankedBotFallbackPolicy.DelaySeconds(
                        UnityEngine.Random.value);
            }
            float delay = Mathf.Max(
                0f,
                rankedBotFallbackDeadline - Time.realtimeSinceStartup);
            rankedBotFallbackCoroutine = StartCoroutine(
                WaitForRankedBotFallback(delay));
        }

        private IEnumerator WaitForRankedBotFallback(float delaySeconds)
        {
            float deadline = Time.realtimeSinceStartup + delaySeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!automaticRankedMatchmaking ||
                    competitivePolicy != CompetitivePolicy.Ranked ||
                    !IsHost ||
                    !IsOnlineDuelActive ||
                    remoteClientId != ulong.MaxValue ||
                    matchStarted)
                {
                    rankedBotFallbackCoroutine = null;
                    yield break;
                }

                int remaining = Mathf.Max(
                    1,
                    Mathf.CeilToInt(deadline - Time.realtimeSinceStartup));
                status = "Buscando rival ranqueado compativel... " +
                    $"IA disponivel em ate {remaining}s.";
                yield return new WaitForSecondsRealtime(1f);
            }

            rankedBotFallbackCoroutine = null;
            if (automaticRankedMatchmaking &&
                competitivePolicy == CompetitivePolicy.Ranked &&
                IsHost &&
                IsOnlineDuelActive &&
                remoteClientId == ulong.MaxValue &&
                !matchStarted)
            {
                BeginRankedBotFallback();
            }
        }

        private async void BeginRankedBotFallback()
        {
            if (rankedBotFallbackInProgress ||
                !automaticRankedMatchmaking ||
                !IsHost ||
                remoteClientId != ulong.MaxValue ||
                matchStarted)
            {
                return;
            }

            rankedBotFallbackInProgress = true;
            rankedBotFallbackDeadline = 0f;
            automaticRankedMatchmaking = false;
            connectionOperationInProgress = true;
            status = "Nenhum jogador entrou na fila. Preparando um rival IA " +
                "compativel com seu elo...";
            await LeaveRoomAsync();
            showPanel = false;
            rankedBotFallbackInProgress = false;

            if (SceneManager.GetActiveScene().name == DuelArenaScene)
                return;
            GameFrontendBootstrap frontend =
                FindAnyObjectByType<GameFrontendBootstrap>(
                    FindObjectsInactive.Include);
            if (frontend == null)
            {
                status = "A fila expirou, mas a Central de Duelos nao esta " +
                    "disponivel para iniciar a IA.";
                showPanel = true;
                return;
            }
            frontend.StartRankedBotFallbackFromMatchmaking();
        }

        private void CancelRankedBotFallback()
        {
            if (rankedBotFallbackCoroutine == null)
                return;
            StopCoroutine(rankedBotFallbackCoroutine);
            rankedBotFallbackCoroutine = null;
        }

        private static async Task InitializeServices()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                string authenticationProfile = CommandLineValue(
                    "-arcaneAuthProfile");
                if (string.IsNullOrWhiteSpace(authenticationProfile))
                {
                    await UnityServices.InitializeAsync();
                }
                else
                {
                    var options = new InitializationOptions();
                    options.SetProfile(authenticationProfile.Trim());
                    await UnityServices.InitializeAsync(options);
                }
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static string CommandLineValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(
                        arguments[index],
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }
            return string.Empty;
        }

        private void ConfigureConnectionIdentity()
        {
            var approval = new ApprovalPayload
            {
                p = AuthenticationService.Instance.PlayerId ?? string.Empty,
                v = ProtocolVersion,
                c = MultiplayerSessionCoordinator.ComputeCompatibilityHash()
            };
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(approval));
            if (payload.Length > 192)
            {
                throw new InvalidOperationException(
                    "Identidade de conexão excedeu o limite seguro do NGO.");
            }
            networkManager.NetworkConfig.ConnectionData = payload;
        }

        private void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Pending = false;
            response.CreatePlayerObject = false;

            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                return;
            }

            try
            {
                string json = Encoding.UTF8.GetString(
                    request.Payload ?? Array.Empty<byte>());
                ApprovalPayload approval =
                    JsonUtility.FromJson<ApprovalPayload>(json);
                bool compatible = approval != null &&
                    approval.v == ProtocolVersion &&
                    approval.c ==
                        MultiplayerSessionCoordinator.ComputeCompatibilityHash();
                bool sessionMember = approval != null &&
                    sessionCoordinator.HasMember(approval.p);
                bool capacityAvailable = networkManager != null &&
                    networkManager.ConnectedClientsIds.Count < 2;

                response.Approved = compatible && sessionMember &&
                    capacityAvailable && (!matchStarted ||
                        hostAwaitingReconnect);
                response.Reason = response.Approved
                    ? string.Empty
                    : "Sala cheia, partida iniciada ou versão incompatível.";
                Debug.Log("[MP] stage=connection-approval approved=" +
                    response.Approved + " member=" + sessionMember +
                    " compatible=" + compatible);
            }
            catch (Exception exception)
            {
                response.Approved = false;
                response.Reason = "Identidade de conexão inválida.";
                Debug.LogWarning(
                    "[MP] stage=connection-approval approved=false error=" +
                    exception.GetType().Name);
            }
        }

        private IEnumerator TryRestorePersistedClientSession()
        {
            yield return null;
            string sessionId = PlayerPrefs.GetString(
                ReconnectSessionKey,
                string.Empty);
            string persistedProtocol = PlayerPrefs.GetString(
                ReconnectProtocolKey,
                string.Empty);
            if (string.IsNullOrWhiteSpace(sessionId) ||
                persistedProtocol != ProtocolVersion)
            {
                persistedReconnectCoroutine = null;
                yield break;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long.TryParse(
                PlayerPrefs.GetString(ReconnectTimestampKey, "0"),
                out long timestamp);
            if (timestamp <= 0 || now - timestamp >
                flowConfig.ReconnectGraceSeconds + 15f)
            {
                ClearReconnectTicket();
                persistedReconnectCoroutine = null;
                yield break;
            }

            DuelDeckLoadout restoredLoadout = null;
            string loadoutError = string.Empty;
            float loadoutDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < loadoutDeadline &&
                   !TryGetLocalLoadout(out restoredLoadout, out loadoutError))
            {
                yield return null;
            }
            if (restoredLoadout == null)
            {
                ClearReconnectTicket();
                persistedReconnectCoroutine = null;
                yield break;
            }

            connectionOperationInProgress = true;
            localLoadout = restoredLoadout;
            role = SessionRole.Client;
            roomCode = PlayerPrefs.GetString(ReconnectRoomKey, string.Empty);
            currentMatchId = PlayerPrefs.GetString(
                ReconnectMatchKey,
                string.Empty);
            matchStarted = !string.IsNullOrWhiteSpace(currentMatchId);
            string eligibilityJson = PlayerPrefs.GetString(
                ReconnectRewardEligibilityKey,
                string.Empty);
            localRewardEligibilityAtMatchStart =
                matchStarted && !string.IsNullOrWhiteSpace(eligibilityJson)
                    ? DeserializeRewardEligibility(eligibilityJson)
                    : null;
            ulong.TryParse(
                PlayerPrefs.GetString(ReconnectStateVersionKey, "0"),
                out lastReplicaStateVersion);
            clientSynchronizing = true;
            status = "Restaurando a sessão online anterior...";

            Task servicesTask = InitializeServices();
            while (!servicesTask.IsCompleted)
                yield return null;
            if (servicesTask.IsFaulted)
            {
                ResetAfterFailedConnection(
                    "Não foi possível restaurar a autenticação online.");
                connectionOperationInProgress = false;
                persistedReconnectCoroutine = null;
                yield break;
            }

            ConfigureConnectionIdentity();
            Task<ISession> reconnectTask =
                sessionCoordinator.ReconnectAsync(sessionId);
            while (!reconnectTask.IsCompleted)
                yield return null;
            if (reconnectTask.Status != TaskStatus.RanToCompletion ||
                reconnectTask.Result == null || networkManager == null ||
                !networkManager.IsConnectedClient)
            {
                ResetAfterFailedConnection(
                    "A sessão anterior não está mais disponível para reconexão.");
                connectionOperationInProgress = false;
                persistedReconnectCoroutine = null;
                yield break;
            }

            handlersRegistered = false;
            RegisterHandlers();
            relayRegion = "QoS automatico";
            relayRegionDescription = "regiao Relay definida pelo anfitriao";
            connectionOperationInProgress = false;
            persistedReconnectCoroutine = null;
            status = "Sessão restaurada. Ressincronizando a partida...";
            PersistReconnectTicket();
            _ = sessionCoordinator.SetPlayerStateAsync("connected", true);
            if (matchStarted)
            {
                loadingPresenter?.Show(
                    "Reconectando...",
                    "Aguardando o anfitrião confirmar a partida.");
            }
        }

        private void PersistReconnectTicket()
        {
            if (role != SessionRole.Client ||
                string.IsNullOrWhiteSpace(sessionCoordinator.SessionId))
            {
                return;
            }

            PlayerPrefs.SetString(
                ReconnectSessionKey,
                sessionCoordinator.SessionId);
            PlayerPrefs.SetString(ReconnectRoomKey, roomCode ?? string.Empty);
            PlayerPrefs.SetString(
                ReconnectMatchKey,
                currentMatchId ?? string.Empty);
            PlayerPrefs.SetString(ReconnectProtocolKey, ProtocolVersion);
            PlayerPrefs.SetString(
                ReconnectStateVersionKey,
                lastReplicaStateVersion.ToString());
            PlayerPrefs.SetString(
                ReconnectTimestampKey,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.SetString(
                ReconnectRewardEligibilityKey,
                localRewardEligibilityAtMatchStart == null
                    ? string.Empty
                    : JsonUtility.ToJson(
                        localRewardEligibilityAtMatchStart));
            PlayerPrefs.Save();
        }

        private static CoinRewardEligibilitySnapshot
            DeserializeRewardEligibility(string json)
        {
            try
            {
                return JsonUtility.FromJson<CoinRewardEligibilitySnapshot>(
                    json);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static void ClearReconnectTicket()
        {
            PlayerPrefs.DeleteKey(ReconnectSessionKey);
            PlayerPrefs.DeleteKey(ReconnectRoomKey);
            PlayerPrefs.DeleteKey(ReconnectMatchKey);
            PlayerPrefs.DeleteKey(ReconnectProtocolKey);
            PlayerPrefs.DeleteKey(ReconnectStateVersionKey);
            PlayerPrefs.DeleteKey(ReconnectTimestampKey);
            PlayerPrefs.DeleteKey(ReconnectRewardEligibilityKey);
            PlayerPrefs.Save();
        }

        private void OnClientConnected(ulong clientId)
        {
            if (networkManager == null)
                return;
            if (networkManager.IsServer)
            {
                if (clientId == NetworkManager.ServerClientId)
                    return;
                CancelRankedBotFallback();
                rankedBotFallbackDeadline = 0f;
                bool resumedMatch = hostAwaitingReconnect && matchStarted &&
                    remoteLoadout != null;
                remoteClientId = clientId;
                hostAwaitingReconnect = false;
                if (resumedMatch)
                    flowStateEnteredAt = Time.realtimeSinceStartup;
                clientSynchronizing = resumedMatch;
                commandTokens = CommandBurstCapacity;
                lastCommandTokenTime = Time.realtimeSinceStartup;
                if (hostReconnectGraceCoroutine != null)
                {
                    StopCoroutine(hostReconnectGraceCoroutine);
                    hostReconnectGraceCoroutine = null;
                }
                if (!resumedMatch)
                {
                    remoteLoadout = null;
                    clientDeckReady = false;
                }
                clientReceivedStart = false;
                clientArenaReady = false;
                if (resumedMatch)
                {
                    if (hostCoreStarted)
                    {
                        hostAwaitingStateAckUnlock = true;
                        status = "Rival reconectado. Restaurando o estado da partida...";
                        StartHostStartHandshake();
                    }
                    else
                    {
                        hostAwaitingStateAckUnlock = false;
                        status = "Rival reconectado. Reiniciando a escolha inicial...";
                        BeginOnlinePreludeRound();
                    }
                }
                else
                {
                    status = "Rival conectado. Validando o deck recebido...";
                    StartHostDeckRequest();
                }
                return;
            }

            if (role == SessionRole.Client &&
                clientId == networkManager.LocalClientId)
            {
                reconnecting = false;
                status = "Conectado. Enviando o deck para validação do host...";
                if (matchStarted && helloAccepted)
                {
                    bool arenaReady = replicaController != null &&
                        SceneManager.GetActiveScene().name == DuelArenaScene;
                    SendClientReady(true, arenaReady);
                }
                else
                {
                    StartClientDeckHandshake();
                }
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

            if (helloRetry == null)
                helloRetry = StartCoroutine(SendHelloUntilAccepted());
        }

        private void StartHostDeckRequest()
        {
            if (!IsHost || remoteClientId == ulong.MaxValue ||
                remoteLoadout != null)
            {
                return;
            }

            if (helloRequestRetry != null)
                StopCoroutine(helloRequestRetry);
            helloRequestRetry = StartCoroutine(RequestHelloUntilReceived());
        }

        private IEnumerator RequestHelloUntilReceived()
        {
            yield return null;

            int attempts = 0;
            while (attempts < MaximumHandshakeAttempts &&
                   IsHost && remoteLoadout == null &&
                   remoteClientId != ulong.MaxValue &&
                   networkManager != null && networkManager.IsServer)
            {
                attempts++;
                SendToClient(remoteClientId, HelloRequestMessage,
                    new ProtocolPayload { protocolVersion = ProtocolVersion },
                    NetworkDelivery.ReliableSequenced);

                if (attempts > 1)
                {
                    status = $"Rival conectado. Solicitando o deck novamente ({attempts})...";
                }
                yield return new WaitForSecondsRealtime(HandshakeRetrySeconds);
            }

            if (remoteLoadout == null && IsHost &&
                remoteClientId != ulong.MaxValue)
            {
                status = "O rival conectou, mas o deck nao chegou. " +
                    "A conexao continua aberta para uma nova tentativa.";
            }
            helloRequestRetry = null;
        }

        private void ProcessHelloRequestMessage(
            ulong senderClientId,
            ProtocolPayload request)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                request == null ||
                helloAccepted)
            {
                return;
            }
            if (request.protocolVersion != ProtocolVersion)
            {
                RejectIncompatibleHost();
                return;
            }

            SendClientHello();
            StartClientDeckHandshake();
        }

        private void SendClientHello()
        {
            if (role != SessionRole.Client || networkManager == null ||
                !networkManager.IsConnectedClient || localLoadout == null ||
                helloAccepted)
            {
                return;
            }

            SendToServer(HelloMessage, new HelloPayload
            {
                protocolVersion = ProtocolVersion,
                compatibility = ProjectIdentity.MultiplayerCompatibility,
                coreApiVersion = ProjectIdentity.CoreApiVersion,
                coreCommit = ProjectIdentity.CoreCommit,
                loadout = localLoadout,
                competitivePolicy = competitivePolicy,
                rankPlayer = localRankHandshake ?? CaptureLocalRankSnapshot()
            });
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
                SendClientHello();

                status = attempts == 1
                    ? "Lobby conectado. Enviando o deck ao anfitriao..."
                    : $"Aguardando confirmacao do anfitriao. Reenviando deck ({attempts})...";
                yield return new WaitForSecondsRealtime(HandshakeRetrySeconds);
            }

            if (!helloAccepted && role == SessionRole.Client &&
                networkManager != null && networkManager.IsConnectedClient)
            {
                status = "O host não confirmou o deck. Ele pode ter fechado " +
                    "a sala, iniciado outra versão do jogo ou perdido a conexão.";
            }
            helloRetry = null;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.LogWarning(
                $"[MP] stage=peer-disconnected client={clientId} " +
                $"role={role} scene={SceneManager.GetActiveScene().name} " +
                $"reason={networkManager?.DisconnectReason ?? "none"}");
            if (networkManager == null ||
                clientId != networkManager.LocalClientId &&
                clientId != remoteClientId)
            {
                return;
            }

            if (matchRewardFinalized ||
                flowState == OnlineMatchFlowState.ResultScreen)
            {
                if (clientId == remoteClientId)
                    remoteClientId = ulong.MaxValue;
                status = "Resultado confirmado. Você já pode voltar ao menu.";
                return;
            }

            if (clientId == remoteClientId && IsHost)
            {
                if (!hostCoreStarted && preludeResultRoutine != null)
                {
                    StopCoroutine(preludeResultRoutine);
                    preludeResultRoutine = null;
                }
                remoteClientId = ulong.MaxValue;
                hostAwaitingReconnect = true;
                clientSynchronizing = true;
                reconnectDeadline =
                    Time.realtimeSinceStartup +
                    flowConfig.ReconnectGraceSeconds;
                hostController?.SetPresentationDecisionLocked(true);
                loadingPresenter?.Show(
                    "Reconectando...",
                    "Aguardando o outro jogador retornar à partida.");
                status = "O rival perdeu a conexão. Aguardando reconexão por 45 segundos...";
                if (hostReconnectGraceCoroutine != null)
                    StopCoroutine(hostReconnectGraceCoroutine);
                hostReconnectGraceCoroutine = StartCoroutine(
                    WaitForRemoteReconnect());
                return;
            }

            if (clientId == networkManager.LocalClientId &&
                !networkManager.IsServer && role == SessionRole.Client)
            {
                replicaController?.SetPresentationDecisionLocked(true);
                UnregisterHandlers();
                clientSynchronizing = true;
                status = "Conexão interrompida. Tentando reconectar à partida...";
                loadingPresenter?.Show(
                    "Reconectando...",
                    "Restaurando a conexão com o anfitrião.");
                StartClientReconnect();
            }
        }

        private IEnumerator WaitForRemoteReconnect()
        {
            while (hostAwaitingReconnect &&
                   Time.realtimeSinceStartup < reconnectDeadline)
            {
                yield return new WaitForSecondsRealtime(0.25f);
            }

            hostReconnectGraceCoroutine = null;
            if (!hostAwaitingReconnect)
                yield break;

            bool duelWasRunning = matchStarted || hostCoreStarted ||
                SceneManager.GetActiveScene().name == DuelArenaScene;
            ResetAfterFailedConnection(
                "O rival não reconectou em 45 segundos. A partida foi encerrada.");
            if (duelWasRunning)
                yield return ReturnToMainMenuAfterDisconnect();
        }

        private void StartClientReconnect()
        {
            if (reconnecting || role != SessionRole.Client ||
                !sessionCoordinator.HasSession)
            {
                return;
            }
            reconnecting = true;
            reconnectDeadline =
                Time.realtimeSinceStartup + flowConfig.ReconnectGraceSeconds;
            if (reconnectCoroutine != null)
                StopCoroutine(reconnectCoroutine);
            reconnectCoroutine = StartCoroutine(ReconnectClientWithBackoff());
        }

        private IEnumerator ReconnectClientWithBackoff()
        {
            float[] delays = { 0.5f, 1f, 2f, 4f, 5f };
            int attempt = 0;
            while (role == SessionRole.Client &&
                   Time.realtimeSinceStartup < reconnectDeadline)
            {
                float delay = delays[Math.Min(attempt, delays.Length - 1)];
                delay += UnityEngine.Random.Range(0f, 0.25f);
                yield return new WaitForSecondsRealtime(delay);
                if (role != SessionRole.Client)
                    break;

                attempt++;
                status = $"Reconectando ao host (tentativa {attempt})...";
                Task<ISession> reconnectTask = sessionCoordinator.ReconnectAsync();
                while (!reconnectTask.IsCompleted)
                    yield return null;

                if (reconnectTask.Status == TaskStatus.RanToCompletion &&
                    reconnectTask.Result != null &&
                    networkManager != null && networkManager.IsConnectedClient)
                {
                    handlersRegistered = false;
                    RegisterHandlers();
                    reconnecting = false;
                    flowStateEnteredAt = Time.realtimeSinceStartup;
                    reconnectCoroutine = null;
                    status = "Reconectado. Ressincronizando o campo...";
                    bool arenaReady = replicaController != null &&
                        SceneManager.GetActiveScene().name == DuelArenaScene;
                    SendClientReady(matchStarted, arenaReady);
                    yield break;
                }

                string error = reconnectTask.Exception?.GetBaseException()
                    .GetType().Name ?? "indisponivel";
                Debug.LogWarning("[MP] stage=reconnect-retry attempt=" +
                    attempt + " error=" + error);
            }

            reconnecting = false;
            reconnectCoroutine = null;
            bool duelWasRunning = matchStarted ||
                SceneManager.GetActiveScene().name == DuelArenaScene;
            ResetAfterFailedConnection(
                "Não foi possível reconectar ao host em 45 segundos.");
            if (duelWasRunning)
                yield return ReturnToMainMenuAfterDisconnect();
        }

        private IEnumerator ReturnToMainMenuAfterDisconnect()
        {
            yield return null;
            showPanel = true;
            if (SceneManager.GetActiveScene().name != ProjectIdentity.MainMenuScene)
                SceneManager.LoadScene(ProjectIdentity.MainMenuScene);
        }

        private void ProcessHelloMessage(
            ulong senderClientId,
            HelloPayload hello)
        {
            if (!IsHost || senderClientId != remoteClientId ||
                hello == null)
            {
                return;
            }

            if (!ValidateHello(hello, out string rejection))
            {
                RuntimeDiagnosticRecorder.Record(
                    "F08",
                    "MultiplayerCompatibility",
                    nameof(DuelOnlineSession),
                    "Remote hello payload was rejected.",
                    mode: "online-host",
                    details: rejection);
                status = rejection;
                SendToClient(senderClientId, HelloRejectedMessage,
                    new HelloRejectedPayload { reason = rejection });
                StartCoroutine(DisconnectRejectedClient(senderClientId));
                return;
            }
            remoteLoadout = hello.loadout;
            remoteDuelIdentity = NormalizeRemoteDuelIdentity(
                hello.loadout.identity,
                hello.loadout.profileId,
                hello.loadout.playerDisplayName,
                hello.rankPlayer);
            remoteRankHandshake = hello.rankPlayer;
            if (helloRequestRetry != null)
            {
                StopCoroutine(helloRequestRetry);
                helloRequestRetry = null;
            }
            status = "Deck do rival validado. Confirmando o lobby com o cliente...";
            SendToClient(senderClientId, HelloAcceptedMessage,
                new HelloAcceptedPayload
                {
                    protocolVersion = ProtocolVersion,
                    compatibility = ProjectIdentity.MultiplayerCompatibility,
                    hostPlayerDisplayName = localLoadout?.playerDisplayName ??
                        string.Empty,
                    hostDeckDisplayName = localLoadout?.displayName ?? string.Empty,
                    competitivePolicy = competitivePolicy,
                    rankPlayer = localRankHandshake ?? CaptureLocalRankSnapshot(),
                    hostIdentity = localLoadout?.identity?.Copy()
                },
                NetworkDelivery.ReliableSequenced);
        }

        private void ProcessHelloAcceptedMessage(
            ulong senderClientId,
            HelloAcceptedPayload accepted)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                accepted == null)
            {
                return;
            }
            if (accepted.protocolVersion != ProtocolVersion ||
                accepted.compatibility !=
                    ProjectIdentity.MultiplayerCompatibility ||
                accepted.competitivePolicy != competitivePolicy ||
                competitivePolicy == CompetitivePolicy.Ranked &&
                (accepted.rankPlayer == null || !accepted.rankPlayer.IsValid))
            {
                RejectIncompatibleHost();
                return;
            }

            helloAccepted = true;
            hostPlayerDisplayName = accepted.hostPlayerDisplayName ?? string.Empty;
            hostDeckDisplayName = accepted.hostDeckDisplayName ?? string.Empty;
            remoteRankHandshake = accepted.rankPlayer;
            remoteDuelIdentity = NormalizeRemoteDuelIdentity(
                accepted.hostIdentity,
                accepted.rankPlayer?.stablePlayerId,
                hostPlayerDisplayName,
                accepted.rankPlayer);
            if (helloRetry != null)
            {
                StopCoroutine(helloRetry);
                helloRetry = null;
            }
            SendClientReady(false, false);
            _ = sessionCoordinator.SetPlayerStateAsync("connected", true);
            status = "Deck validado e lobby confirmado. Aguardando o anfitriao iniciar.";
        }

        private void ProcessHelloRejectedMessage(
            ulong senderClientId,
            HelloRejectedPayload rejection)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                rejection == null)
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

        private static DuelIdentitySnapshot NormalizeRemoteDuelIdentity(
            DuelIdentitySnapshot supplied,
            string fallbackStablePlayerId,
            string fallbackNickname,
            RankPlayerSnapshot rank)
        {
            DuelIdentitySnapshot normalized = supplied?.Copy() ??
                new DuelIdentitySnapshot();
            string stablePlayerId = !string.IsNullOrWhiteSpace(
                    normalized.stablePlayerId)
                ? normalized.stablePlayerId.Trim()
                : !string.IsNullOrWhiteSpace(rank?.stablePlayerId)
                    ? rank.stablePlayerId.Trim()
                    : !string.IsNullOrWhiteSpace(fallbackStablePlayerId)
                        ? fallbackStablePlayerId.Trim()
                        : "remote-player";
            if (stablePlayerId.Length > 128)
                stablePlayerId = stablePlayerId.Substring(0, 128);

            string nickname = !string.IsNullOrWhiteSpace(normalized.nickname)
                ? normalized.nickname.Trim()
                : !string.IsNullOrWhiteSpace(fallbackNickname)
                    ? fallbackNickname.Trim()
                    : "OPONENTE";
            if (nickname.Length >
                ArcaneArena.Frontend.DeckRepository.MaximumPlayerNameLength)
            {
                nickname = nickname.Substring(
                    0,
                    ArcaneArena.Frontend.DeckRepository.MaximumPlayerNameLength);
            }

            bool suppliedKnownIcon = supplied != null &&
                !string.IsNullOrWhiteSpace(supplied.equippedIconId);
            int rankedPoints = rank != null && rank.IsValid
                ? rank.rankedPoints
                : RankRules.ClampPoints(normalized.rankedPoints);
            normalized.stablePlayerId = stablePlayerId;
            normalized.nickname = nickname;
            normalized.equippedIconId = suppliedKnownIcon
                ? ProfileIconCatalog.ResolveId(supplied.equippedIconId)
                : ProfileIconCatalog.ResolveForStableIdentity(stablePlayerId);
            normalized.rankedPoints = rankedPoints;
            normalized.rankTier = RankRules.ResolveTier(rankedPoints);
            normalized.cosmeticsCatalogVersion =
                ProfileIconCatalog.CatalogVersion;
            return normalized;
        }

        private void BeginHostMatch()
        {
            if (!IsHost || matchStarted || remoteLoadout == null ||
                !clientDeckReady || remoteClientId == ulong.MaxValue)
            {
                status = "Aguardando o cliente confirmar os dois decks.";
                return;
            }

            if (!OnlineDeckLegalityGate.TryValidate(
                    localLoadout, out string localRejection))
            {
                status = "Deck do anfitriao recusado: " + localRejection;
                return;
            }
            if (!OnlineDeckLegalityGate.TryValidate(
                    remoteLoadout, out string remoteRejection))
            {
                status = "Deck do rival recusado: " + remoteRejection;
                return;
            }

            uint[] localMain = ParseCardCodes(localLoadout?.mainDeckCardIds);
            uint[] remoteMain = ParseCardCodes(remoteLoadout.mainDeckCardIds);
            if (localMain.Length < 40 || remoteMain.Length < 40)
            {
                status = "Um dos decks nao possui 40 cartas validas. " +
                    "Escolha novamente o deck antes de iniciar.";
                return;
            }

            // Every duel in a Best-of series needs its own immutable id.
            // The tournament confrontation id remains in activeTournamentContext;
            // reusing it here would make rewards and transport acknowledgements
            // from game 1 collide with games 2/3/5.
            currentMatchId = Guid.NewGuid().ToString("N");
            if (competitivePolicy == CompetitivePolicy.Ranked &&
                !TrySealRankedMatchSnapshot(currentMatchId, out string rankError))
            {
                currentMatchId = string.Empty;
                status = rankError;
                return;
            }
            currentTransitionEpoch = ++transitionEpochCounter;
            if (currentTransitionEpoch == 0)
                currentTransitionEpoch = ++transitionEpochCounter;
            readinessBarrier.Begin(currentMatchId, currentTransitionEpoch);
            localRewardEligibilityAtMatchStart =
                CaptureLocalRewardEligibility();
            tournamentMetricsCollector = activeTournamentContext == null
                ? null
                : new TournamentDuelMetricsCollector(
                    currentMatchId,
                    activeTournamentContext.playerAId,
                    activeTournamentContext.playerBId);
            matchStarted = true;
            clientSynchronizing = true;
            hostCoreStarted = false;
            clientReceivedStart = false;
            clientArenaReady = false;
            localSceneReady = false;
            localSceneLoadRequested = false;
            beginDuelReceived = false;
            beginDuelApplied = false;
            clientBeginApplied = false;
            nextStateSequence = 0;
            authoritativeStateVersion = 0;
            authoritativePublicStateHash = 0;
            status = "Avisando o cliente e abrindo as duas arenas...";
            SetFlowState(OnlineMatchFlowState.PreparingTransition);
            loadingPresenter?.Show(
                "PREPARANDO O DUELO",
                "Os dois decks foram validados.");
            _ = sessionCoordinator.SetHostMatchStateAsync(
                "starting",
                currentMatchId,
                false);
            showPanel = false;
            if (diagnosticPreludeBypass || Application.isBatchMode)
            {
                onlineStartingPlayer = 0;
                onlinePreludeResolved = true;
                StartHostStartHandshake();
                StartArenaTransitionAfterBlack();
                return;
            }
            BeginOnlinePreludeRound();
        }

        private void BeginOnlinePreludeRound()
        {
            if (!IsHost || !matchStarted ||
                remoteClientId == ulong.MaxValue)
            {
                return;
            }

            onlinePreludeRound++;
            hostPreludeChoice = DuelPreludeChoice.None;
            clientPreludeChoice = DuelPreludeChoice.None;
            onlinePreludeResolved = false;
            status = "Escolha pedra, papel ou tesoura para definir quem inicia.";
            loadingPresenter?.ShowRockPaperScissors(
                remoteLoadout?.displayName ?? "RIVAL",
                onlinePreludeRound,
                choice =>
                {
                    if (!DuelPreludeRules.IsPlayable(choice))
                        return;
                    hostPreludeChoice = choice;
                    loadingPresenter?.ShowRockPaperScissorsWaiting(
                        "Escolha confirmada · aguardando o rival...");
                    TryResolveOnlinePrelude();
                });
            SendToClient(
                remoteClientId,
                PreludeMessage,
                new PreludePayload
                {
                    protocolVersion = ProtocolVersion,
                    matchId = currentMatchId,
                    transitionEpoch = currentTransitionEpoch,
                    round = onlinePreludeRound
                },
                NetworkDelivery.ReliableSequenced);
        }

        private void ProcessPreludeMessage(
            ulong senderClientId,
            PreludePayload prelude)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                prelude == null ||
                prelude.protocolVersion != ProtocolVersion ||
                string.IsNullOrWhiteSpace(prelude.matchId) ||
                prelude.transitionEpoch == 0 || prelude.round <= 0)
            {
                return;
            }
            if (matchStarted &&
                (!MatchIdsAreCompatible(currentMatchId, prelude.matchId) ||
                 currentTransitionEpoch != 0 &&
                 currentTransitionEpoch != prelude.transitionEpoch))
            {
                return;
            }

            matchStarted = true;
            clientSynchronizing = true;
            currentMatchId = prelude.matchId;
            currentTransitionEpoch = prelude.transitionEpoch;
            onlinePreludeRound = prelude.round;
            hostPreludeChoice = DuelPreludeChoice.None;
            clientPreludeChoice = DuelPreludeChoice.None;
            onlinePreludeResolved = false;
            localRewardEligibilityAtMatchStart ??=
                CaptureLocalRewardEligibility();
            PersistReconnectTicket();
            showPanel = false;
            status = "Escolha pedra, papel ou tesoura para definir quem inicia.";
            loadingPresenter?.ShowRockPaperScissors(
                hostPlayerDisplayName,
                onlinePreludeRound,
                choice =>
                {
                    if (!DuelPreludeRules.IsPlayable(choice))
                        return;
                    clientPreludeChoice = choice;
                    loadingPresenter?.ShowRockPaperScissorsWaiting(
                        "Escolha enviada · aguardando o resultado do host...");
                    SendToServer(
                        PreludeChoiceMessage,
                        new PreludeChoicePayload
                        {
                            protocolVersion = ProtocolVersion,
                            matchId = currentMatchId,
                            transitionEpoch = currentTransitionEpoch,
                            round = onlinePreludeRound,
                            choice = (int)choice
                        },
                        NetworkDelivery.ReliableSequenced);
                });
        }

        private void ProcessPreludeChoiceMessage(
            ulong senderClientId,
            PreludeChoicePayload choice)
        {
            if (!IsHost || senderClientId != remoteClientId ||
                choice == null || choice.protocolVersion != ProtocolVersion ||
                choice.matchId != currentMatchId ||
                choice.transitionEpoch != currentTransitionEpoch ||
                choice.round != onlinePreludeRound)
            {
                return;
            }
            DuelPreludeChoice value = (DuelPreludeChoice)choice.choice;
            if (!DuelPreludeRules.IsPlayable(value))
                return;
            clientPreludeChoice = value;
            TryResolveOnlinePrelude();
        }

        private void TryResolveOnlinePrelude()
        {
            if (!IsHost || onlinePreludeResolved ||
                !DuelPreludeRules.IsPlayable(hostPreludeChoice) ||
                !DuelPreludeRules.IsPlayable(clientPreludeChoice))
            {
                return;
            }

            DuelPreludeOutcome outcome = DuelPreludeRules.Resolve(
                hostPreludeChoice,
                clientPreludeChoice);
            bool tie = outcome == DuelPreludeOutcome.Tie;
            int winnerSeat = tie
                ? -1
                : outcome == DuelPreludeOutcome.PlayerOne ? 0 : 1;
            onlinePreludeResolved = true;
            SendToClient(
                remoteClientId,
                PreludeResultMessage,
                new PreludeResultPayload
                {
                    protocolVersion = ProtocolVersion,
                    matchId = currentMatchId,
                    transitionEpoch = currentTransitionEpoch,
                    round = onlinePreludeRound,
                    hostChoice = (int)hostPreludeChoice,
                    clientChoice = (int)clientPreludeChoice,
                    winnerSeat = winnerSeat,
                    tie = tie
                },
                NetworkDelivery.ReliableSequenced);
            loadingPresenter?.ShowRockPaperScissorsResult(
                hostPreludeChoice,
                clientPreludeChoice,
                winnerSeat == 0,
                tie);
            if (!tie)
                onlineStartingPlayer = (byte)winnerSeat;
            if (preludeResultRoutine != null)
                StopCoroutine(preludeResultRoutine);
            preludeResultRoutine = StartCoroutine(
                ContinueAfterOnlinePreludeResult(tie));
        }

        private void ProcessPreludeResultMessage(
            ulong senderClientId,
            PreludeResultPayload result)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                result == null || result.protocolVersion != ProtocolVersion ||
                result.matchId != currentMatchId ||
                result.transitionEpoch != currentTransitionEpoch ||
                result.round != onlinePreludeRound)
            {
                return;
            }

            hostPreludeChoice = (DuelPreludeChoice)result.hostChoice;
            clientPreludeChoice = (DuelPreludeChoice)result.clientChoice;
            if (!DuelPreludeRules.IsPlayable(hostPreludeChoice) ||
                !DuelPreludeRules.IsPlayable(clientPreludeChoice))
            {
                return;
            }
            onlinePreludeResolved = true;
            if (!result.tie && (result.winnerSeat == 0 || result.winnerSeat == 1))
                onlineStartingPlayer = (byte)result.winnerSeat;
            loadingPresenter?.ShowRockPaperScissorsResult(
                clientPreludeChoice,
                hostPreludeChoice,
                result.winnerSeat == 1,
                result.tie);
            status = result.tie
                ? "Empate. O host iniciará uma nova rodada."
                : result.winnerSeat == 1
                    ? "Você venceu a escolha e iniciará o duelo."
                    : "O anfitrião venceu a escolha e iniciará o duelo.";
        }

        private IEnumerator ContinueAfterOnlinePreludeResult(bool tie)
        {
            yield return new WaitForSecondsRealtime(tie ? 0.82f : 1.05f);
            preludeResultRoutine = null;
            if (!IsHost || !matchStarted)
                yield break;
            if (tie)
            {
                BeginOnlinePreludeRound();
                yield break;
            }

            loadingPresenter?.ShowDuelLoading(
                "PREPARANDO O DUELO",
                "Abrindo os dois campos simultaneamente.",
                0.10f);
            status = "Avisando o cliente e abrindo as duas arenas...";
            StartHostStartHandshake();
            StartArenaTransitionAfterBlack();
        }

        private void ProcessStartMessage(
            ulong senderClientId,
            StartPayload start)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                start == null)
            {
                return;
            }
            if (start.protocolVersion != ProtocolVersion ||
                start.compatibility !=
                    ProjectIdentity.MultiplayerCompatibility ||
                start.transitionEpoch == 0)
            {
                RejectIncompatibleHost();
                return;
            }

            if (start.rankedMatch != null)
            {
                string rankRejection =
                    "O snapshot ranqueado não pertence a esta partida.";
                if (!string.Equals(
                        start.rankedMatch.matchId,
                        start.matchId,
                        StringComparison.Ordinal) ||
                    !TryAcceptRankedMatchSnapshot(start.rankedMatch,
                        out rankRejection))
                {
                    ResetAfterFailedConnection(rankRejection);
                    showPanel = true;
                    return;
                }
            }
            else if (competitivePolicy == CompetitivePolicy.Ranked)
            {
                ResetAfterFailedConnection(
                    "O anfitrião não enviou o snapshot da partida ranqueada.");
                showPanel = true;
                return;
            }

            if (matchStarted &&
                (!MatchIdsAreCompatible(currentMatchId, start.matchId) ||
                 currentTransitionEpoch != 0 &&
                 currentTransitionEpoch != start.transitionEpoch))
            {
                return;
            }

            helloAccepted = true;
            clientSynchronizing = true;
            if (start.hostIdentity != null)
            {
                remoteDuelIdentity = NormalizeRemoteDuelIdentity(
                    start.hostIdentity,
                    start.hostIdentity.stablePlayerId,
                    hostPlayerDisplayName,
                    remoteRankHandshake);
            }
            if (string.IsNullOrWhiteSpace(currentMatchId))
                currentMatchId = start.matchId ?? string.Empty;
            currentTransitionEpoch = start.transitionEpoch;
            if (start.duelAlreadyBegun)
            {
                beginDuelReceived = true;
                beginDuelApplied = true;
            }
            if (!matchStarted)
            {
                localRewardEligibilityAtMatchStart =
                    CaptureLocalRewardEligibility();
            }
            PersistReconnectTicket();
            if (helloRetry != null)
            {
                StopCoroutine(helloRetry);
                helloRetry = null;
            }
            bool arenaIsReady = replicaController != null &&
                                SceneManager.GetActiveScene().name == DuelArenaScene;
            SetFlowState(OnlineMatchFlowState.PreparingTransition);
            loadingPresenter?.ShowDuelLoading(
                arenaIsReady ? "Sincronizando partida..." : "Carregando duelo...",
                arenaIsReady
                    ? "Validando o snapshot inicial do anfitrião."
                    : "Preparando o campo online.",
                arenaIsReady ? 0.48f : 0.10f);
            SendClientReady(true, arenaIsReady);
            if (matchStarted)
            {
                localSceneReady = arenaIsReady;
                localSceneLoadRequested = arenaIsReady;
                if (!arenaIsReady)
                    StartArenaTransitionAfterBlack();
                return;
            }

            matchStarted = true;
            status = "Decks validados. Abrindo a arena...";
            showPanel = false;
            localSceneReady = arenaIsReady;
            localSceneLoadRequested = arenaIsReady;
            StartArenaTransitionAfterBlack();
        }

        private void SendClientReady(bool startReceived, bool arenaReady)
        {
            if (role != SessionRole.Client || networkManager == null ||
                !networkManager.IsConnectedClient)
            {
                return;
            }

            SendToServer(ClientReadyMessage, new ClientReadyPayload
            {
                protocolVersion = ProtocolVersion,
                compatibility = ProjectIdentity.MultiplayerCompatibility,
                matchId = currentMatchId,
                transitionEpoch = currentTransitionEpoch,
                deckReady = helloAccepted,
                startReceived = startReceived,
                arenaReady = arenaReady,
                beginApplied = beginDuelApplied,
                rankPlayer = CaptureLocalRankSnapshot()
            }, NetworkDelivery.ReliableSequenced);
        }

        private void ProcessClientReadyMessage(
            ulong senderClientId,
            ClientReadyPayload ready)
        {
            if (!IsHost || senderClientId != remoteClientId ||
                ready == null ||
                ready.protocolVersion != ProtocolVersion ||
                ready.compatibility !=
                    ProjectIdentity.MultiplayerCompatibility ||
                !MatchIdsAreCompatible(currentMatchId, ready.matchId) ||
                ready.transitionEpoch != currentTransitionEpoch)
            {
                return;
            }

            if (competitivePolicy == CompetitivePolicy.Ranked)
            {
                if (ready.rankPlayer == null || !ready.rankPlayer.IsValid ||
                    !string.Equals(
                        ready.rankPlayer.stablePlayerId,
                        remoteLoadout?.profileId,
                        StringComparison.Ordinal))
                {
                    clientDeckReady = false;
                    status = "O snapshot ranqueado do rival é inválido ou incompatível.";
                    return;
                }
                remoteRankHandshake = ready.rankPlayer;
            }

            clientDeckReady |= ready.deckReady;
            clientReceivedStart |= ready.startReceived;
            clientArenaReady |= ready.arenaReady;
            clientBeginApplied |= ready.beginApplied;
            if (ready.arenaReady)
            {
                readinessBarrier.RegisterSceneReady(
                    currentMatchId,
                    currentTransitionEpoch,
                    1);
                loadingPresenter?.SetProgress(0.62f);
            }

            if (!matchStarted && clientDeckReady)
            {
                if (activeTournamentContext != null ||
                    automaticRankedMatchmaking)
                {
                    status = "Os dois decks foram confirmados. Iniciando o confronto...";
                    BeginHostMatch();
                    return;
                }
                status = "Os dois decks foram confirmados. O anfitriao pode iniciar.";
                return;
            }
            if (!matchStarted)
                return;

            if (clientArenaReady)
            {
                if (startRetry != null)
                {
                    StopCoroutine(startRetry);
                    startRetry = null;
                }
                if (hostCoreStarted)
                {
                    clientSynchronizing = true;
                    hostAwaitingStateAckUnlock = true;
                    BroadcastState();
                    status = beginDuelApplied
                        ? "Rival reconectado. Aguardando o snapshot de retomada."
                        : "Snapshot inicial enviado. Aguardando confirmação do cliente.";
                }
                else
                {
                    status = "As duas arenas estão prontas. Preparando o duelo.";
                    TryStartHostDuel();
                }
                showPanel = false;
            }
            else if (clientReceivedStart)
            {
                status = "Cliente confirmou o inicio. Aguardando a arena dele carregar...";
            }
        }

        private static bool MatchIdsAreCompatible(string expected, string received)
        {
            // If the remote host is an immediately previous v2 build, both
            // ids remain empty. Once this build announces a match id, packets
            // without that exact id are stale and must not enter a new duel.
            return string.IsNullOrWhiteSpace(expected) ||
                   !string.IsNullOrWhiteSpace(received) &&
                   string.Equals(expected, received, StringComparison.Ordinal);
        }

        private void RejectIncompatibleHost()
        {
            const string reason =
                "A sala usa conteúdo incompatível. Instale a mesma versão " +
                "ONLINE v11 no PC e no celular e crie um novo código.";
            ResetAfterFailedConnection(reason);
            showPanel = true;
        }

        private void StartHostStartHandshake()
        {
            if (!IsHost || !matchStarted || remoteClientId == ulong.MaxValue)
                return;

            if (startRetry != null)
                StopCoroutine(startRetry);
            startRetry = StartCoroutine(SendStartUntilClientArenaReady());
        }

        private IEnumerator SendStartUntilClientArenaReady()
        {
            int attempts = 0;
            while (attempts < MaximumStartAttempts && IsHost &&
                   matchStarted && !clientArenaReady &&
                   remoteClientId != ulong.MaxValue &&
                   networkManager != null && networkManager.IsServer)
            {
                attempts++;
                SendToClient(remoteClientId, StartMessage, new StartPayload
                {
                    protocolVersion = ProtocolVersion,
                    compatibility = ProjectIdentity.MultiplayerCompatibility,
                    matchId = currentMatchId,
                    transitionEpoch = currentTransitionEpoch,
                    duelAlreadyBegun = beginDuelApplied,
                    rankedMatch = sealedRankedMatch,
                    hostIdentity = localLoadout?.identity?.Copy()
                }, NetworkDelivery.ReliableSequenced);

                if (clientReceivedStart)
                {
                    status = "Cliente recebeu o inicio. Aguardando a arena dele carregar...";
                }
                else if (attempts > 1)
                {
                    status = $"Confirmando o inicio com o cliente ({attempts})...";
                }
                yield return new WaitForSecondsRealtime(StartRetrySeconds);
            }

            if (!clientArenaReady && IsHost && matchStarted &&
                remoteClientId != ulong.MaxValue)
            {
                status = "A arena do cliente ainda nao confirmou. " +
                    "A conexao permanece aberta aguardando a confirmacao.";
            }
            startRetry = null;
        }

        private void ProcessStateMessage(
            ulong senderClientId,
            DuelNetworkState networkState)
        {
            ulong computedHash = DuelNetworkProtocol
                .ComputePublicProjectionHash(networkState);
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                networkState == null ||
                !MatchIdsAreCompatible(currentMatchId, networkState.matchId) ||
                networkState.sequence <= lastReplicaSequence ||
                networkState.stateVersion == 0)
            {
                return;
            }
            if (computedHash == 0 ||
                computedHash != networkState.publicStateHash)
            {
                Debug.LogWarning(
                    $"[MP] stage=state-hash-rejected " +
                    $"sequence={networkState.sequence} " +
                    $"version={networkState.stateVersion} " +
                    $"expected={networkState.publicStateHash:x16} " +
                    $"computed={computedHash:x16} " +
                    $"recipient={networkState.recipientSeat}");
                RequestResync("public-hash-mismatch");
                return;
            }
            if (networkState.stateVersion < lastReplicaStateVersion ||
                networkState.stateVersion == lastReplicaStateVersion &&
                lastReplicaPublicStateHash != 0 &&
                lastReplicaPublicStateHash != networkState.publicStateHash)
            {
                RequestResync("state-version-rollback");
                return;
            }
            if (string.IsNullOrWhiteSpace(currentMatchId) &&
                !string.IsNullOrWhiteSpace(networkState.matchId))
            {
                currentMatchId = networkState.matchId;
            }
            lastReplicaSequence = networkState.sequence;
            lastReplicaStateVersion = networkState.stateVersion;
            lastReplicaPublicStateHash = networkState.publicStateHash;
            nextClientSequence = Math.Max(
                nextClientSequence,
                networkState.lastAcceptedClientSequence);
            nextClientCommandId = Math.Max(
                nextClientCommandId,
                networkState.acknowledgedCommandId);
            PersistReconnectTicket();
            pendingReplicaState = networkState;
            if (!beginDuelApplied)
            {
                SetFlowState(OnlineMatchFlowState.WaitingSnapshotAck);
                loadingPresenter?.SetProgress(0.86f);
                loadingPresenter?.SetText(
                    "Sincronizando partida...",
                    "Snapshot recebido. Confirmando o estado inicial.");
            }
            else if (clientSynchronizing)
            {
                loadingPresenter?.Show(
                    "Ressincronizando duelo...",
                    "Aplicando o estado confirmado pelo anfitrião.");
            }
            if (replicaController == null)
                return;
            if (!ApplyReplicaState(networkState))
            {
                RequestResync("snapshot-apply-failed");
                return;
            }
            SendStateAck(networkState);
            if (beginDuelApplied &&
                flowState != OnlineMatchFlowState.ResultScreen)
            {
                SetFlowState(OnlineMatchFlowState.InDuel);
                DuelOnlineBridge.CompleteOnlineArenaTransition();
                loadingPresenter?.Hide();
            }
            TryApplyPendingClientResult();
        }

        private void TryApplyPendingClientResult()
        {
            if (role != SessionRole.Client || lastAuthoritativeResult == null ||
                lastAuthoritativeResult.resultSequence <=
                    lastAppliedResultSequence ||
                lastAuthoritativeResult.finalStateVersion >
                    lastReplicaStateVersion)
            {
                return;
            }
            ApplyClientAuthoritativeResult(lastAuthoritativeResult);
        }

        private void SendStateAck(DuelNetworkState state)
        {
            if (role != SessionRole.Client || state == null ||
                !networkManager.IsConnectedClient)
            {
                return;
            }
            lastStateAckVersion = state.stateVersion;
            SendToServer(StateAckMessage, new StateAckPayload
            {
                protocolVersion = ProtocolVersion,
                matchId = currentMatchId,
                transitionEpoch = currentTransitionEpoch,
                stateVersion = state.stateVersion,
                publicStateHash = state.publicStateHash,
                lastAcceptedClientSequence =
                    state.lastAcceptedClientSequence
            });
        }

        private void ProcessStateAckMessage(
            ulong senderClientId,
            StateAckPayload acknowledgement)
        {
            if (!IsHost || senderClientId != remoteClientId ||
                acknowledgement == null ||
                acknowledgement.protocolVersion != ProtocolVersion ||
                acknowledgement.matchId != currentMatchId ||
                acknowledgement.transitionEpoch != currentTransitionEpoch ||
                acknowledgement.stateVersion != authoritativeStateVersion ||
                acknowledgement.publicStateHash !=
                    authoritativePublicStateHash ||
                acknowledgement.lastAcceptedClientSequence !=
                    lastAcceptedClientSequence)
            {
                return;
            }

            readinessBarrier.RegisterSnapshotApplied(
                currentMatchId,
                currentTransitionEpoch,
                1,
                acknowledgement.stateVersion);
            if (!beginDuelApplied)
                loadingPresenter?.SetProgress(0.94f);
            if (beginDuelApplied && hostAwaitingLiveStateAck)
            {
                hostAwaitingLiveStateAck = false;
                clientSynchronizing = false;
                status = "Estado do rival confirmado. Duelo online sincronizado.";
                return;
            }
            if (beginDuelApplied && hostAwaitingStateAckUnlock)
            {
                hostAwaitingStateAckUnlock = false;
                clientSynchronizing = false;
                hostController?.SetPresentationDecisionLocked(false);
                loadingPresenter?.Hide();
                status = "Rival ressincronizado. Duelo online retomado.";
                return;
            }
            Debug.Log("[MP] stage=state-ack version=" +
                acknowledgement.stateVersion);
            TryIssueBeginDuel();
        }

        private void TryIssueBeginDuel()
        {
            if (!IsHost || networkManager == null ||
                !networkManager.IsServer || !hostCoreStarted ||
                remoteClientId == ulong.MaxValue ||
                !readinessBarrier.TryIssueBegin())
            {
                return;
            }

            long leadTicks = Math.Max(
                1,
                Mathf.CeilToInt(flowConfig.StartLeadSeconds * NetworkTickRate));
            var begin = new BeginDuelPayload
            {
                protocolVersion = ProtocolVersion,
                matchId = currentMatchId,
                transitionEpoch = currentTransitionEpoch,
                initialStateVersion = authoritativeStateVersion,
                serverStartTick = networkManager.ServerTime.Tick + leadTicks
            };
            beginDuelReceived = true;
            loadingPresenter?.SetProgress(0.97f);
            SendToClient(
                remoteClientId,
                BeginDuelMessage,
                begin,
                NetworkDelivery.ReliableSequenced);
            StartBeginDuelAtTick(begin);
            Debug.Log(
                $"[MP] stage=begin-issued epoch={currentTransitionEpoch} " +
                $"state={authoritativeStateVersion} tick={begin.serverStartTick}");
        }

        private void ProcessBeginDuelMessage(
            ulong senderClientId,
            BeginDuelPayload begin)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                begin == null || begin.protocolVersion != ProtocolVersion ||
                begin.matchId != currentMatchId ||
                begin.transitionEpoch != currentTransitionEpoch ||
                begin.initialStateVersion == 0 ||
                begin.initialStateVersion != lastReplicaStateVersion)
            {
                return;
            }

            if (beginDuelReceived)
            {
                if (beginDuelApplied)
                    SendClientReady(true, localSceneReady);
                return;
            }

            beginDuelReceived = true;
            loadingPresenter?.SetProgress(0.97f);
            StartBeginDuelAtTick(begin);
            Debug.Log(
                $"[MP] stage=begin-received epoch={currentTransitionEpoch} " +
                $"state={begin.initialStateVersion} tick={begin.serverStartTick}");
        }

        private void StartBeginDuelAtTick(BeginDuelPayload begin)
        {
            if (beginDuelRoutine != null)
                StopCoroutine(beginDuelRoutine);
            beginDuelRoutine = StartCoroutine(ApplyBeginDuelAtTick(begin));
        }

        private IEnumerator ApplyBeginDuelAtTick(BeginDuelPayload begin)
        {
            while (networkManager != null && IsOnlineDuelActive &&
                   networkManager.ServerTime.Tick < begin.serverStartTick)
            {
                yield return null;
            }

            beginDuelRoutine = null;
            if (!IsOnlineDuelActive || begin == null ||
                begin.matchId != currentMatchId ||
                begin.transitionEpoch != currentTransitionEpoch ||
                beginDuelApplied)
            {
                yield break;
            }

            beginDuelApplied = true;
            clientSynchronizing = false;
            hostAwaitingStateAckUnlock = false;
            hostController?.SetPresentationDecisionLocked(false);
            replicaController?.SetPresentationDecisionLocked(false);
            CardArenaBootstrap openingArena = IsHost
                ? hostController?.GetComponent<CardArenaBootstrap>()
                : replicaController?.GetComponent<CardArenaBootstrap>();
            openingArena?.StartOpeningDuelPresentation();
            SetFlowState(OnlineMatchFlowState.InDuel);
            DuelOnlineBridge.CompleteOnlineArenaTransition();
            loadingPresenter?.SetProgress(1f);
            loadingPresenter?.Hide();
            status = "Duelo online ativo. Os dois jogadores estão sincronizados.";
            if (IsHost)
            {
                _ = sessionCoordinator.SetHostMatchStateAsync(
                    "in-match",
                    currentMatchId,
                    false);
            }
            else
            {
                SendClientReady(true, localSceneReady);
            }
            Debug.Log(
                $"[MP] stage=begin-applied epoch={currentTransitionEpoch} " +
                $"tick={begin.serverStartTick} role={role}");
        }

        private void RequestResync(string reason)
        {
            if (role != SessionRole.Client || networkManager == null ||
                !networkManager.IsConnectedClient ||
                Time.realtimeSinceStartup < nextClientResyncTime)
            {
                return;
            }

            nextClientResyncTime =
                Time.realtimeSinceStartup + ResyncCooldownSeconds;
            clientSynchronizing = true;
            replicaController?.SetPresentationDecisionLocked(true);
            SendToServer(ResyncRequestMessage, new ResyncRequestPayload
            {
                protocolVersion = ProtocolVersion,
                matchId = currentMatchId,
                lastStateVersion = lastReplicaStateVersion,
                reason = reason ?? "client-request"
            });
            RuntimeDiagnosticRecorder.Record(
                "F08",
                "MultiplayerState",
                nameof(DuelOnlineSession),
                "The client requested an authoritative state repair.",
                RuntimeDiagnosticSeverity.Warning,
                mode: "online-client",
                details: $"reason={reason}; " +
                         $"stateVersion={lastReplicaStateVersion}; " +
                         $"sequence={lastReplicaSequence}");
            Debug.LogWarning("[MP] stage=resync-request reason=" + reason);
        }

        private void ProcessResyncRequestMessage(
            ulong senderClientId,
            ResyncRequestPayload request)
        {
            if (!IsHost || senderClientId != remoteClientId ||
                request == null || request.protocolVersion != ProtocolVersion ||
                request.matchId != currentMatchId ||
                Time.realtimeSinceStartup < nextHostResyncTime)
            {
                return;
            }

            nextHostResyncTime =
                Time.realtimeSinceStartup + ResyncCooldownSeconds;
            BeginHostLiveStateRepair(request.reason);
            Debug.LogWarning("[MP] stage=resync-send reason=" + request.reason);
            BroadcastState();
        }

        private void SendAuthoritativeResync(string reason)
        {
            if (!IsHost || Time.realtimeSinceStartup < nextHostResyncTime)
                return;
            nextHostResyncTime =
                Time.realtimeSinceStartup + ResyncCooldownSeconds;
            BeginHostLiveStateRepair(reason);
            Debug.LogWarning("[MP] stage=resync-send reason=" + reason);
            BroadcastState();
        }

        private void BeginHostLiveStateRepair(string reason)
        {
            clientSynchronizing = true;
            hostAwaitingLiveStateAck =
                beginDuelApplied &&
                !hostAwaitingReconnect &&
                !reconnecting;
            RuntimeDiagnosticRecorder.Record(
                "F08",
                "MultiplayerState",
                nameof(DuelOnlineSession),
                "The host started an authoritative mid-duel state repair.",
                RuntimeDiagnosticSeverity.Warning,
                mode: "online-host",
                details:
                    $"reason={reason ?? "host-request"}; " +
                    $"stateVersion={authoritativeStateVersion}; " +
                    $"prompt={hostController?.CurrentPrompt?.RequestId ?? 0}; " +
                    $"presentationLocked={hostController?.PresentationDecisionLocked == true}; " +
                    $"outboundTransfers={outboundWireTransfers.Count}");
        }

        private void ProcessResponseMessage(
            ulong senderClientId,
            ResponsePayload response)
        {
            if (!IsHost || senderClientId != remoteClientId || response == null)
            {
                return;
            }
            if (!ConsumeCommandToken())
            {
                Debug.LogWarning("[MP] stage=command-rejected reason=rate-limit");
                return;
            }
            if (!TryDecodeResponseBytes(response, out byte[] bytes))
                return;

            ulong payloadHash = DuelWireProtocol.ComputePayloadChecksum(bytes);
            if (response.commandId == lastAcknowledgedCommandId &&
                response.clientSequence == lastAcceptedClientSequence)
            {
                if (response.requestId == lastAcknowledgedResponseRequestId &&
                    payloadHash == lastAcceptedCommandPayloadHash)
                {
                    // Exact retry after a lost state ACK: return the current
                    // authoritative snapshot without applying the command.
                    BroadcastState();
                }
                else
                {
                    DisconnectProtocolViolation(
                        "command-id-reused-with-different-payload");
                }
                return;
            }
            if (!ValidateCommandEnvelope(response))
            {
                SendAuthoritativeResync("invalid-command-envelope");
                return;
            }
            if (!TryValidateRemoteResponse(response, bytes))
            {
                SendAuthoritativeResync("prompt-validation-failed");
                return;
            }
            CommitHostResponse(response, bytes);
        }

        private void CommitHostResponse(
            ResponsePayload command,
            byte[] response)
        {
            if (hostController == null || response == null ||
                response.Length == 0 ||
                command == null ||
                !hostController.SubmitAuthoritativeNetworkResponse(
                    response,
                    command.requestId))
            {
                SendAuthoritativeResync("core-rejected-command");
                return;
            }

            lastAcknowledgedResponseRequestId = command.requestId;
            lastAcknowledgedCommandId = command.commandId;
            lastAcceptedClientSequence = command.clientSequence;
            lastAcceptedCommandPayloadHash =
                DuelWireProtocol.ComputePayloadChecksum(response);
            if (hostAwaitingLiveStateAck)
            {
                // The response itself proves that the client received a
                // current prompt/state projection. Do not keep the host in a
                // synchronization state while the acknowledgement snapshot is
                // travelling back to the client.
                hostAwaitingLiveStateAck = false;
                clientSynchronizing = false;
            }
            BroadcastState();
        }

        private void ProcessPresentationEventMessage(
            ulong senderClientId,
            DuelNetworkPresentationEvent presentationEvent)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                presentationEvent == null ||
                !MatchIdsAreCompatible(
                    currentMatchId,
                    presentationEvent.matchId) ||
                presentationEvent.eventSequence <=
                    lastPresentationEventSequence)
            {
                return;
            }

            pendingPresentationEvents[
                presentationEvent.eventSequence] = presentationEvent;
            DrainPresentationEvents();
        }

        private void TryStartHostDuel()
        {
            if (!IsHost || !matchStarted || hostCoreStarted ||
                hostController == null ||
                localLoadout == null || remoteLoadout == null ||
                !clientDeckReady || !readinessBarrier.BothScenesReady)
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
                SetFlowState(OnlineMatchFlowState.Synchronizing);
                loadingPresenter?.SetProgress(0.70f);
                loadingPresenter?.SetText(
                    "Sincronizando partida...",
                    "Preparando o snapshot inicial de cada jogador.");
                hostController.ConfigureRemotePlayerOneAuthority(true);
                if (!hostController.RestartExternalDuel(
                        localMain,
                        localExtra,
                        remoteMain,
                        remoteExtra,
                        onlineStartingPlayer))
                {
                    throw new InvalidOperationException(
                        "O ygopro-core não confirmou o início do duelo online.");
                }
                hostCoreStarted = true;
                hostAwaitingStateAckUnlock = true;
                hostController.SetPresentationDecisionLocked(true);
                status = "Motor preparado. Aguardando o snapshot do cliente.";
                showPanel = false;
                BroadcastState();
                readinessBarrier.SetInitialStateVersion(
                    currentMatchId,
                    currentTransitionEpoch,
                    authoritativeStateVersion);
                readinessBarrier.RegisterSnapshotApplied(
                    currentMatchId,
                    currentTransitionEpoch,
                    0,
                    authoritativeStateVersion);
                SetFlowState(OnlineMatchFlowState.WaitingSnapshotAck);
                loadingPresenter?.SetText(
                    "Sincronizando partida...",
                    "Aguardando o outro jogador aplicar o snapshot.");
                StartStateHeartbeat();
            }
            catch (Exception exception)
            {
                status = $"Falha ao iniciar o duelo online: {exception.GetBaseException().Message}";
                RuntimeDiagnosticRecorder.Record(
                    "F08",
                    "Multiplayer",
                    nameof(DuelOnlineSession),
                    "Authoritative online duel failed to start.",
                    RuntimeDiagnosticSeverity.Critical,
                    mode: "online-host",
                    exception: exception);
                Debug.LogException(exception);
            }
        }

        private void OnHostCoreEvent(DuelEvent duelEvent)
        {
            tournamentMetricsCollector?.Capture(duelEvent);
            TrackAndFinalizeOnlineReward(duelEvent);
            if (duelEvent != null && IsHost && matchStarted &&
                remoteClientId != ulong.MaxValue)
            {
                DuelNetworkPresentationEvent presentationEvent =
                    DuelNetworkProtocol.CreatePresentationEvent(
                        duelEvent,
                        hostController?.PresentationState,
                        1,
                        ++nextPresentationEventSequence,
                        nextStateSequence + 1,
                        currentMatchId);
                outgoingPresentationEvents.Enqueue(presentationEvent);
                TrySendPendingPresentationEvents();
            }
            if (!matchStarted || pendingStateBroadcast != null)
                return;
            pendingStateBroadcast = StartCoroutine(
                BroadcastLatestStateAtEndOfFrame());
        }

        private void TrackAndFinalizeOnlineReward(DuelEvent duelEvent)
        {
            if (duelEvent == null || !IsHost || !matchStarted ||
                string.IsNullOrWhiteSpace(currentMatchId))
            {
                return;
            }

            if (duelEvent.Message == CoreMessage.Damage)
            {
                int damage = (int)Math.Min(
                    (uint)OnlineDuelCoinReward.MaximumDamage,
                    duelEvent.Value);
                if (duelEvent.Player == 1)
                {
                    hostRewardDamage = Math.Min(
                        OnlineDuelCoinReward.MaximumDamage,
                        hostRewardDamage + damage);
                    hostStatisticsDamageDealt = SaturatingDamageAdd(
                        hostStatisticsDamageDealt,
                        duelEvent.Value);
                    clientStatisticsDamageReceived = SaturatingDamageAdd(
                        clientStatisticsDamageReceived,
                        duelEvent.Value);
                }
                else if (duelEvent.Player == 0)
                {
                    clientRewardDamage = Math.Min(
                        OnlineDuelCoinReward.MaximumDamage,
                        clientRewardDamage + damage);
                    clientStatisticsDamageDealt = SaturatingDamageAdd(
                        clientStatisticsDamageDealt,
                        duelEvent.Value);
                    hostStatisticsDamageReceived = SaturatingDamageAdd(
                        hostStatisticsDamageReceived,
                        duelEvent.Value);
                }
            }
            else if (duelEvent.Message == CoreMessage.NewTurn)
            {
                if (currentRewardTurnPlayer == 0)
                    rewardPlayerZeroTurnEnded = true;
                else if (currentRewardTurnPlayer == 1)
                    rewardPlayerOneTurnEnded = true;

                if (rewardPlayerZeroTurnEnded && rewardPlayerOneTurnEnded)
                {
                    completedRewardRounds++;
                    rewardPlayerZeroTurnEnded = false;
                    rewardPlayerOneTurnEnded = false;
                }
                currentRewardTurnPlayer = duelEvent.Player;
            }

            if (duelEvent.Message != CoreMessage.Win ||
                matchRewardFinalized || pendingTerminalEvent != null)
            {
                return;
            }

            pendingTerminalEvent = duelEvent;
            SetFlowState(OnlineMatchFlowState.DuelFinished);
            hostController?.SetPresentationDecisionLocked(true);
            status = "Duelo finalizado. Confirmando o resultado autoritativo...";
        }

        private static long SaturatingDamageAdd(long current, uint amount)
        {
            long remaining = long.MaxValue - Math.Max(0L, current);
            return amount >= (ulong)remaining
                ? long.MaxValue
                : Math.Max(0L, current) + amount;
        }

        private void FinalizePendingAuthoritativeResult()
        {
            DuelEvent duelEvent = pendingTerminalEvent;
            if (duelEvent == null || matchRewardFinalized || !IsHost ||
                string.IsNullOrWhiteSpace(currentMatchId))
            {
                return;
            }

            pendingTerminalEvent = null;
            matchRewardFinalized = true;
            bool draw = duelEvent.Player > 1;
            int winnerSeat = draw ? -1 : duelEvent.Player;
            int loserSeat = draw ? -1 : 1 - winnerSeat;
            bool hostWon = winnerSeat == 0;
            bool clientWon = winnerSeat == 1;
            GameFrontendBootstrap frontend = GameFrontendBootstrap.Instance;
            RewardReceipt hostReceipt = null;
            string rewardError = "frontend indisponível";
            if (frontend == null || !frontend.TryApplyOnlineDuelReward(
                    currentMatchId,
                    "seat0",
                    hostRewardDamage,
                    hostStatisticsDamageDealt,
                    hostStatisticsDamageReceived,
                    completedRewardRounds,
                    hostWon,
                    draw,
                    localRewardEligibilityAtMatchStart,
                    out hostReceipt,
                    out rewardError))
            {
                Debug.LogWarning(
                    "[Arcane Duel Online] Recompensa local não salva: " +
                    (rewardError ?? "frontend indisponível"));
            }

            string hostDetail = hostReceipt != null
                ? FormatRewardStatus(hostReceipt, draw)
                : "Resultado confirmado. A recompensa ficará pendente para uma nova tentativa.";
            RankChangeReceipt clientRankReceipt = null;
            localRankResultReceipt = null;
            if (competitivePolicy == CompetitivePolicy.Ranked &&
                sealedRankedMatch != null)
            {
                RankedOutcome hostOutcome = draw
                    ? RankedOutcome.Draw
                    : hostWon ? RankedOutcome.Win : RankedOutcome.Loss;
                RankedOutcome clientOutcome = draw
                    ? RankedOutcome.Draw
                    : clientWon ? RankedOutcome.Win : RankedOutcome.Loss;
                if (RankPointService.TryCreateReceipt(
                        sealedRankedMatch,
                        0,
                        hostOutcome,
                        out RankChangeReceipt proposedHostRank,
                        out string hostRankError) &&
                    frontend != null && frontend.TryApplyRankReceipt(
                        proposedHostRank,
                        out localRankResultReceipt,
                        out hostRankError))
                {
                    hostDetail = AppendRankStatus(
                        hostDetail,
                        localRankResultReceipt);
                }
                else
                {
                    Debug.LogWarning(
                        "[MP] stage=rank-host-commit result=blocked reason=" +
                        (hostRankError ?? "frontend unavailable"));
                    hostDetail += "\nO ranque nao foi alterado: " +
                        (hostRankError ?? "perfil indisponivel");
                }

                if (!RankPointService.TryCreateReceipt(
                        sealedRankedMatch,
                        1,
                        clientOutcome,
                        out clientRankReceipt,
                        out string clientRankError))
                {
                    Debug.LogWarning(
                        "[MP] stage=rank-client-receipt result=blocked reason=" +
                        clientRankError);
                    clientRankReceipt = null;
                }
            }
            var result = new MatchRewardPayload
            {
                protocolVersion = ProtocolVersion,
                matchId = currentMatchId,
                transitionEpoch = currentTransitionEpoch,
                resultSequence = ++nextResultSequence,
                winnerSeat = winnerSeat,
                loserSeat = loserSeat,
                endReason = draw ? "DRAW" : "ENGINE_WIN",
                finalStateVersion = authoritativeStateVersion,
                finishedAtServerTick = networkManager != null
                    ? networkManager.ServerTime.Tick
                    : 0,
                damageDealt = clientRewardDamage,
                statisticsDamageDealt = clientStatisticsDamageDealt,
                statisticsDamageReceived = clientStatisticsDamageReceived,
                completedRounds = completedRewardRounds,
                winner = clientWon,
                draw = draw,
                rankReceipt = clientRankReceipt
            };
            lastAuthoritativeResult = result;
            if (activeTournamentContext != null && winnerSeat >= 0)
            {
                TournamentDuelStatsSnapshot tournamentStats =
                    tournamentMetricsCollector?.Finish();
                tournamentResultReportTask = TournamentOnlineSession
                    .EnsureInstance()
                    .ReportDuelResultAsync(
                        activeTournamentContext,
                        winnerSeat,
                        false,
                        false,
                        tournamentStats);
            }
            HandleAuthoritativeResult(
                result,
                0,
                hostDetail,
                localRankResultReceipt);

            if (remoteClientId != ulong.MaxValue)
            {
                SendToClient(
                    remoteClientId,
                    MatchRewardMessage,
                    result,
                    NetworkDelivery.ReliableSequenced);
            }
            _ = sessionCoordinator.SetHostMatchStateAsync(
                "finished",
                currentMatchId,
                false);
            if (stateHeartbeat != null)
            {
                StopCoroutine(stateHeartbeat);
                stateHeartbeat = null;
            }
        }

        private void ProcessMatchRewardMessage(
            ulong senderClientId,
            MatchRewardPayload reward)
        {
            if (role != SessionRole.Client ||
                senderClientId != NetworkManager.ServerClientId ||
                reward == null ||
                reward.protocolVersion != ProtocolVersion ||
                !MatchIdsAreCompatible(currentMatchId, reward.matchId) ||
                reward.transitionEpoch != currentTransitionEpoch ||
                reward.resultSequence == 0 ||
                reward.resultSequence <= lastAppliedResultSequence)
            {
                return;
            }

            lastAuthoritativeResult = reward;
            if (reward.finalStateVersion > lastReplicaStateVersion)
            {
                clientSynchronizing = true;
                replicaController?.SetPresentationDecisionLocked(true);
                loadingPresenter?.Show(
                    "Sincronizando resultado...",
                    "Aguardando o estado final confirmado pelo anfitrião.");
                RequestResync("terminal-state-pending");
                return;
            }

            ApplyClientAuthoritativeResult(reward);
        }

        private void ApplyClientAuthoritativeResult(MatchRewardPayload reward)
        {
            if (reward == null || reward.resultSequence == 0 ||
                reward.resultSequence <= lastAppliedResultSequence)
            {
                return;
            }

            GameFrontendBootstrap frontend = GameFrontendBootstrap.Instance;
            RewardReceipt receipt = null;
            string rejection = "frontend indisponível";
            if (frontend == null || !frontend.TryApplyOnlineDuelReward(
                    reward.matchId,
                    "seat1",
                    reward.damageDealt,
                    reward.statisticsDamageDealt,
                    reward.statisticsDamageReceived,
                    reward.completedRounds,
                    reward.winner,
                    reward.draw,
                    localRewardEligibilityAtMatchStart,
                    out receipt,
                    out rejection))
            {
                Debug.LogWarning(
                    "[Arcane Duel Online] Recompensa do host não salva: " +
                    (rejection ?? "frontend indisponível"));
            }

            string detail = receipt != null
                ? FormatRewardStatus(receipt, reward.draw)
                : "Resultado confirmado. A recompensa ficará pendente para uma nova tentativa.";
            RankChangeReceipt committedRank = null;
            if (reward.rankReceipt != null)
            {
                string rankRejection = "frontend indisponível";
                if (frontend != null && frontend.TryApplyRankReceipt(
                        reward.rankReceipt,
                        out committedRank,
                        out rankRejection))
                {
                    detail = AppendRankStatus(detail, committedRank);
                }
                else
                {
                    Debug.LogWarning(
                        "[MP] stage=rank-client-commit result=blocked reason=" +
                        (rankRejection ?? "frontend unavailable"));
                    detail += "\nO ranque nao foi alterado: " +
                        (rankRejection ?? "perfil indisponivel");
                }
            }
            localRankResultReceipt = committedRank;
            HandleAuthoritativeResult(reward, 1, detail, committedRank);
        }

        private void HandleAuthoritativeResult(
            MatchRewardPayload result,
            byte localSeat,
            string detail,
            RankChangeReceipt rankReceipt = null)
        {
            if (result == null || result.resultSequence == 0 ||
                result.resultSequence <= lastAppliedResultSequence)
            {
                return;
            }

            lastAppliedResultSequence = result.resultSequence;
            matchRewardFinalized = true;
            clientSynchronizing = false;
            hostController?.SetPresentationDecisionLocked(true);
            replicaController?.SetPresentationDecisionLocked(true);
            loadingPresenter?.HideImmediately();
            OnlineDuelResultKind kind = OnlineDuelResultMapper.Map(
                localSeat,
                result.winnerSeat,
                result.loserSeat,
                result.endReason);
            SetFlowState(OnlineMatchFlowState.ResultScreen);
            status = detail ?? string.Empty;
            if (OnlineDuelResultPresenter.CanPresentRankTransition(
                    rankReceipt))
            {
                resultPresenter?.ShowRanked(
                    kind,
                    detail,
                    rankReceipt,
                    ReturnToMenuAfterOnlineMatch);
            }
            else
            {
                resultPresenter?.Show(
                    kind,
                    detail,
                    ReturnToMenuAfterOnlineMatch);
            }
            Debug.Log(
                $"[MP] stage=result-applied sequence={result.resultSequence} " +
                $"kind={kind} role={role} state={result.finalStateVersion}");
        }

        private void ShowRewardResult(string message)
        {
            rewardResultMessage = message ?? string.Empty;
            rewardResultVisibleUntil = Time.realtimeSinceStartup + 10f;
        }

        private static string FormatRewardStatus(
            RewardReceipt receipt,
            bool draw)
        {
            if (receipt == null)
                return "Duelo concluído sem recibo de recompensa.";
            if (receipt.status == RewardReceiptStatus.AlreadyProcessed)
            {
                return receipt.originalStatus == RewardReceiptStatus.Granted
                    ? $"Recompensa já processada: {receipt.coins} moedas."
                    : "Recompensa já processada: " +
                      ArcaneArena.Frontend.DeckRepository.RewardStatusMessage(
                          receipt.originalStatus);
            }
            if (receipt.status != RewardReceiptStatus.Granted)
            {
                return "Duelo concluído. 0 moedas. " +
                       ArcaneArena.Frontend.DeckRepository.RewardStatusMessage(
                           receipt.status);
            }
            return draw
                ? "Duelo concluído. Empate não concede moedas."
                : $"Duelo concluído. Recompensa: {receipt.coins} moedas.";
        }

        private static string AppendRankStatus(
            string detail,
            RankChangeReceipt receipt)
        {
            if (receipt == null)
                return detail ?? string.Empty;
            string delta = receipt.delta > 0
                ? $"+{receipt.delta} PE"
                : $"{receipt.delta} PE";
            return (detail ?? string.Empty) + "\nRanqueada: " + delta +
                   $" · {RankRules.DisplayName(receipt.newTier)} " +
                   $"({receipt.newPoints} PE)";
        }

        private IEnumerator BroadcastLatestStateAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            pendingStateBroadcast = null;
            BroadcastState();
            FinalizePendingAuthoritativeResult();
        }

        private void BroadcastState()
        {
            if (!IsHost || hostController == null || !hostCoreStarted ||
                remoteClientId == ulong.MaxValue || !matchStarted)
            {
                return;
            }
            if (!hostController.ReconcilePresentationFromCore())
            {
                Debug.LogWarning(
                    "[Arcane Duel Online] O snapshot autoritativo do Core " +
                    "não pôde ser consultado nesta atualização.");
            }
            DuelNetworkState state = DuelNetworkProtocol.CreateState(
                hostController.PresentationState,
                hostController.CurrentPrompt,
                1,
                ++nextStateSequence,
                status);
            DuelNetworkProtocol.PopulateCombatStats(
                state,
                hostController,
                1);
            state.matchId = currentMatchId;
            ulong publicHash = DuelNetworkProtocol
                .ComputePublicProjectionHash(state);
            if (authoritativeStateVersion == 0 ||
                publicHash != authoritativePublicStateHash)
            {
                authoritativeStateVersion++;
                authoritativePublicStateHash = publicHash;
            }
            state.stateVersion = authoritativeStateVersion;
            state.publicStateHash = authoritativePublicStateHash;
            state.lastAcceptedClientSequence = lastAcceptedClientSequence;
            state.acknowledgedCommandId = lastAcknowledgedCommandId;
            state.acknowledgedResponseRequestId =
                lastAcknowledgedResponseRequestId;
            SendToClient(remoteClientId, StateMessage, state);
        }

        private void StartStateHeartbeat()
        {
            if (stateHeartbeat != null)
                StopCoroutine(stateHeartbeat);
            stateHeartbeat = StartCoroutine(BroadcastStateHeartbeat());
        }

        private IEnumerator BroadcastStateHeartbeat()
        {
            while (IsHost && matchStarted && hostCoreStarted &&
                   remoteClientId != ulong.MaxValue)
            {
                yield return new WaitForSecondsRealtime(StateHeartbeatSeconds);
                BroadcastState();
            }
            stateHeartbeat = null;
        }

        private bool ApplyReplicaState(DuelNetworkState state)
        {
            if (replicaController == null)
                return false;
            if (!replicaController.ApplyNetworkState(state))
                return false;
            DrainPresentationEvents();
            bool hostAdvancedPrompt = state.prompt == null ||
                state.prompt.requestId != pendingResponseRequestId;
            if (pendingResponseRequestId != 0 &&
                (state.acknowledgedCommandId != pendingCommandId ||
                 state.acknowledgedResponseRequestId != pendingResponseRequestId ||
                 !hostAdvancedPrompt))
            {
                if (!hostAdvancedPrompt)
                {
                    // A resynchronization can confirm a newer state version
                    // while the Core is still waiting on the same request.
                    // Retrying with the confirmed version avoids a permanent
                    // stale-command loop on a slow Relay connection.
                    pendingExpectedStateVersion = state.stateVersion;
                    nextResponseRetryTime =
                        Time.realtimeSinceStartup + 0.20f;
                }
                replicaController.SetPresentationDecisionLocked(true);
                status = "Resposta entregue ao Relay. Aguardando o host processar...";
                return true;
            }
            if (pendingResponseRequestId != 0)
            {
                pendingResponseRequestId = 0;
                pendingResponseBytes = null;
                pendingCommandId = 0;
                pendingClientSequence = 0;
                pendingExpectedStateVersion = 0;
                nextResponseRetryTime = 0f;
                pendingResponseStartedAt = 0f;
                nextPendingResponseResyncTime = 0f;
            }
            if (!beginDuelApplied)
            {
                replicaController.SetPresentationDecisionLocked(true);
                status = "Snapshot aplicado. Aguardando o início autoritativo...";
                return true;
            }
            clientSynchronizing = false;
            replicaController.SetPresentationDecisionLocked(false);
            return true;
        }

        private void MaintainPendingClientResponse(float now)
        {
            if (pendingResponseRequestId == 0 ||
                pendingResponseBytes == null)
            {
                return;
            }

            if (now >= nextResponseRetryTime)
                SendPendingClientResponse();

            if (pendingResponseStartedAt <= 0f)
                pendingResponseStartedAt = now;
            if (now < nextPendingResponseResyncTime)
                return;

            nextPendingResponseResyncTime = now + ResponseResyncSeconds;
            RequestResync("response-ack-timeout");
        }

        private void SendPendingClientResponse()
        {
            if (role != SessionRole.Client || networkManager == null ||
                !networkManager.IsConnectedClient ||
                pendingResponseRequestId == 0 ||
                pendingResponseBytes == null ||
                pendingResponseBytes.Length == 0)
            {
                return;
            }

            var response = new ResponsePayload
            {
                schemaVersion = CommandSchemaVersion,
                matchId = currentMatchId,
                commandType = "prompt-response",
                commandId = pendingCommandId,
                clientSequence = pendingClientSequence,
                expectedStateVersion = pendingExpectedStateVersion,
                requestId = pendingResponseRequestId,
                responseBase64 = Convert.ToBase64String(pendingResponseBytes)
            };

            // Prompt responses are tiny and latency-sensitive. Send a direct
            // reliable copy in addition to the chunked/retry wire path. The
            // host's command id + sequence + payload hash validation makes
            // both deliveries idempotent. This prevents a delayed wire ACK on
            // Android from holding an already-selected attack for minutes.
            SendFastResponseToServer(response);
            SendToServer(ResponseMessage, response);
            nextResponseRetryTime =
                Time.realtimeSinceStartup + ResponseRetrySeconds;
        }

        private bool SendFastResponseToServer(ResponsePayload response)
        {
            if (response == null || role != SessionRole.Client ||
                networkManager?.CustomMessagingManager == null ||
                !networkManager.IsConnectedClient)
            {
                return false;
            }

            byte[] jsonBytes;
            try
            {
                jsonBytes = Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(response));
            }
            catch (Exception)
            {
                return false;
            }
            if (jsonBytes.Length == 0 ||
                jsonBytes.Length > MaximumFastResponseBytes)
            {
                return false;
            }

            var writer = new FastBufferWriter(
                jsonBytes.Length + sizeof(ushort),
                Allocator.Temp,
                MaximumFastResponseBytes + sizeof(ushort));
            try
            {
                writer.WriteValueSafe((ushort)jsonBytes.Length);
                writer.WriteBytesSafe(jsonBytes);
                networkManager.CustomMessagingManager.SendNamedMessage(
                    ResponseFastMessage,
                    NetworkManager.ServerClientId,
                    writer,
                    NetworkDelivery.ReliableSequenced);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Arcane Duel Online] Fast response indisponivel; " +
                    "mantendo o envio v4: " +
                    exception.GetBaseException().Message);
                return false;
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void OnFastResponseMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!IsHost || senderClientId != remoteClientId)
                return;

            try
            {
                reader.ReadValueSafe(out ushort length);
                if (length == 0 || length > MaximumFastResponseBytes)
                    return;
                var jsonBytes = new byte[length];
                reader.ReadBytesSafe(ref jsonBytes, length);
                ResponsePayload response = JsonUtility.FromJson<ResponsePayload>(
                    Encoding.UTF8.GetString(jsonBytes));
                ProcessResponseMessage(senderClientId, response);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Arcane Duel Online] Fast response invalida: " +
                    exception.GetBaseException().Message);
            }
        }

        private void TrySendPendingPresentationEvents()
        {
            if (!IsHost || remoteClientId == ulong.MaxValue ||
                networkManager == null || !networkManager.IsServer)
            {
                return;
            }

            while (outgoingPresentationEvents.Count > 0)
            {
                DuelNetworkPresentationEvent next =
                    outgoingPresentationEvents.Peek();
                if (!SendToClient(
                        remoteClientId,
                        PresentationEventMessage,
                        next))
                {
                    return;
                }
                outgoingPresentationEvents.Dequeue();
            }
        }

        private void DrainPresentationEvents()
        {
            if (replicaController == null)
                return;
            while (pendingPresentationEvents.TryGetValue(
                       lastPresentationEventSequence + 1,
                       out DuelNetworkPresentationEvent presentationEvent))
            {
                if (lastReplicaSequence <
                    presentationEvent.requiredStateSequence)
                {
                    return;
                }
                pendingPresentationEvents.Remove(
                    presentationEvent.eventSequence);
                lastPresentationEventSequence =
                    presentationEvent.eventSequence;
                replicaController.PresentNetworkEvent(
                    DuelNetworkProtocol.ToPresentationEvent(
                        presentationEvent));
            }
        }

        private bool TryValidateRemoteResponse(
            ResponsePayload response,
            byte[] bytes)
        {
            DuelPrompt prompt = hostController?.CurrentPrompt;
            if (prompt == null || prompt.Player != 1 ||
                prompt.RequestId == 0 || prompt.RequestId != response.requestId ||
                bytes == null || bytes.Length == 0 || bytes.Length > 2048)
            {
                return false;
            }
            // The current prompt, the player side and the request id are
            // checked here; ocgcore remains the final validator for every
            // protocol byte, including multi-card selections.
            return true;
        }

        private static bool TryDecodeResponseBytes(
            ResponsePayload response,
            out byte[] bytes)
        {
            bytes = null;
            if (response == null ||
                string.IsNullOrWhiteSpace(response.responseBase64))
            {
                return false;
            }
            try
            {
                bytes = Convert.FromBase64String(response.responseBase64);
                return bytes.Length > 0 && bytes.Length <= 2048;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private bool ValidateCommandEnvelope(ResponsePayload response)
        {
            return response != null &&
                response.schemaVersion == CommandSchemaVersion &&
                response.commandType == "prompt-response" &&
                response.commandId != 0 &&
                response.clientSequence == lastAcceptedClientSequence + 1 &&
                response.expectedStateVersion == authoritativeStateVersion &&
                response.matchId == currentMatchId &&
                CanAcceptPromptResponseDuringSynchronization();
        }

        private bool CanAcceptPromptResponseDuringSynchronization()
        {
            if (!clientSynchronizing)
                return true;

            // A normal mid-duel repair must continue accepting the exact
            // current prompt response. Initial loading and reconnect recovery
            // remain closed until their own readiness barriers complete.
            return beginDuelApplied &&
                   hostAwaitingLiveStateAck &&
                   !hostAwaitingReconnect &&
                   !reconnecting;
        }

        private bool ConsumeCommandToken()
        {
            float now = Time.realtimeSinceStartup;
            float elapsed = Mathf.Max(0f, now - lastCommandTokenTime);
            lastCommandTokenTime = now;
            commandTokens = Mathf.Min(
                CommandBurstCapacity,
                commandTokens + elapsed * CommandRatePerSecond);
            if (commandTokens < 1f)
                return false;
            commandTokens -= 1f;
            return true;
        }

        private void DisconnectProtocolViolation(string reason)
        {
            RuntimeDiagnosticRecorder.Record(
                "F08",
                "MultiplayerProtocol",
                nameof(DuelOnlineSession),
                "Remote command violated the online protocol.",
                mode: IsHost ? "online-host" : "online-client",
                details: reason);
            Debug.LogWarning("[MP] stage=protocol-violation reason=" + reason);
            status = "O rival enviou uma sequência de comandos inválida.";
            if (networkManager != null && networkManager.IsServer &&
                remoteClientId != ulong.MaxValue)
            {
                networkManager.DisconnectClient(remoteClientId, reason);
            }
        }

        private bool ValidateHello(HelloPayload hello, out string rejection)
        {
            rejection = string.Empty;
            if (hello == null || hello.loadout == null ||
                hello.protocolVersion != ProtocolVersion)
            {
                rejection = "O rival usa um protocolo online incompatível. " +
                    "Ambos precisam instalar a versão ONLINE v11.";
                return false;
            }
            if (hello.compatibility !=
                ProjectIdentity.MultiplayerCompatibility)
            {
                rejection = "O conteúdo do jogo é diferente entre os dois " +
                    "dispositivos. Instale a mesma versão ONLINE v11 no PC " +
                    "e no celular para usar todos os decks corretamente.";
                return false;
            }
            if (hello.competitivePolicy != competitivePolicy)
            {
                rejection = "A sala mudou de modo competitivo. Feche o painel e entre novamente.";
                return false;
            }
            if (competitivePolicy == CompetitivePolicy.Ranked &&
                (hello.rankPlayer == null || !hello.rankPlayer.IsValid ||
                 !string.Equals(
                     hello.rankPlayer.stablePlayerId,
                     hello.loadout.profileId,
                     StringComparison.Ordinal)))
            {
                rejection = "O perfil ranqueado do rival é inválido ou usa regras diferentes.";
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
            int sideCount = hello.loadout.sideDeckCardIds?.Count ?? 0;
            if ((hello.loadout.playerDisplayName?.Length ?? 0) > 128 ||
                (hello.loadout.displayName?.Length ?? 0) > 128 ||
                (hello.loadout.profileId?.Length ?? 0) > 128 ||
                (hello.loadout.deckId?.Length ?? 0) > 128)
            {
                rejection = "O deck remoto possui metadados maiores que o limite online.";
                return false;
            }
            if (mainCount < 40 || mainCount > 60 ||
                extraCount > 15 || sideCount > 15)
            {
                rejection = "O deck remoto não respeita os limites de Main, Extra e Side.";
                return false;
            }
            if (ParseCardCodes(hello.loadout.mainDeckCardIds).Length != mainCount ||
                ParseCardCodes(hello.loadout.extraDeckCardIds).Length != extraCount ||
                ParseCardCodes(hello.loadout.sideDeckCardIds).Length != sideCount)
            {
                rejection = "O deck remoto possui identificadores de carta inválidos.";
                return false;
            }
            return OnlineDeckLegalityGate.TryValidate(
                hello.loadout, out rejection);
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
            if (!OnlineDeckLegalityGate.TryValidate(loadout, out error))
                return false;
            return true;
        }

        private static CoinRewardEligibilitySnapshot
            CaptureLocalRewardEligibility()
        {
            GameFrontendBootstrap frontend = GameFrontendBootstrap.Instance;
            return frontend != null
                ? frontend.CaptureOnlineDuelRewardEligibility()
                : CoinRewardEligibilitySnapshot.Blocked(
                    string.Empty,
                    string.Empty,
                    0,
                    RewardReceiptStatus.BlockedInvalidMatch);
        }

        private static RankPlayerSnapshot CaptureLocalRankSnapshot()
        {
            return GameFrontendBootstrap.Instance?.CaptureRankPlayerSnapshot();
        }

        private bool TrySealRankedMatchSnapshot(
            string matchId,
            out string rejection)
        {
            rejection = string.Empty;
            localRankHandshake = CaptureLocalRankSnapshot();
            if (localRankHandshake == null || !localRankHandshake.IsValid ||
                remoteRankHandshake == null || !remoteRankHandshake.IsValid)
            {
                rejection = "Não foi possível selar os dois perfis ranqueados. Reconecte a sala.";
                return false;
            }
            if (!string.Equals(
                    localRankHandshake.stablePlayerId,
                    localLoadout?.profileId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    remoteRankHandshake.stablePlayerId,
                    remoteLoadout?.profileId,
                    StringComparison.Ordinal))
            {
                rejection = "A identidade ranqueada não corresponde aos decks confirmados.";
                return false;
            }

            sealedRankedMatch = new RankedMatchSnapshot
            {
                matchId = matchId,
                policy = CompetitivePolicy.Ranked,
                source = activeTournamentContext != null
                    ? CompetitiveMatchSource.Tournament
                    : automaticRankedMatchmaking
                        ? CompetitiveMatchSource.Matchmaking
                        : CompetitiveMatchSource.PrivateRoom,
                rulesVersion = RankRules.RulesVersion,
                rulesHash = RankRules.RulesHash,
                sealedAtUtcTicks = DateTime.UtcNow.Ticks,
                seat0 = localRankHandshake,
                seat1 = remoteRankHandshake
            };
            if (sealedRankedMatch.IsValid)
                return true;

            sealedRankedMatch = null;
            rejection = "O snapshot ranqueado final não passou na validação.";
            return false;
        }

        private bool TryAcceptRankedMatchSnapshot(
            RankedMatchSnapshot snapshot,
            out string rejection)
        {
            rejection = string.Empty;
            if (competitivePolicy != CompetitivePolicy.Ranked ||
                snapshot == null || !snapshot.IsValid ||
                snapshot.policy != CompetitivePolicy.Ranked)
            {
                rejection = "O snapshot da partida ranqueada é incompatível.";
                return false;
            }

            RankPlayerSnapshot current = CaptureLocalRankSnapshot();
            RankPlayerSnapshot client = snapshot.seat1;
            if (current == null || !current.IsValid || client == null ||
                !string.Equals(
                    current.stablePlayerId,
                    client.stablePlayerId,
                    StringComparison.Ordinal) ||
                current.rankedPoints != client.rankedPoints ||
                current.stateVersion != client.stateVersion)
            {
                rejection = "Seu perfil ranqueado mudou depois da entrada na sala. Entre novamente.";
                return false;
            }

            sealedRankedMatch = snapshot;
            remoteRankHandshake = snapshot.seat0;
            localRankHandshake = snapshot.seat1;
            return true;
        }

        private bool SendToServer<T>(
            string messageName,
            T payload,
            NetworkDelivery delivery = NetworkDelivery.Unreliable)
        {
            return Send(
                NetworkManager.ServerClientId,
                messageName,
                payload,
                delivery);
        }

        private bool SendToClient<T>(
            ulong clientId,
            string messageName,
            T payload,
            NetworkDelivery delivery = NetworkDelivery.Unreliable)
        {
            return Send(clientId, messageName, payload, delivery);
        }

        private bool Send<T>(
            ulong target,
            string messageName,
            T payload,
            NetworkDelivery delivery)
        {
            // Large snapshots still use the v4 chunk ACK/retry layer. Tiny
            // control messages additionally use NGO reliable delivery so a
            // response or state-repair command cannot sit behind presentation
            // traffic after an isolated packet loss.
            if (networkManager == null ||
                networkManager.CustomMessagingManager == null)
            {
                return false;
            }

            if (!TryResolveLogicalMessage(
                    messageName,
                    out LogicalMessage logicalMessage,
                    out DuelWireKind wireKind))
            {
                Debug.LogError(
                    $"[Arcane Duel Online] Tipo de mensagem desconhecido: '{messageName}'.");
                return false;
            }

            byte[] jsonBytes;
            try
            {
                string json = JsonUtility.ToJson(payload);
                jsonBytes = Encoding.UTF8.GetBytes(json);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Arcane Duel Online] Nao foi possivel serializar " +
                    $"'{messageName}': {exception.GetBaseException().Message}");
                return false;
            }

            if (jsonBytes.Length <= 0 || jsonBytes.Length > MaxWireBytes)
            {
                Debug.LogError(
                    $"[Arcane Duel Online] Mensagem '{messageName}' fora do " +
                    $"limite v4: {jsonBytes.Length} bytes UTF-8.");
                return false;
            }

            byte[] wirePayload;
            try
            {
                wirePayload = EncodeLogicalPayload(logicalMessage, jsonBytes);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Arcane Duel Online] Falha ao comprimir '{messageName}': " +
                    exception.GetBaseException().Message);
                return false;
            }
            if (wirePayload.Length > MaxWireBytes)
            {
                Debug.LogError(
                    $"[Arcane Duel Online] Mensagem '{messageName}' excedeu " +
                    $"o limite comprimido v4: {wirePayload.Length} bytes.");
                return false;
            }
            ulong checksum = DuelWireProtocol.ComputePayloadChecksum(wirePayload);
            foreach (OutboundWireTransfer existing in outboundWireTransfers.Values)
            {
                if (existing.Target == target &&
                    existing.LogicalMessage == logicalMessage &&
                    existing.Transfer.TotalLength == wirePayload.Length &&
                    existing.Transfer.PayloadChecksum == checksum)
                {
                    existing.NextSendTime = 0f;
                    return true;
                }
            }

            if (logicalMessage == LogicalMessage.State)
                RemoveSupersededStateTransfers(target);
            if (outboundWireTransfers.Count >= MaximumConcurrentWireTransfers)
            {
                Debug.LogError(
                    "[Arcane Duel Online] Limite de transferencias pendentes " +
                    "atingido. A conexao sera mantida para recuperar os ACKs.");
                return false;
            }

            DuelWireTransfer transfer;
            try
            {
                transfer = DuelWireProtocol.CreateTransfer(
                    wireKind,
                    wirePayload);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[Arcane Duel Online] Falha ao preparar '{messageName}': " +
                    exception.GetBaseException().Message);
                return false;
            }

            var pending = new OutboundWireTransfer
            {
                Target = target,
                LogicalMessage = logicalMessage,
                Delivery = ResolveWireDelivery(logicalMessage, delivery),
                Transfer = transfer,
                AckTracker = new DuelWireAckTracker(transfer),
                NextSendTime = 0f
            };
            outboundWireTransfers.Add(
                new WireTransferKey(target, transfer.TransferId),
                pending);
            return true;
        }

        private static NetworkDelivery ResolveWireDelivery(
            LogicalMessage message,
            NetworkDelivery requested)
        {
            if (IsCriticalControlMessage(message))
                return NetworkDelivery.ReliableSequenced;
            return requested == NetworkDelivery.Reliable ||
                   requested == NetworkDelivery.ReliableSequenced ||
                   requested == NetworkDelivery.ReliableFragmentedSequenced
                ? NetworkDelivery.ReliableSequenced
                : NetworkDelivery.Unreliable;
        }

        private static bool IsCriticalControlMessage(LogicalMessage message)
        {
            return message == LogicalMessage.Response ||
                   message == LogicalMessage.StateAck ||
                   message == LogicalMessage.ResyncRequest ||
                   message == LogicalMessage.ClientReady ||
                   message == LogicalMessage.BeginDuel ||
                   message == LogicalMessage.Start ||
                   message == LogicalMessage.MatchReward;
        }

        private static int WirePriority(LogicalMessage message)
        {
            if (IsCriticalControlMessage(message))
                return 0;
            if (message == LogicalMessage.State)
                return 1;
            if (message == LogicalMessage.PresentationEvent)
                return 3;
            return 2;
        }

        private static byte[] EncodeLogicalPayload(
            LogicalMessage logicalMessage,
            byte[] jsonBytes)
        {
            bool compress = jsonBytes.Length >= CompressionThresholdBytes;
            byte[] body = jsonBytes;
            if (compress)
            {
                using var output = new MemoryStream();
                using (var gzip = new GZipStream(
                           output,
                           System.IO.Compression.CompressionLevel.Fastest,
                           true))
                {
                    gzip.Write(jsonBytes, 0, jsonBytes.Length);
                }
                body = output.ToArray();
                if (body.Length >= jsonBytes.Length)
                {
                    compress = false;
                    body = jsonBytes;
                }
            }

            var encoded = new byte[body.Length + 2];
            encoded[0] = (byte)logicalMessage;
            encoded[1] = compress ? (byte)1 : (byte)0;
            Buffer.BlockCopy(body, 0, encoded, 2, body.Length);
            return encoded;
        }

        private static bool TryDecodeLogicalJson(
            byte[] payload,
            out LogicalMessage logicalMessage,
            out string json,
            out string error)
        {
            logicalMessage = LogicalMessage.Unknown;
            json = string.Empty;
            error = string.Empty;
            if (payload == null || payload.Length < 3)
            {
                error = "Payload lógico v4 incompleto.";
                return false;
            }

            logicalMessage = (LogicalMessage)payload[0];
            byte codec = payload[1];
            var body = new byte[payload.Length - 2];
            Buffer.BlockCopy(payload, 2, body, 0, body.Length);

            byte[] jsonBytes;
            if (codec == 0)
            {
                jsonBytes = body;
            }
            else if (codec == 1)
            {
                try
                {
                    using var input = new MemoryStream(body, false);
                    using var gzip = new GZipStream(
                        input,
                        CompressionMode.Decompress);
                    using var output = new MemoryStream();
                    var buffer = new byte[4096];
                    int read;
                    while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + read > MaxWireBytes)
                        {
                            error = "Payload descomprimido excede o limite v4.";
                            return false;
                        }
                        output.Write(buffer, 0, read);
                    }
                    jsonBytes = output.ToArray();
                }
                catch (Exception)
                {
                    error = "Payload GZip v4 inválido.";
                    return false;
                }
            }
            else
            {
                error = "Codec lógico v4 desconhecido.";
                return false;
            }

            return DuelWireProtocol.TryDecodeUtf8(
                jsonBytes,
                out json,
                out error);
        }

        private void SetFlowState(OnlineMatchFlowState next)
        {
            if (flowState == next)
                return;
            OnlineMatchFlowState previous = flowState;
            flowState = next;
            flowStateEnteredAt = Time.realtimeSinceStartup;
            Debug.Log(
                $"[MP] stage=flow from={previous} to={next} " +
                $"epoch={currentTransitionEpoch} match={currentMatchId}");
        }

        private void MaintainFlowTimeout(float now)
        {
            if (!matchStarted || flowStateEnteredAt <= 0f ||
                reconnecting || hostAwaitingReconnect ||
                flowState == OnlineMatchFlowState.InDuel ||
                flowState == OnlineMatchFlowState.DuelFinished ||
                flowState == OnlineMatchFlowState.ResultScreen ||
                flowState == OnlineMatchFlowState.Leaving ||
                flowState == OnlineMatchFlowState.RecoverableError ||
                flowState == OnlineMatchFlowState.FatalError)
            {
                return;
            }

            float timeout = flowState == OnlineMatchFlowState.Synchronizing ||
                            flowState == OnlineMatchFlowState.WaitingSnapshotAck
                ? flowConfig.SnapshotApplyTimeoutSeconds
                : flowConfig.SceneLoadTimeoutSeconds;
            if (now - flowStateEnteredAt < timeout)
                return;

            string code = flowState == OnlineMatchFlowState.Synchronizing ||
                          flowState == OnlineMatchFlowState.WaitingSnapshotAck
                ? "INITIAL_SYNC_FAILED"
                : "MATCH_LOAD_TIMEOUT";
            string message = code == "INITIAL_SYNC_FAILED"
                ? "Não foi possível sincronizar a partida."
                : "Não foi possível carregar a partida a tempo.";
            SetFlowState(OnlineMatchFlowState.RecoverableError);
            status = $"{code}: {message}";
            hostController?.SetPresentationDecisionLocked(true);
            replicaController?.SetPresentationDecisionLocked(true);
            loadingPresenter?.ShowError(message, ReturnToMenuAfterOnlineMatch);
            Debug.LogWarning(
                $"[MP] stage=transition-timeout code={code} " +
                $"epoch={currentTransitionEpoch}");
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            MaintainFlowTimeout(now);
            MaintainArenaReadyHandshake(now);
            TrySendPendingPresentationEvents();
            MaintainPendingClientResponse(now);
            if (now < nextWirePumpTime)
                return;
            nextWirePumpTime = now + WirePumpSeconds;
            if (now >= nextWireCleanupTime)
            {
                nextWireCleanupTime = now + 1f;
                CleanupWireTransfers(now);
            }
            if (outboundWireTransfers.Count == 0)
                return;

            int remainingBudget = WirePacketsPerFrame;
            var keys = new List<WireTransferKey>(outboundWireTransfers.Keys);
            keys.Sort((left, right) =>
            {
                bool hasLeft = outboundWireTransfers.TryGetValue(
                    left,
                    out OutboundWireTransfer leftTransfer);
                bool hasRight = outboundWireTransfers.TryGetValue(
                    right,
                    out OutboundWireTransfer rightTransfer);
                if (!hasLeft || !hasRight)
                    return hasLeft ? -1 : hasRight ? 1 : 0;
                return WirePriority(leftTransfer.LogicalMessage).CompareTo(
                    WirePriority(rightTransfer.LogicalMessage));
            });
            foreach (WireTransferKey key in keys)
            {
                if (remainingBudget <= 0)
                    break;
                if (!outboundWireTransfers.TryGetValue(
                        key,
                        out OutboundWireTransfer pending) ||
                    pending.NextSendTime > now)
                {
                    continue;
                }

                remainingBudget -= PumpOutboundWireTransfer(
                    pending,
                    now,
                    Math.Min(WirePacketsPerTransferPump, remainingBudget));
            }
        }

        private void MaintainArenaReadyHandshake(float now)
        {
            if (role != SessionRole.Client || !matchStarted ||
                replicaController == null || pendingReplicaState != null ||
                networkManager == null ||
                !networkManager.IsConnectedClient ||
                now < nextClientArenaReadyRetryTime)
            {
                return;
            }

            // Reliable delivery protects each individual message. Repeating
            // the readiness acknowledgement also covers scene races and a
            // host that registered its handler just after the first send.
            SendClientReady(true, true);
            nextClientArenaReadyRetryTime = now + ArenaReadyRetrySeconds;
            status = "Arena pronta. Aguardando o primeiro estado do host...";
            Debug.Log("[MP] stage=arena-ready-retry");
        }

        private int PumpOutboundWireTransfer(
            OutboundWireTransfer pending,
            float now,
            int packetBudget)
        {
            if (pending == null || packetBudget <= 0 ||
                pending.AckTracker.TransferAcknowledged)
            {
                return 0;
            }

            if (pending.AckTracker.AllChunksAcknowledged)
            {
                // Every chunk reached the peer, but its final ACK may have
                // been lost. A completed peer repeats that ACK as soon as it
                // sees the first duplicate. If its completed-transfer cache
                // was also lost or expired, walking the whole transfer here
                // lets it reconstruct the payload again instead of leaving
                // both peers permanently waiting on the final confirmation.
                int sentWhileAwaitingFinalAck = 0;
                int chunkCount = pending.Transfer.ChunkCount;
                while (sentWhileAwaitingFinalAck < packetBudget &&
                       pending.MissingCursor < chunkCount)
                {
                    SendWirePacket(
                        pending.Target,
                        pending.Transfer.GetPacket(pending.MissingCursor++),
                        pending.Delivery);
                    sentWhileAwaitingFinalAck++;
                }

                if (pending.MissingCursor >= chunkCount)
                {
                    pending.MissingCursor = 0;
                    pending.SendRounds++;
                    pending.NextSendTime = now + WireRetrySeconds;
                }
                else
                {
                    pending.NextSendTime = now + WirePumpSeconds;
                }
                return sentWhileAwaitingFinalAck;
            }

            int sent = 0;
            int inspected = 0;
            int transferChunkCount = pending.Transfer.ChunkCount;
            bool completedScan = false;
            while (sent < packetBudget && inspected < transferChunkCount)
            {
                int packetIndex = pending.MissingCursor;
                pending.MissingCursor =
                    (pending.MissingCursor + 1) % transferChunkCount;
                inspected++;
                if (pending.MissingCursor == 0)
                    completedScan = true;
                if (pending.AckTracker.IsChunkAcknowledged(packetIndex))
                    continue;
                SendWirePacket(
                    pending.Target,
                    pending.Transfer.GetPacket(packetIndex),
                    pending.Delivery);
                sent++;
            }

            if (completedScan)
            {
                pending.SendRounds++;
                pending.NextSendTime = now + WireRetrySeconds;
            }
            else
            {
                pending.NextSendTime = now + WirePumpSeconds;
            }
            return sent;
        }

        private void SendWirePacket(
            ulong target,
            DuelWirePacket packet,
            NetworkDelivery delivery = NetworkDelivery.Unreliable)
        {
            if (packet == null || networkManager?.CustomMessagingManager == null)
                return;

            var writer = new FastBufferWriter(
                DuelWireProtocol.MaximumWriterPacketBytes,
                Allocator.Temp,
                DuelWireProtocol.MaximumWriterPacketBytes);
            try
            {
                if (!DuelWireProtocol.TryWritePacket(
                        ref writer,
                        packet,
                        out string error))
                {
                    Debug.LogWarning(
                        $"[Arcane Duel Online] Pacote v3 nao foi codificado: {error}");
                    return;
                }

                networkManager.CustomMessagingManager.SendNamedMessage(
                    WirePacketMessage,
                    target,
                    writer,
                    delivery);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Arcane Duel Online] Falha temporaria ao enviar pacote " +
                    $"{packet.TransferId}: {exception.GetBaseException().Message}");
            }
            finally
            {
                writer.Dispose();
            }
        }

        private void OnWirePacketMessage(
            ulong senderClientId,
            FastBufferReader reader)
        {
            if (!IsExpectedWirePeer(senderClientId))
                return;
            if (!DuelWireProtocol.TryReadPacket(
                    ref reader,
                    out DuelWirePacket packet,
                    out string error))
            {
                Debug.LogWarning(
                    $"[Arcane Duel Online] Pacote v3 invalido de " +
                    $"{senderClientId}: {error}");
                return;
            }

            if (packet.IsData)
                ProcessWireData(senderClientId, packet);
            else
                ProcessWireAck(senderClientId, packet);
        }

        private bool IsExpectedWirePeer(ulong senderClientId)
        {
            if (IsHost)
                return remoteClientId != ulong.MaxValue &&
                       senderClientId == remoteClientId;
            return role == SessionRole.Client &&
                   senderClientId == NetworkManager.ServerClientId;
        }

        private void ProcessWireData(
            ulong senderClientId,
            DuelWirePacket packet)
        {
            var key = new WireTransferKey(
                senderClientId,
                packet.TransferId);
            if (completedWireTransfers.TryGetValue(
                    key,
                    out CompletedWireTransfer completed))
            {
                if (WireMetadataMatches(completed.TransferAck, packet))
                    SendWirePacket(
                        senderClientId,
                        completed.TransferAck,
                        NetworkDelivery.ReliableSequenced);
                return;
            }

            if (!inboundWireTransfers.TryGetValue(
                    key,
                    out InboundWireTransfer incoming))
            {
                if (inboundWireTransfers.Count >=
                    MaximumConcurrentWireTransfers)
                {
                    RemoveOldestInboundWireTransfer();
                }
                incoming = new InboundWireTransfer();
                inboundWireTransfers.Add(key, incoming);
            }
            incoming.LastActivityTime = Time.realtimeSinceStartup;

            DuelWireAcceptResult result = incoming.Reassembler.Accept(
                packet,
                out byte[] payload,
                out string error);
            if (result == DuelWireAcceptResult.Rejected)
            {
                Debug.LogWarning(
                    $"[Arcane Duel Online] Bloco v3 rejeitado " +
                    $"({packet.TransferId}): {error}");
                inboundWireTransfers.Remove(key);
                return;
            }

            SendWirePacket(
                senderClientId,
                DuelWireProtocol.CreateChunkAck(packet),
                NetworkDelivery.ReliableSequenced);
            if (IsHost && packet.Kind == DuelWireKind.Deck &&
                remoteLoadout == null)
            {
                status = $"Recebendo deck do rival: " +
                    $"{incoming.Reassembler.ReceivedChunkCount} / " +
                    $"{incoming.Reassembler.ChunkCount} blocos confirmados...";
            }
            if (result != DuelWireAcceptResult.Completed)
                return;

            DuelWirePacket transferAck =
                DuelWireProtocol.CreateTransferAck(incoming.Reassembler);
            inboundWireTransfers.Remove(key);
            completedWireTransfers[key] = new CompletedWireTransfer
            {
                TransferAck = transferAck,
                CompletedTime = Time.realtimeSinceStartup
            };
            SendWirePacket(
                senderClientId,
                transferAck,
                NetworkDelivery.ReliableSequenced);
            DispatchWirePayload(senderClientId, packet.Kind, payload);
        }

        private void ProcessWireAck(
            ulong senderClientId,
            DuelWirePacket packet)
        {
            var key = new WireTransferKey(
                senderClientId,
                packet.TransferId);
            if (!outboundWireTransfers.TryGetValue(
                    key,
                    out OutboundWireTransfer pending))
            {
                return;
            }

            DuelWireAckResult result = pending.AckTracker.Accept(
                packet,
                out string error);
            if (result == DuelWireAckResult.Rejected)
            {
                Debug.LogWarning(
                    $"[Arcane Duel Online] ACK v3 rejeitado " +
                    $"({packet.TransferId}): {error}");
                return;
            }
            if (pending.AckTracker.TransferAcknowledged)
            {
                outboundWireTransfers.Remove(key);
                return;
            }

            pending.NextSendTime = Math.Min(
                pending.NextSendTime,
                Time.realtimeSinceStartup + WirePumpSeconds);
        }

        private void DispatchWirePayload(
            ulong senderClientId,
            DuelWireKind kind,
            byte[] payload)
        {
            if (!TryDecodeLogicalJson(
                    payload,
                    out LogicalMessage logicalMessage,
                    out string json,
                    out string decodeError))
            {
                Debug.LogWarning(
                    "[Arcane Duel Online] Payload lógico v4 inválido: " +
                    decodeError);
                return;
            }

            if (!LogicalKindMatches(logicalMessage, kind))
            {
                Debug.LogWarning(
                    $"[Arcane Duel Online] Tipo logico {logicalMessage} nao " +
                    $"corresponde ao envelope {kind}.");
                return;
            }

            try
            {
                switch (logicalMessage)
                {
                    case LogicalMessage.Hello:
                        ProcessHelloMessage(
                            senderClientId,
                            JsonUtility.FromJson<HelloPayload>(json));
                        break;
                    case LogicalMessage.HelloRequest:
                        ProcessHelloRequestMessage(
                            senderClientId,
                            JsonUtility.FromJson<ProtocolPayload>(json));
                        break;
                    case LogicalMessage.HelloAccepted:
                        ProcessHelloAcceptedMessage(
                            senderClientId,
                            JsonUtility.FromJson<HelloAcceptedPayload>(json));
                        break;
                    case LogicalMessage.HelloRejected:
                        ProcessHelloRejectedMessage(
                            senderClientId,
                            JsonUtility.FromJson<HelloRejectedPayload>(json));
                        break;
                    case LogicalMessage.Start:
                        ProcessStartMessage(
                            senderClientId,
                            JsonUtility.FromJson<StartPayload>(json));
                        break;
                    case LogicalMessage.ClientReady:
                        ProcessClientReadyMessage(
                            senderClientId,
                            JsonUtility.FromJson<ClientReadyPayload>(json));
                        break;
                    case LogicalMessage.State:
                        ProcessStateMessage(
                            senderClientId,
                            JsonUtility.FromJson<DuelNetworkState>(json));
                        break;
                    case LogicalMessage.Response:
                        ProcessResponseMessage(
                            senderClientId,
                            JsonUtility.FromJson<ResponsePayload>(json));
                        break;
                    case LogicalMessage.PresentationEvent:
                        ProcessPresentationEventMessage(
                            senderClientId,
                            JsonUtility.FromJson<DuelNetworkPresentationEvent>(json));
                        break;
                    case LogicalMessage.StateAck:
                        ProcessStateAckMessage(
                            senderClientId,
                            JsonUtility.FromJson<StateAckPayload>(json));
                        break;
                    case LogicalMessage.ResyncRequest:
                        ProcessResyncRequestMessage(
                            senderClientId,
                            JsonUtility.FromJson<ResyncRequestPayload>(json));
                        break;
                    case LogicalMessage.MatchReward:
                        ProcessMatchRewardMessage(
                            senderClientId,
                            JsonUtility.FromJson<MatchRewardPayload>(json));
                        break;
                    case LogicalMessage.BeginDuel:
                        ProcessBeginDuelMessage(
                            senderClientId,
                            JsonUtility.FromJson<BeginDuelPayload>(json));
                        break;
                    case LogicalMessage.Prelude:
                        ProcessPreludeMessage(
                            senderClientId,
                            JsonUtility.FromJson<PreludePayload>(json));
                        break;
                    case LogicalMessage.PreludeChoice:
                        ProcessPreludeChoiceMessage(
                            senderClientId,
                            JsonUtility.FromJson<PreludeChoicePayload>(json));
                        break;
                    case LogicalMessage.PreludeResult:
                        ProcessPreludeResultMessage(
                            senderClientId,
                            JsonUtility.FromJson<PreludeResultPayload>(json));
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Mensagem logica v3 desconhecida: {logicalMessage}.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Arcane Duel Online] Falha ao aplicar {logicalMessage}: " +
                    exception.GetBaseException().Message);
            }
        }

        private static bool TryResolveLogicalMessage(
            string messageName,
            out LogicalMessage logicalMessage,
            out DuelWireKind kind)
        {
            logicalMessage = LogicalMessage.Unknown;
            kind = DuelWireKind.Unknown;
            switch (messageName)
            {
                case HelloMessage:
                    logicalMessage = LogicalMessage.Hello;
                    kind = DuelWireKind.Deck;
                    return true;
                case HelloRequestMessage:
                    logicalMessage = LogicalMessage.HelloRequest;
                    kind = DuelWireKind.Control;
                    return true;
                case HelloAcceptedMessage:
                    logicalMessage = LogicalMessage.HelloAccepted;
                    kind = DuelWireKind.Control;
                    return true;
                case HelloRejectedMessage:
                    logicalMessage = LogicalMessage.HelloRejected;
                    kind = DuelWireKind.Control;
                    return true;
                case StartMessage:
                    logicalMessage = LogicalMessage.Start;
                    kind = DuelWireKind.Start;
                    return true;
                case ClientReadyMessage:
                    logicalMessage = LogicalMessage.ClientReady;
                    kind = DuelWireKind.Control;
                    return true;
                case StateMessage:
                    logicalMessage = LogicalMessage.State;
                    kind = DuelWireKind.State;
                    return true;
                case ResponseMessage:
                    logicalMessage = LogicalMessage.Response;
                    kind = DuelWireKind.Response;
                    return true;
                case PresentationEventMessage:
                    logicalMessage = LogicalMessage.PresentationEvent;
                    kind = DuelWireKind.State;
                    return true;
                case StateAckMessage:
                    logicalMessage = LogicalMessage.StateAck;
                    kind = DuelWireKind.Control;
                    return true;
                case ResyncRequestMessage:
                    logicalMessage = LogicalMessage.ResyncRequest;
                    kind = DuelWireKind.Control;
                    return true;
                case MatchRewardMessage:
                    logicalMessage = LogicalMessage.MatchReward;
                    kind = DuelWireKind.Control;
                    return true;
                case BeginDuelMessage:
                    logicalMessage = LogicalMessage.BeginDuel;
                    kind = DuelWireKind.Control;
                    return true;
                case PreludeMessage:
                    logicalMessage = LogicalMessage.Prelude;
                    kind = DuelWireKind.Control;
                    return true;
                case PreludeChoiceMessage:
                    logicalMessage = LogicalMessage.PreludeChoice;
                    kind = DuelWireKind.Control;
                    return true;
                case PreludeResultMessage:
                    logicalMessage = LogicalMessage.PreludeResult;
                    kind = DuelWireKind.Control;
                    return true;
                default:
                    return false;
            }
        }

        private static bool LogicalKindMatches(
            LogicalMessage logicalMessage,
            DuelWireKind kind)
        {
            switch (logicalMessage)
            {
                case LogicalMessage.Hello:
                    return kind == DuelWireKind.Deck;
                case LogicalMessage.State:
                    return kind == DuelWireKind.State;
                case LogicalMessage.Start:
                    return kind == DuelWireKind.Start;
                case LogicalMessage.Response:
                    return kind == DuelWireKind.Response;
                case LogicalMessage.PresentationEvent:
                    return kind == DuelWireKind.State;
                case LogicalMessage.HelloRequest:
                case LogicalMessage.HelloAccepted:
                case LogicalMessage.HelloRejected:
                case LogicalMessage.ClientReady:
                case LogicalMessage.StateAck:
                case LogicalMessage.ResyncRequest:
                case LogicalMessage.MatchReward:
                case LogicalMessage.BeginDuel:
                case LogicalMessage.Prelude:
                case LogicalMessage.PreludeChoice:
                case LogicalMessage.PreludeResult:
                    return kind == DuelWireKind.Control;
                default:
                    return false;
            }
        }

        private static bool WireMetadataMatches(
            DuelWirePacket ack,
            DuelWirePacket data)
        {
            return ack != null && data != null &&
                   ack.TransferId == data.TransferId &&
                   ack.Kind == data.Kind &&
                   ack.TotalLength == data.TotalLength &&
                   ack.ChunkCount == data.ChunkCount &&
                   ack.PayloadChecksum == data.PayloadChecksum;
        }

        private void RemoveSupersededStateTransfers(ulong target)
        {
            var removals = new List<WireTransferKey>();
            foreach (KeyValuePair<WireTransferKey, OutboundWireTransfer> pair in
                     outboundWireTransfers)
            {
                if (pair.Value.Target == target &&
                    pair.Value.LogicalMessage == LogicalMessage.State)
                {
                    removals.Add(pair.Key);
                }
            }
            foreach (WireTransferKey key in removals)
                outboundWireTransfers.Remove(key);
        }

        private void RemoveOldestInboundWireTransfer()
        {
            bool found = false;
            WireTransferKey oldestKey = default;
            float oldestTime = float.MaxValue;
            foreach (KeyValuePair<WireTransferKey, InboundWireTransfer> pair in
                     inboundWireTransfers)
            {
                if (pair.Value.LastActivityTime >= oldestTime)
                    continue;
                found = true;
                oldestKey = pair.Key;
                oldestTime = pair.Value.LastActivityTime;
            }
            if (found)
                inboundWireTransfers.Remove(oldestKey);
        }

        private void CleanupWireTransfers(float now)
        {
            var incomplete = new List<WireTransferKey>();
            foreach (KeyValuePair<WireTransferKey, InboundWireTransfer> pair in
                     inboundWireTransfers)
            {
                if (now - pair.Value.LastActivityTime >
                    WireAssemblyTimeoutSeconds)
                {
                    incomplete.Add(pair.Key);
                }
            }
            foreach (WireTransferKey key in incomplete)
                inboundWireTransfers.Remove(key);

            var receipts = new List<WireTransferKey>();
            foreach (KeyValuePair<WireTransferKey, CompletedWireTransfer> pair in
                     completedWireTransfers)
            {
                if (now - pair.Value.CompletedTime >
                    WireReceiptLifetimeSeconds)
                {
                    receipts.Add(pair.Key);
                }
            }
            foreach (WireTransferKey key in receipts)
                completedWireTransfers.Remove(key);
        }

        private void StartArenaTransitionAfterBlack()
        {
            if (localSceneLoadRequested)
                return;
            if (sceneTransitionRoutine != null)
                StopCoroutine(sceneTransitionRoutine);
            sceneTransitionRoutine = StartCoroutine(OpenArenaAfterBlack());
        }

        private IEnumerator OpenArenaAfterBlack()
        {
            loadingPresenter?.ShowDuelLoading(
                "Carregando duelo...",
                "Preparando o campo online.",
                0.10f);
            float blackDeadline = Time.realtimeSinceStartup + 2f;
            while (loadingPresenter != null && !loadingPresenter.IsOpaque &&
                   Time.realtimeSinceStartup < blackDeadline)
            {
                yield return null;
            }

            sceneTransitionRoutine = null;
            if (!matchStarted || localSceneLoadRequested)
                yield break;
            localSceneLoadRequested = true;
            SetFlowState(OnlineMatchFlowState.LoadingDuel);
            OpenDuelArena();
        }

        private void OpenDuelArena()
        {
            if (SceneManager.GetActiveScene().name != DuelArenaScene)
            {
                DuelOnlineBridge.BeginOnlineArenaTransition();
                SceneManager.LoadScene(DuelArenaScene);
            }
        }

        private void StopConnectionCoroutines()
        {
            if (arenaAttachRetry != null)
            {
                StopCoroutine(arenaAttachRetry);
                arenaAttachRetry = null;
            }
            if (helloRetry != null)
            {
                StopCoroutine(helloRetry);
                helloRetry = null;
            }
            if (helloRequestRetry != null)
            {
                StopCoroutine(helloRequestRetry);
                helloRequestRetry = null;
            }
            if (startRetry != null)
            {
                StopCoroutine(startRetry);
                startRetry = null;
            }
            if (stateHeartbeat != null)
            {
                StopCoroutine(stateHeartbeat);
                stateHeartbeat = null;
            }
            if (pendingStateBroadcast != null)
            {
                StopCoroutine(pendingStateBroadcast);
                pendingStateBroadcast = null;
            }
            if (sceneTransitionRoutine != null)
            {
                StopCoroutine(sceneTransitionRoutine);
                sceneTransitionRoutine = null;
            }
            if (beginDuelRoutine != null)
            {
                StopCoroutine(beginDuelRoutine);
                beginDuelRoutine = null;
            }
            if (preludeResultRoutine != null)
            {
                StopCoroutine(preludeResultRoutine);
                preludeResultRoutine = null;
            }
            if (reconnectCoroutine != null)
            {
                StopCoroutine(reconnectCoroutine);
                reconnectCoroutine = null;
            }
            if (hostReconnectGraceCoroutine != null)
            {
                StopCoroutine(hostReconnectGraceCoroutine);
                hostReconnectGraceCoroutine = null;
            }
            CancelRankedBotFallback();
        }

        private void ResetMatchState(bool clearLocalLoadout)
        {
            DuelOnlineBridge.CompleteOnlineArenaTransition();
            StopConnectionCoroutines();
            if (hostController != null)
                hostController.CoreEventPresented -= OnHostCoreEvent;
            hostController = null;
            replicaController = null;
            pendingReplicaState = null;
            if (clearLocalLoadout)
                localLoadout = null;
            remoteLoadout = null;
            remoteDuelIdentity = null;
            remoteClientId = ulong.MaxValue;
            currentMatchId = string.Empty;
            currentTransitionEpoch = 0;
            readinessBarrier.Reset();
            nextStateSequence = 0;
            lastReplicaSequence = 0;
            nextPresentationEventSequence = 0;
            lastPresentationEventSequence = 0;
            pendingResponseRequestId = 0;
            pendingResponseBytes = null;
            pendingCommandId = 0;
            pendingClientSequence = 0;
            pendingExpectedStateVersion = 0;
            nextClientCommandId = 0;
            nextClientSequence = 0;
            nextResponseRetryTime = 0f;
            pendingResponseStartedAt = 0f;
            nextPendingResponseResyncTime = 0f;
            lastAcknowledgedResponseRequestId = 0;
            lastAcknowledgedCommandId = 0;
            lastAcceptedClientSequence = 0;
            lastAcceptedCommandPayloadHash = 0;
            authoritativeStateVersion = 0;
            authoritativePublicStateHash = 0;
            lastReplicaStateVersion = 0;
            lastReplicaPublicStateHash = 0;
            lastStateAckVersion = 0;
            clientSynchronizing = false;
            commandTokens = CommandBurstCapacity;
            lastCommandTokenTime = Time.realtimeSinceStartup;
            nextClientResyncTime = 0f;
            nextHostResyncTime = 0f;
            reconnecting = false;
            hostAwaitingReconnect = false;
            hostAwaitingStateAckUnlock = false;
            hostAwaitingLiveStateAck = false;
            reconnectDeadline = 0f;
            matchStarted = false;
            hostCoreStarted = false;
            helloAccepted = false;
            clientDeckReady = false;
            clientReceivedStart = false;
            clientArenaReady = false;
            localSceneReady = false;
            localSceneLoadRequested = false;
            beginDuelReceived = false;
            beginDuelApplied = false;
            clientBeginApplied = false;
            onlinePreludeRound = 0;
            hostPreludeChoice = DuelPreludeChoice.None;
            clientPreludeChoice = DuelPreludeChoice.None;
            onlineStartingPlayer = 0;
            onlinePreludeResolved = false;
            diagnosticPreludeBypass = false;
            nextClientArenaReadyRetryTime = 0f;
            hostPlayerDisplayName = string.Empty;
            hostDeckDisplayName = string.Empty;
            outboundWireTransfers.Clear();
            inboundWireTransfers.Clear();
            completedWireTransfers.Clear();
            pendingPresentationEvents.Clear();
            outgoingPresentationEvents.Clear();
            nextWirePumpTime = 0f;
            nextWireCleanupTime = 0f;
            hostRewardDamage = 0;
            clientRewardDamage = 0;
            hostStatisticsDamageDealt = 0;
            hostStatisticsDamageReceived = 0;
            clientStatisticsDamageDealt = 0;
            clientStatisticsDamageReceived = 0;
            completedRewardRounds = 0;
            currentRewardTurnPlayer = -1;
            rewardPlayerZeroTurnEnded = false;
            rewardPlayerOneTurnEnded = false;
            matchRewardFinalized = false;
            nextResultSequence = 0;
            lastAppliedResultSequence = 0;
            pendingTerminalEvent = null;
            lastAuthoritativeResult = null;
            localRewardEligibilityAtMatchStart = null;
            localRankHandshake = null;
            remoteRankHandshake = null;
            sealedRankedMatch = null;
            localRankResultReceipt = null;
            automaticRankedMatchmaking = false;
            rankedBotFallbackInProgress = false;
            rankedBotFallbackDeadline = 0f;
            rewardResultMessage = string.Empty;
            rewardResultVisibleUntil = 0f;
        }

        private async Task LeaveRoomAsync()
        {
            bool reopenPanel = showPanel;
            ClearReconnectTicket();
            status = "Saindo da sala e liberando o Relay...";
            roomCode = string.Empty;
            relayRegion = string.Empty;
            relayRegionDescription = string.Empty;
            disconnectReason = string.Empty;
            role = SessionRole.None;
            ResetMatchState(true);
            UnregisterHandlers();
            try
            {
                await sessionCoordinator.LeaveAsync();
                bool transportStopped =
                    await EnsureNetworkStoppedAfterLeaveAsync();
                status = transportStopped
                    ? "Sala encerrada. Voce ja pode criar ou entrar em outra sala."
                    : "Sala encerrada. O transporte ainda esta finalizando; aguarde um instante.";
            }
            catch (Exception exception)
            {
                status = "A sala foi limpa localmente, mas o servico respondeu: " +
                    exception.GetBaseException().Message;
                Debug.LogWarning("[MP] stage=explicit-leave result=" +
                    exception.GetBaseException().Message);
            }
            finally
            {
                ClearTournamentDuelContext();
                connectionOperationInProgress = false;
                focusJoinCode = false;
                requestJoinFocus = false;
                joinCode = string.Empty;
                showPanel = reopenPanel &&
                    SceneManager.GetActiveScene().name != DuelArenaScene;
            }
        }

        private async Task<bool> EnsureNetworkStoppedAfterLeaveAsync()
        {
            if (networkManager == null)
                return true;
            if ((networkManager.IsClient || networkManager.IsServer) &&
                !networkManager.ShutdownInProgress)
            {
                networkManager.Shutdown();
            }

            for (int attempt = 0; attempt < 120; attempt++)
            {
                if (!networkManager.IsClient && !networkManager.IsServer &&
                    !networkManager.ShutdownInProgress)
                {
                    return true;
                }
                await Task.Delay(25);
            }
            return !networkManager.IsClient && !networkManager.IsServer;
        }

        private async void ResetAfterFailedConnection(string failure)
        {
            ClearReconnectTicket();
            status = failure;
            roomCode = string.Empty;
            relayRegion = string.Empty;
            relayRegionDescription = string.Empty;
            role = SessionRole.None;
            ResetMatchState(true);
            disconnectReason = failure;
            UnregisterHandlers();
            if (sessionCoordinator.HasSession)
            {
                await sessionCoordinator.LeaveAsync();
            }
            else if (networkManager != null &&
                     (networkManager.IsClient || networkManager.IsServer) &&
                     !networkManager.ShutdownInProgress)
            {
                // Only sessions started outside the MPS facade may be stopped
                // manually. MPS-owned sessions stop NGO through LeaveAsync.
                networkManager.Shutdown();
            }
            await EnsureNetworkStoppedAfterLeaveAsync();
            ClearTournamentDuelContext();
        }

        private void ClearTournamentDuelContext()
        {
            activeTournamentContext = null;
            tournamentMetricsCollector = null;
            tournamentResultReportTask = null;
            tournamentLaunchRequested = false;
        }

        private static string DescribeJoinFailure(Exception exception)
        {
            string detail = exception?.GetBaseException().Message ??
                "falha desconhecida";
            if (detail.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0 ||
                detail.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Código da sala não encontrado no Relay. O anfitrião " +
                    "pode ter saído da sala; mantenha o jogo do anfitrião aberto " +
                    "e use um novo código criado neste mesmo Card12.";
            }
            return $"Não foi possível entrar na sala: {detail}";
        }

        private void OnGUI()
        {
            if (matchStarted &&
                SceneManager.GetActiveScene().name == DuelArenaScene)
            {
                showPanel = false;
            }
            DrawRewardResultOverlay();
            if (!showPanel)
                return;
            EnsureLobbyStyles();
            const float width = 640f;
            const float height = 500f;
            Color originalColor = GUI.color;
            Color accent = GetLobbyVisualAccent();
            GUI.color = new Color(0.002f, 0.008f, 0.016f, 0.96f);
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.08f);
            GUI.DrawTexture(
                new Rect(0f, Screen.height * 0.12f,
                    Screen.width, Mathf.Max(2f, Screen.height * 0.002f)),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(0f, Screen.height * 0.88f,
                    Screen.width, Mathf.Max(2f, Screen.height * 0.002f)),
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
            GUI.color = new Color(0f, 0f, 0f, 0.56f);
            GUI.DrawTexture(new Rect(area.x + 10f, area.y + 12f,
                area.width, area.height), Texture2D.whiteTexture);
            GUI.color = new Color(accent.r, accent.g, accent.b, 0.28f);
            GUI.DrawTexture(new Rect(area.x - 2f, area.y - 2f,
                area.width + 4f, area.height + 4f), Texture2D.whiteTexture);
            GUI.color = originalColor;
            GUI.backgroundColor = Color.white;
            GUI.ModalWindow(
                912701,
                area,
                DrawPanel,
                string.Empty,
                lobbyWindowStyle);
            GUI.backgroundColor = originalBackground;
            GUI.matrix = previousMatrix;
        }

        private void DrawRewardResultOverlay()
        {
            if (string.IsNullOrWhiteSpace(rewardResultMessage) ||
                Time.realtimeSinceStartup > rewardResultVisibleUntil)
            {
                return;
            }

            float width = Mathf.Min(760f, Screen.width - 32f);
            float height = Mathf.Clamp(Screen.height * 0.12f, 72f, 112f);
            var area = new Rect(
                (Screen.width - width) * 0.5f,
                Mathf.Max(18f, Screen.height * 0.04f),
                width,
                height);
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.Clamp(Screen.height / 38, 18, 30),
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.02f, 0.18f, 0.22f, 0.96f);
            GUI.Box(area, rewardResultMessage, style);
            GUI.backgroundColor = previousBackground;
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

            bool roomActive = IsOnlineDuelActive;
            bool automaticQueue = automaticRankedMatchmaking &&
                competitivePolicy == CompetitivePolicy.Ranked;
            bool rankedRoomSetup = rankedRoomCreationPanel &&
                competitivePolicy == CompetitivePolicy.Ranked &&
                !automaticQueue;
            Color accent = GetLobbyVisualAccent();
            string heading = automaticQueue
                ? "ENCONTRAR RIVAL"
                : competitivePolicy == CompetitivePolicy.Ranked
                    ? "MULTIPLAYER RANQUEADO"
                    : "MULTIPLAYER CASUAL";
            string eyebrow = automaticQueue
                ? "RANQUEADO  •  PAREAMENTO COMPETITIVO"
                : competitivePolicy == CompetitivePolicy.Ranked
                    ? "SALA PRIVADA  •  RESULTADO COMPETITIVO"
                    : "SALA PRIVADA  •  DUELO SEM ALTERAÇÃO DE ELO";

            GUI.Label(new Rect(margin, 22f, contentWidth, 38f),
                heading, lobbyHeadingStyle);
            GUI.Label(new Rect(margin, 58f, contentWidth, 20f),
                eyebrow, lobbyEyebrowStyle);
            GUI.Label(new Rect(margin, 77f, contentWidth, 25f),
                GetRelayLobbyInfo(),
                lobbySubheadingStyle);
            GUI.Box(new Rect(margin, 105f, contentWidth, 42f),
                GUIContent.none, lobbyStatusBoxStyle);
            GUI.Label(new Rect(margin + 12f, 107f, contentWidth - 24f, 38f),
                status ?? string.Empty, lobbyStatusStyle);

            string code = roomActive ? roomCode : string.Empty;
            GUI.Label(new Rect(margin, 151f, contentWidth, 30f),
                automaticQueue
                    ? "BUSCA AUTOMÁTICA  •  DECK VALIDADO"
                    : rankedRoomSetup
                        ? "CRIAR SALA RANQUEADA PRIVADA"
                    : $"CÓDIGO DA SALA  •  {(string.IsNullOrWhiteSpace(code) ? "—" : code)}",
                lobbyCodeStyle);

            if (!automaticQueue && !string.IsNullOrWhiteSpace(code))
            {
                GUI.backgroundColor = Color.Lerp(accent, Color.white, 0.08f);
                if (GUI.Button(new Rect(458f, 178f, 144f, 38f), "COPIAR", lobbyButtonStyle))
                    GUIUtility.systemCopyBuffer = code;
            }

            if (!roomActive)
            {
                if (automaticQueue)
                {
                    GUI.Label(
                        new Rect(margin, 188f, contentWidth, 58f),
                        "PROCURANDO UM JOGADOR COM A MESMA VERSÃO, " +
                        "REGRAS E BAN LIST...",
                        lobbyDeckStyle);
                }
                else if (rankedRoomSetup)
                {
                    GUI.Label(
                        new Rect(margin, 174f, contentWidth, 36f),
                        "CRIE UMA SALA RANQUEADA COM CODIGO. " +
                        "O RESULTADO ALTERA PE E ELO.",
                        lobbyDeckStyle);
                    bool canStartConnection = !connectionOperationInProgress &&
                        (networkManager == null ||
                         !networkManager.ShutdownInProgress);
                    GUI.enabled = canStartConnection;
                    GUI.backgroundColor = accent;
                    if (GUI.Button(
                            new Rect(margin, 212f, contentWidth, 42f),
                            "CRIAR SALA RANQUEADA",
                            lobbyButtonStyle))
                    {
                        BeginHosting();
                    }
                    GUI.enabled = true;
                }
                else
                {
                    GUI.Label(new Rect(margin, 180f, 260f, 22f),
                        "CODIGO PARA ENTRAR", lobbySmallLabelStyle);
                    if (requestJoinFocus)
                    {
                        GUI.SetNextControlName("ArcaneJoinCode");
                        requestJoinFocus = false;
                    }
                    bool canStartConnection = !connectionOperationInProgress &&
                        (networkManager == null ||
                         !networkManager.ShutdownInProgress);
                    GUI.enabled = canStartConnection;
                    joinCode = GUI.TextField(
                        new Rect(margin, 204f, 412f, 42f),
                        joinCode ?? string.Empty,
                        lobbyInputStyle).Trim().ToUpperInvariant();
                    if (focusJoinCode)
                        GUI.FocusControl("ArcaneJoinCode");

                    GUI.backgroundColor = accent;
                    if (GUI.Button(
                            new Rect(464f, 204f, 138f, 42f),
                            "CRIAR SALA",
                            lobbyButtonStyle))
                    {
                        BeginHosting();
                    }
                    GUI.backgroundColor = Color.Lerp(accent, Color.white, 0.14f);
                    if (GUI.Button(
                            new Rect(464f, 254f, 138f, 42f),
                            "ENTRAR",
                            lobbyButtonStyle))
                    {
                        BeginJoining();
                    }
                    GUI.enabled = true;
                }
            }

            GUI.backgroundColor = Color.white;
            GUI.Box(new Rect(margin, 264f, 412f, 92f),
                GUIContent.none, lobbySectionStyle);
            bool peerConnected = IsHost
                ? remoteClientId != ulong.MaxValue
                : role == SessionRole.Client && networkManager != null &&
                  networkManager.IsConnectedClient;
            int players = role == SessionRole.None
                ? 0
                : peerConnected ? 2 : 1;
            int confirmedDecks = localLoadout == null ? 0 : 1;
            if (IsHost ? remoteLoadout != null && clientDeckReady : helloAccepted)
                confirmedDecks++;
            string roleLabel = IsHost ? "ANFITRIAO / JOGADOR 1" :
                role == SessionRole.Client ? "JOGADOR 2" : "DESCONECTADO";
            GUI.Label(new Rect(54f, 274f, 380f, 22f),
                $"JOGADORES • {players}/2    DECKS • {confirmedDecks}/2    {roleLabel}",
                lobbySmallLabelStyle);
            string localDeck = localLoadout?.displayName ?? "AGUARDANDO";
            string playerOneDeck = IsHost
                ? localDeck
                : string.IsNullOrWhiteSpace(hostDeckDisplayName)
                    ? "AGUARDANDO"
                    : hostDeckDisplayName;
            string playerTwoDeck = IsHost
                ? remoteLoadout?.displayName ?? "AGUARDANDO"
                : localDeck;
            GUI.Label(new Rect(54f, 300f, 380f, 42f),
                $"DECK JOGADOR 1  •  {playerOneDeck}\n" +
                $"DECK JOGADOR 2  •  {playerTwoDeck}",
                lobbyDeckStyle);

            bool canStartMatch = IsHost && peerConnected &&
                remoteLoadout != null && clientDeckReady && !matchStarted;
            string matchButtonLabel = matchStarted
                ? "DUELO ONLINE EM ANDAMENTO"
                : automaticQueue && canStartMatch
                    ? "INICIANDO DUELO RANQUEADO"
                : canStartMatch
                    ? "INICIAR DUELO ONLINE"
                    : roomActive
                        ? "AGUARDANDO JOGADORES E DECKS"
                        : automaticQueue
                            ? "BUSCANDO RIVAL RANQUEADO"
                            : "CRIE OU ENTRE EM UMA SALA";
            GUI.enabled = canStartMatch && !automaticQueue;
            GUI.backgroundColor = GUI.enabled
                ? accent
                : new Color(0.24f, 0.27f, 0.30f, 1f);
            if (GUI.Button(new Rect(margin, 366f, contentWidth, 42f),
                    matchButtonLabel, lobbyButtonStyle))
            {
                BeginHostMatch();
            }
            GUI.enabled = true;

            if (roomActive)
            {
                GUI.backgroundColor = new Color(0.95f, 0.25f, 0.35f, 1f);
                if (GUI.Button(new Rect(margin, 420f, 220f, 34f),
                        matchStarted
                            ? "ENCERRAR PARTIDA"
                            : automaticQueue ? "SAIR DA FILA" : "SAIR DA SALA",
                        lobbyButtonStyle))
                {
                    LeaveRoom();
                }
            }
            GUI.backgroundColor = new Color(0.28f, 0.38f, 0.48f, 1f);
            if (GUI.Button(new Rect(398f, 420f, 204f, 34f),
                    "FECHAR PAINEL", lobbyButtonStyle))
            {
                // Closing only hides this window. Leaving the Relay room is
                // always explicit through the red action beside it.
                if (roomActive)
                {
                    status = matchStarted
                        ? "A partida continua ativa. Use ENCERRAR PARTIDA para sair."
                        : automaticQueue
                        ? "A fila ranqueada continua ativa. Reabra o painel para acompanhar a busca."
                        : IsHost
                        ? "Sala continua ativa. Reabra o painel para copiar o código ou iniciar o duelo."
                        : "Conexão continua ativa. Aguarde o anfitrião iniciar o duelo.";
                }
                showPanel = false;
            }
            GUI.backgroundColor = Color.white;
        }

        private string GetRelayLobbyInfo()
        {
            if (!IsOnlineDuelActive)
            {
                return "ONLINE v11 • Sessions escolhe a melhor região Relay.";
            }

            int roundTrip = RelayRoundTripTimeMs;
            string rtt = roundTrip < 0
                ? "medindo RTT..."
                : $"RTT real: {roundTrip} ms";
            return $"ONLINE v11 • Relay: {GetRelayRegionLabel()}  •  {rtt}";
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
        private GUIStyle lobbyEyebrowStyle;
        private GUIStyle lobbySubheadingStyle;
        private GUIStyle lobbyStatusStyle;
        private GUIStyle lobbyCodeStyle;
        private GUIStyle lobbySmallLabelStyle;
        private GUIStyle lobbyDeckStyle;
        private GUIStyle lobbyInputStyle;
        private GUIStyle lobbyButtonStyle;
        private GUIStyle lobbyWindowStyle;
        private GUIStyle lobbySectionStyle;
        private GUIStyle lobbyStatusBoxStyle;
        private int lobbyVisualThemeKey = int.MinValue;

        private void EnsureLobbyStyles()
        {
            int themeKey = GetLobbyVisualThemeKey();
            if (lobbyHeadingStyle != null &&
                lobbyVisualThemeKey == themeKey)
                return;

            lobbyVisualThemeKey = themeKey;
            Color accent = GetLobbyVisualAccent();
            Texture2D windowTexture = CreateLobbySurfaceTexture(
                accent, false, 0.98f, 128);
            Texture2D sectionTexture = CreateLobbySurfaceTexture(
                accent, false, 0.78f, 64);
            Texture2D raisedTexture = CreateLobbySurfaceTexture(
                accent, true, 0.96f, 64);
            Texture2D pressedTexture = CreateLobbySurfaceTexture(
                Color.Lerp(accent, Color.white, 0.24f),
                true,
                1f,
                64);

            lobbyWindowStyle = new GUIStyle(GUI.skin.window)
            {
                border = new RectOffset(14, 14, 14, 14),
                normal = { background = windowTexture }
            };
            lobbySectionStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(9, 9, 9, 9),
                normal = { background = sectionTexture }
            };
            lobbyStatusBoxStyle = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(9, 9, 9, 9),
                normal = { background = sectionTexture }
            };

            lobbyHeadingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            lobbyEyebrowStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                normal =
                {
                    textColor = new Color(
                        accent.r, accent.g, accent.b, 0.94f)
                }
            };
            lobbySubheadingStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                normal = { textColor = new Color(0.66f, 0.75f, 0.82f, 1f) }
            };
            lobbyStatusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal =
                {
                    textColor = Color.Lerp(accent, Color.white, 0.26f)
                }
            };
            lobbyCodeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.Lerp(accent, Color.white, 0.15f) }
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
                padding = new RectOffset(14, 12, 8, 8),
                border = new RectOffset(9, 9, 9, 9),
                normal =
                {
                    textColor = Color.white,
                    background = sectionTexture
                },
                focused =
                {
                    textColor = Color.white,
                    background = raisedTexture
                }
            };
            lobbyButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                border = new RectOffset(9, 9, 9, 9),
                normal =
                {
                    textColor = Color.white,
                    background = raisedTexture
                },
                hover =
                {
                    textColor = Color.white,
                    background = pressedTexture
                },
                active =
                {
                    textColor = Color.white,
                    background = pressedTexture
                },
                focused =
                {
                    textColor = Color.white,
                    background = raisedTexture
                }
            };
        }

        private int GetLobbyVisualThemeKey()
        {
            if (automaticRankedMatchmaking &&
                competitivePolicy == CompetitivePolicy.Ranked)
            {
                return 1;
            }
            return competitivePolicy == CompetitivePolicy.Ranked ? 3 : 2;
        }

        private Color GetLobbyVisualAccent()
        {
            return GetLobbyVisualThemeKey() switch
            {
                1 => new Color(0.20f, 0.48f, 1f, 1f),
                2 => new Color(1f, 0.30f, 0.16f, 1f),
                _ => new Color(0.62f, 0.32f, 1f, 1f)
            };
        }

        private static Texture2D CreateLobbySurfaceTexture(
            Color accent,
            bool raised,
            float opacity,
            int size)
        {
            int dimension = Mathf.Max(16, size);
            var texture = new Texture2D(
                dimension,
                dimension,
                TextureFormat.RGBA32,
                false)
            {
                name = raised
                    ? "Duel Mode Raised Surface"
                    : "Duel Mode Panel Surface",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[dimension * dimension];
            float cut = dimension * (raised ? 0.11f : 0.065f);
            float border = Mathf.Max(1.4f, dimension * 0.025f);
            for (int y = 0; y < dimension; y++)
            {
                float ny = y / (dimension - 1f);
                for (int x = 0; x < dimension; x++)
                {
                    float cornerDistance = Mathf.Min(
                        x + y,
                        Mathf.Min(
                            (dimension - 1 - x) + y,
                            Mathf.Min(
                                x + (dimension - 1 - y),
                                (dimension - 1 - x) +
                                (dimension - 1 - y))));
                    if (cornerDistance < cut)
                    {
                        pixels[y * dimension + x] = Color.clear;
                        continue;
                    }

                    float edgeDistance = Mathf.Min(
                        Mathf.Min(x, dimension - 1 - x),
                        Mathf.Min(y, dimension - 1 - y));
                    bool diagonalEdge = cornerDistance < cut + border * 1.8f;
                    bool isBorder = edgeDistance < border || diagonalEdge;
                    if (isBorder)
                    {
                        pixels[y * dimension + x] = new Color(
                            accent.r,
                            accent.g,
                            accent.b,
                            (raised ? 0.82f : 0.54f) * opacity);
                        continue;
                    }

                    Color bottom = new(0.003f, 0.012f, 0.023f, 0.98f * opacity);
                    Color top = new(0.030f, 0.065f, 0.090f, 0.94f * opacity);
                    Color fill = Color.Lerp(bottom, top, ny);
                    float sheen = Mathf.Clamp01(
                        1f - Mathf.Abs((x / (dimension - 1f) + ny) - 1.42f) * 7f);
                    fill = Color.Lerp(
                        fill,
                        new Color(accent.r, accent.g, accent.b, fill.a),
                        sheen * (raised ? 0.16f : 0.06f));
                    pixels[y * dimension + x] = fill;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
