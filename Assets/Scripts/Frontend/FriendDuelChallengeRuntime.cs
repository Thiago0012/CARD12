using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcaneArena.Multiplayer;
using ArcaneDuel.Game.Accounts;
using ArcaneDuel.Game.Competitive;
using ArcaneDuel.Game.Social;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Negocia convites privados entre amigos pelo catálogo autenticado. O
    /// código Relay nunca passa pela interface: depois da aceitação, apenas
    /// as duas contas do desafio o recebem e entram na sessão existente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FriendDuelChallengeRuntime : MonoBehaviour
    {
        [Serializable]
        private sealed class CreateChallengeRequest
        {
            public string recipientPlayerId;
            public string duelMode;
            public string clientRequestId;
        }

        [Serializable]
        private sealed class RoomCodeRequest
        {
            public string roomCode;
        }

        [Serializable]
        private sealed class ErrorResponse
        {
            public string message;
        }

        private const float PollIntervalSeconds = 3f;
        private const float SessionLaunchTimeoutSeconds = 35f;
        private static FriendDuelChallengeRuntime _instance;
        private static Task _readyTask;

        private FriendDuelChallengeView _incoming;
        private FriendDuelChallengeView _outgoing;
        private Coroutine _pollLoop;
        private bool _initialized;
        private bool _busy;
        private bool _pollInProgress;
        private bool _bridgeMutationInProgress;
        private string _status = "Preparando convites de duelo...";
        private string _hostStartedChallengeId = string.Empty;
        private string _joinStartedChallengeId = string.Empty;
        private string _roomPublishedChallengeId = string.Empty;
        private string _joinedReportedChallengeId = string.Empty;
        private float _hostStartedAt;
        private float _joinStartedAt;
        private int _identityGeneration;

        public static event Action Changed;

        public static bool IsReady => _instance?._initialized == true;
        public static bool IsBusy => _instance?._busy == true;
        public static string Status =>
            _instance?._status ?? "Convites de duelo ainda não iniciados.";
        public static int IncomingCount =>
            _instance?._incoming?.IsActive == true ? 1 : 0;
        public static FriendDuelChallengeView Incoming =>
            _instance?._incoming?.Copy();
        public static FriendDuelChallengeView Outgoing =>
            _instance?._outgoing?.Copy();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (_instance != null)
                return;
            var root = new GameObject("Convites Privados de Duelo");
            root.AddComponent<FriendDuelChallengeRuntime>();
        }

        public static Task EnsureReadyAsync()
        {
            EnsureExists();
            return _readyTask ?? Task.CompletedTask;
        }

        public static async Task RebindCurrentAuthenticationAsync()
        {
            EnsureExists();
            if (_readyTask != null)
            {
                try
                {
                    await _readyTask;
                }
                catch
                {
                    // Uma inicialização antiga não pode impedir o novo login.
                }
            }

            _instance.ResetForIdentityChange();
            _readyTask = _instance.InitializeAsync();
            await _readyTask;
        }

        public static async Task ChallengeAsync(
            FriendProfileView friend,
            FriendDuelMode mode)
        {
            await EnsureReadyAsync();
            RequireReady();
            if (friend == null || string.IsNullOrWhiteSpace(friend.playerId))
                throw new InvalidOperationException("O amigo selecionado é inválido.");
            bool isFriend = PlayerFriendsRuntime.Friends.Any(candidate =>
                candidate != null && string.Equals(
                    candidate.playerId,
                    friend.playerId,
                    StringComparison.Ordinal));
            if (!isFriend ||
                friend.connectionState != FriendConnectionState.Friend)
            {
                throw new InvalidOperationException(
                    "Somente amigos confirmados podem receber desafios.");
            }
            RequireDuelCapability(mode);
            RequireNoOnlineDuel();
            if (_instance._incoming?.IsActive == true ||
                _instance._outgoing?.IsActive == true)
            {
                throw new InvalidOperationException(
                    "Conclua ou cancele o desafio atual antes de criar outro.");
            }

            var payload = new CreateChallengeRequest
            {
                recipientPlayerId = friend.playerId,
                duelMode = FriendDuelChallengePolicy.SerializeMode(mode),
                clientRequestId = Guid.NewGuid().ToString("N")
            };
            await _instance.RunMutationAsync(
                "/v1/duel/challenges",
                JsonUtility.ToJson(payload));
        }

        public static async Task AcceptAsync(string challengeId)
        {
            await EnsureReadyAsync();
            RequireReady();
            FriendDuelChallengeView challenge = _instance._incoming;
            if (challenge == null ||
                !string.Equals(
                    challenge.challengeId,
                    challengeId,
                    StringComparison.Ordinal) ||
                !FriendDuelChallengePolicy.CanAccept(
                    challenge,
                    PlayerIdAccessRuntime.CanonicalPlayerId,
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            {
                throw new InvalidOperationException(
                    "Este convite expirou ou já recebeu uma resposta.");
            }
            RequireDuelCapability(challenge.Mode);
            RequireNoOnlineDuel();
            await _instance.RunMutationAsync(
                ChallengeActionPath(challengeId, "accept"),
                "{}");
        }

        public static async Task DeclineAsync(string challengeId)
        {
            await EnsureReadyAsync();
            RequireReady();
            await _instance.RunMutationAsync(
                ChallengeActionPath(challengeId, "decline"),
                "{}");
        }

        public static async Task CancelAsync(string challengeId)
        {
            await EnsureReadyAsync();
            RequireReady();
            await _instance.RunMutationAsync(
                ChallengeActionPath(challengeId, "cancel"),
                "{}");
        }

        public static async Task RefreshNowAsync()
        {
            await EnsureReadyAsync();
            if (_instance != null)
                await _instance.RefreshStateAsync(false);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _readyTask = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                await PlayerIdAccessRuntime.EnsureReadyAsync();
                if (!PlayerIdAccessRuntime.IsCatalogConfigured)
                {
                    _status = "O servidor de convites de duelo não está configurado.";
                    return;
                }
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    _status = "A conta ainda não foi autenticada.";
                    return;
                }
                _initialized = true;
                await RefreshStateAsync(true);
                _pollLoop = StartCoroutine(PollLoop());
            }
            catch (Exception exception)
            {
                _status = DescribeException(exception);
                Debug.LogWarning("[Desafios de amigos] " + _status);
            }
            finally
            {
                NotifyChanged();
            }
        }

        private void ResetForIdentityChange()
        {
            _identityGeneration++;
            if (_pollLoop != null)
            {
                StopCoroutine(_pollLoop);
                _pollLoop = null;
            }
            _initialized = false;
            _busy = false;
            _pollInProgress = false;
            _bridgeMutationInProgress = false;
            _incoming = null;
            _outgoing = null;
            _hostStartedChallengeId = string.Empty;
            _joinStartedChallengeId = string.Empty;
            _roomPublishedChallengeId = string.Empty;
            _joinedReportedChallengeId = string.Empty;
            _status = "Reconectando convites à conta restaurada...";
            NotifyChanged();
        }

        private IEnumerator PollLoop()
        {
            var delay = new WaitForSecondsRealtime(PollIntervalSeconds);
            while (true)
            {
                yield return delay;
                Task task = RefreshStateAsync(true);
                while (!task.IsCompleted)
                    yield return null;
            }
        }

        private void Update()
        {
            if (!_initialized || _bridgeMutationInProgress)
                return;
            ProcessSessionBridge();
        }

        private void ProcessSessionBridge()
        {
            DuelOnlineSession session = DuelOnlineSession.EnsureInstance();
            if (_outgoing?.Status == FriendDuelChallengeStatus.Accepted &&
                !string.Equals(
                    _hostStartedChallengeId,
                    _outgoing.challengeId,
                    StringComparison.Ordinal))
            {
                CompetitivePolicy policy = CompetitivePolicyFor(_outgoing.Mode);
                if (!session.BeginFriendChallengeHosting(
                        policy,
                        out string rejection))
                {
                    _status = rejection;
                    _ = CancelAfterBridgeFailureAsync(
                        _outgoing.challengeId,
                        rejection);
                    return;
                }
                _hostStartedChallengeId = _outgoing.challengeId;
                _hostStartedAt = Time.realtimeSinceStartup;
                _status = "Desafio aceito. Criando a sala privada...";
                NotifyChanged();
            }

            if (_outgoing != null &&
                string.Equals(
                    _hostStartedChallengeId,
                    _outgoing.challengeId,
                    StringComparison.Ordinal) &&
                _outgoing.Status == FriendDuelChallengeStatus.Accepted)
            {
                if (!string.IsNullOrWhiteSpace(session.RoomCode) &&
                    !string.Equals(
                        _roomPublishedChallengeId,
                        _outgoing.challengeId,
                        StringComparison.Ordinal))
                {
                    _ = PublishRoomAsync(
                        _outgoing.challengeId,
                        session.RoomCode);
                }
                else if (!session.IsOnlineDuelActive &&
                         !session.ConnectionOperationInProgress &&
                         Time.realtimeSinceStartup - _hostStartedAt >
                         SessionLaunchTimeoutSeconds)
                {
                    _ = CancelAfterBridgeFailureAsync(
                        _outgoing.challengeId,
                        session.Status);
                }
            }

            if (_incoming?.Status == FriendDuelChallengeStatus.Ready &&
                !string.Equals(
                    _joinStartedChallengeId,
                    _incoming.challengeId,
                    StringComparison.Ordinal))
            {
                CompetitivePolicy policy = CompetitivePolicyFor(_incoming.Mode);
                if (!session.BeginFriendChallengeJoining(
                        _incoming.roomCode,
                        policy,
                        out string rejection))
                {
                    _status = rejection;
                    _ = CancelAfterBridgeFailureAsync(
                        _incoming.challengeId,
                        rejection);
                    return;
                }
                _joinStartedChallengeId = _incoming.challengeId;
                _joinStartedAt = Time.realtimeSinceStartup;
                _status = "Sala localizada. Entrando no duelo privado...";
                NotifyChanged();
            }

            if (_incoming != null &&
                string.Equals(
                    _joinStartedChallengeId,
                    _incoming.challengeId,
                    StringComparison.Ordinal) &&
                _incoming.Status == FriendDuelChallengeStatus.Ready)
            {
                if (session.IsOnlineDuelActive && !session.IsHost &&
                    !string.Equals(
                        _joinedReportedChallengeId,
                        _incoming.challengeId,
                        StringComparison.Ordinal))
                {
                    _ = ReportJoinedAsync(_incoming.challengeId);
                }
                else if (!session.IsOnlineDuelActive &&
                         !session.ConnectionOperationInProgress &&
                         Time.realtimeSinceStartup - _joinStartedAt >
                         SessionLaunchTimeoutSeconds)
                {
                    _ = CancelAfterBridgeFailureAsync(
                        _incoming.challengeId,
                        session.Status);
                }
            }
        }

        private async Task RefreshStateAsync(bool background)
        {
            if (_pollInProgress || !_initialized)
                return;
            int identityGeneration = _identityGeneration;
            _pollInProgress = true;
            try
            {
                string priorSignature = StateSignature();
                string json = await SendAsync("/v1/duel/challenges", "GET", null);
                if (identityGeneration != _identityGeneration)
                    return;
                FriendDuelChallengeStateResponse response =
                    JsonUtility.FromJson<FriendDuelChallengeStateResponse>(json);
                if (response == null || response.schemaVersion < 1)
                    throw new InvalidOperationException(
                        "O servidor devolveu um estado de convite inválido.");
                ApplyState(response.incoming, response.outgoing);
                _status = response.message ?? string.Empty;
                if (!string.Equals(
                        priorSignature,
                        StateSignature(),
                        StringComparison.Ordinal))
                {
                    NotifyChanged();
                }
            }
            catch (Exception exception)
            {
                if (identityGeneration != _identityGeneration)
                    return;
                if (!background)
                    throw;
                _status = "Os convites serão sincronizados novamente: " +
                          DescribeException(exception);
                Debug.LogWarning("[Desafios de amigos] " + _status);
                NotifyChanged();
            }
            finally
            {
                if (identityGeneration == _identityGeneration)
                    _pollInProgress = false;
            }
        }

        private async Task RunMutationAsync(string path, string jsonBody)
        {
            if (_busy)
                throw new InvalidOperationException("Aguarde a operação atual.");
            _busy = true;
            _status = "Sincronizando o desafio...";
            NotifyChanged();
            try
            {
                string json = await SendAsync(path, "POST", jsonBody);
                FriendDuelChallengeMutationResponse response =
                    JsonUtility.FromJson<FriendDuelChallengeMutationResponse>(json);
                if (response?.challenge == null)
                    throw new InvalidOperationException(
                        "O servidor não confirmou o desafio.");
                ApplyMutation(response.challenge);
                _status = response.message ?? "Desafio sincronizado.";
            }
            catch (Exception exception)
            {
                _status = DescribeException(exception);
                throw new InvalidOperationException(_status, exception);
            }
            finally
            {
                _busy = false;
                NotifyChanged();
            }
        }

        private async Task PublishRoomAsync(string challengeId, string roomCode)
        {
            if (_bridgeMutationInProgress)
                return;
            _bridgeMutationInProgress = true;
            try
            {
                var body = new RoomCodeRequest { roomCode = roomCode };
                await RunBridgeMutationAsync(
                    ChallengeActionPath(challengeId, "room"),
                    JsonUtility.ToJson(body));
                _roomPublishedChallengeId = challengeId;
                _status = "Sala pronta. Aguardando o amigo entrar...";
            }
            catch (Exception exception)
            {
                _status = DescribeException(exception);
                Debug.LogWarning("[Desafios de amigos] " + _status);
            }
            finally
            {
                _bridgeMutationInProgress = false;
                NotifyChanged();
            }
        }

        private async Task ReportJoinedAsync(string challengeId)
        {
            if (_bridgeMutationInProgress)
                return;
            _bridgeMutationInProgress = true;
            try
            {
                await RunBridgeMutationAsync(
                    ChallengeActionPath(challengeId, "joined"),
                    "{}");
                _joinedReportedChallengeId = challengeId;
                _status = "Conexão confirmada. Iniciando o duelo...";
            }
            catch (Exception exception)
            {
                _status = DescribeException(exception);
                Debug.LogWarning("[Desafios de amigos] " + _status);
            }
            finally
            {
                _bridgeMutationInProgress = false;
                NotifyChanged();
            }
        }

        private async Task CancelAfterBridgeFailureAsync(
            string challengeId,
            string detail)
        {
            if (_bridgeMutationInProgress || string.IsNullOrWhiteSpace(challengeId))
                return;
            _bridgeMutationInProgress = true;
            try
            {
                await RunBridgeMutationAsync(
                    ChallengeActionPath(challengeId, "cancel"),
                    "{}");
                _status = string.IsNullOrWhiteSpace(detail)
                    ? "Não foi possível preparar a sala; o desafio foi cancelado."
                    : detail;
            }
            catch (Exception exception)
            {
                _status = DescribeException(exception);
            }
            finally
            {
                _bridgeMutationInProgress = false;
                NotifyChanged();
            }
        }

        private async Task RunBridgeMutationAsync(string path, string jsonBody)
        {
            string json = await SendAsync(path, "POST", jsonBody);
            FriendDuelChallengeMutationResponse response =
                JsonUtility.FromJson<FriendDuelChallengeMutationResponse>(json);
            if (response?.challenge == null)
                throw new InvalidOperationException(
                    "O servidor não confirmou a transição do duelo.");
            ApplyMutation(response.challenge);
        }

        private void ApplyMutation(FriendDuelChallengeView challenge)
        {
            string localPlayerId = PlayerIdAccessRuntime.CanonicalPlayerId;
            if (string.Equals(
                    challenge.senderPlayerId,
                    localPlayerId,
                    StringComparison.Ordinal))
            {
                _outgoing = challenge.IsActive ? challenge.Copy() : null;
            }
            if (string.Equals(
                    challenge.recipientPlayerId,
                    localPlayerId,
                    StringComparison.Ordinal))
            {
                _incoming = challenge.IsActive ? challenge.Copy() : null;
            }
            ResetBridgeTrackingForInactiveChallenges();
        }

        private void ApplyState(
            FriendDuelChallengeView incoming,
            FriendDuelChallengeView outgoing)
        {
            _incoming = incoming?.IsActive == true ? incoming.Copy() : null;
            _outgoing = outgoing?.IsActive == true ? outgoing.Copy() : null;
            ResetBridgeTrackingForInactiveChallenges();
        }

        private void ResetBridgeTrackingForInactiveChallenges()
        {
            if (_outgoing == null || !string.Equals(
                    _outgoing.challengeId,
                    _hostStartedChallengeId,
                    StringComparison.Ordinal))
            {
                _hostStartedChallengeId = string.Empty;
                _roomPublishedChallengeId = string.Empty;
            }
            if (_incoming == null || !string.Equals(
                    _incoming.challengeId,
                    _joinStartedChallengeId,
                    StringComparison.Ordinal))
            {
                _joinStartedChallengeId = string.Empty;
                _joinedReportedChallengeId = string.Empty;
            }
        }

        private string StateSignature()
        {
            return ChallengeSignature(_incoming) + "|" +
                   ChallengeSignature(_outgoing);
        }

        private static string ChallengeSignature(
            FriendDuelChallengeView challenge)
        {
            return challenge == null
                ? string.Empty
                : string.Join(
                    ":",
                    challenge.challengeId ?? string.Empty,
                    challenge.status ?? string.Empty,
                    challenge.duelMode ?? string.Empty,
                    challenge.roomCode ?? string.Empty,
                    challenge.updatedUtcUnixSeconds.ToString());
        }

        private static async Task<string> SendAsync(
            string path,
            string method,
            string jsonBody)
        {
            if (!AuthenticationService.Instance.IsSignedIn)
                throw new InvalidOperationException("A conta não está autenticada.");
            string url = PlayerIdAccessRuntime.CatalogBaseUrl + path;
            using var request = new UnityWebRequest(url, method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = PlayerIdAccessRuntime.CatalogRequestTimeoutSeconds
            };
            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(
                    Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            request.SetRequestHeader(
                "Authorization",
                "Bearer " + AuthenticationService.Instance.AccessToken);
            UnityWebRequestAsyncOperation send = request.SendWebRequest();
            while (!send.isDone)
                await Task.Yield();
            if (request.result != UnityWebRequest.Result.Success)
            {
                ErrorResponse error = null;
                try
                {
                    error = JsonUtility.FromJson<ErrorResponse>(
                        request.downloadHandler.text);
                }
                catch
                {
                    // Mantém a mensagem HTTP abaixo quando o corpo não é JSON.
                }
                string detail = !string.IsNullOrWhiteSpace(error?.message)
                    ? error.message
                    : request.error;
                throw new InvalidOperationException(
                    $"O servidor respondeu HTTP {request.responseCode}: {detail}");
            }
            return request.downloadHandler.text;
        }

        private static string ChallengeActionPath(
            string challengeId,
            string action)
        {
            string id = (challengeId ?? string.Empty).Trim().ToLowerInvariant();
            if (id.Length != 32 || id.Any(character =>
                    !Uri.IsHexDigit(character)))
            {
                throw new InvalidOperationException(
                    "A identificação do desafio é inválida.");
            }
            return $"/v1/duel/challenges/{id}/{action}";
        }

        private static CompetitivePolicy CompetitivePolicyFor(
            FriendDuelMode mode)
        {
            return mode == FriendDuelMode.Ranked
                ? CompetitivePolicy.Ranked
                : CompetitivePolicy.Unranked;
        }

        private static void RequireDuelCapability(FriendDuelMode mode)
        {
            string capability = mode == FriendDuelMode.Ranked
                ? PlayerIdCapability.Ranked
                : PlayerIdCapability.Online;
            if (!PlayerIdAccessRuntime.Allows(capability, out string rejection))
                throw new InvalidOperationException(rejection);
        }

        private static void RequireNoOnlineDuel()
        {
            if (DuelOnlineSession.Instance?.IsOnlineDuelActive == true)
            {
                throw new InvalidOperationException(
                    "Finalize a sessão online atual antes de desafiar um amigo.");
            }
        }

        private static void RequireReady()
        {
            if (!IsReady)
                throw new InvalidOperationException(Status);
        }

        private static string DescribeException(Exception exception)
        {
            return exception?.GetBaseException().Message ??
                   "Não foi possível sincronizar o desafio.";
        }

        private static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (_pollLoop != null)
                StopCoroutine(_pollLoop);
            if (_instance == this)
                _instance = null;
        }
    }
}
