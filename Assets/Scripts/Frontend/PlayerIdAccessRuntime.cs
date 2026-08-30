using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using ArcaneDuel.Game.Accounts;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Autentica o jogador, abre sua presença no catálogo por ID e mantém um
    /// heartbeat. O cliente nunca contém uma lista privilegiada de IDs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerIdAccessRuntime : MonoBehaviour
    {
        [Serializable]
        private sealed class Settings
        {
            public int schemaVersion = 1;
            public bool enabled;
            public string baseUrl;
            public int heartbeatSeconds = 60;
            public int requestTimeoutSeconds = 10;
            // Online, ranqueado e economia dependem do catálogo autoritativo.
            // O jogo local continua acessível quando a rede está indisponível,
            // mas nunca devemos tratar uma conta não verificada como liberada
            // para recursos que precisam respeitar bloqueios por ID.
            public bool allowOnlineWhenCatalogUnavailable;

            public void Normalize()
            {
                schemaVersion = Math.Max(1, schemaVersion);
                baseUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
                heartbeatSeconds = Mathf.Clamp(heartbeatSeconds, 20, 600);
                requestTimeoutSeconds = Mathf.Clamp(
                    requestTimeoutSeconds,
                    3,
                    30);
            }
        }

        [Serializable]
        private sealed class PresenceRequest
        {
            public int schemaVersion = 1;
            public int publicProfileSchemaVersion;
            public long publicProfileRevisionUtcMilliseconds;
            public string sessionId;
            public string playerId;
            public string publicId;
            public string playerDisplayName;
            public string equippedIconId;
            public int rankedPoints;
            public long duelsPlayed;
            public long wins;
            public long losses;
            public long draws;
            public int privateProfileSchemaVersion;
            public long privateProfileRevisionUtcMilliseconds;
            public int coinBalance;
            public int ownedIconCount;
            public int ownedArtworkCount;
            public int ownedCardCopies;
            public int uniqueCardCount;
            public int deckCount;
            public int unlockedDeckCount;
            public int craftPointsN;
            public int craftPointsR;
            public int craftPointsSR;
            public int craftPointsUR;
            public string equippedArtworkId;
            public string buildVersion;
            public string platform;
        }

        private const string SettingsResourcePath =
            "AccountControl/PlayerIdAccessSettings";
        private static PlayerIdAccessRuntime _instance;
        private static Task<PlayerIdAccessSnapshot> _readyTask;
        private static bool _hasAuthoritativeUtc;
        private static long _authoritativeUtcUnixSeconds;
        private static float _authoritativeRealtimeAtCapture;
        private static string _authoritativeClockSource = string.Empty;

        private Settings _settings;
        private PlayerIdAccessSnapshot _snapshot;
        private string _sessionId;
        private string _playerDisplayName;
        private string _equippedIconId = ProfileIconCatalog.DefaultIconId;
        private int _rankedPoints;
        private long _duelsPlayed;
        private long _wins;
        private long _losses;
        private long _draws;
        private int _coinBalance;
        private int _ownedIconCount;
        private int _ownedArtworkCount;
        private int _ownedCardCopies;
        private int _uniqueCardCount;
        private int _deckCount;
        private int _unlockedDeckCount;
        private int _craftPointsN;
        private int _craftPointsR;
        private int _craftPointsSR;
        private int _craftPointsUR;
        private string _equippedArtworkId =
            ProfileArtworkCatalog.DefaultArtworkId;
        private long _publicProfileRevisionUtcMilliseconds;
        private bool _publicProfileReadyForUpload;
        private bool _authenticationEventsSubscribed;
        private Coroutine _heartbeat;

        public static event Action<PlayerIdAccessSnapshot> AccessChanged;

        public static PlayerIdAccessSnapshot Current =>
            _instance?._snapshot?.Copy();
        public static string CanonicalPlayerId =>
            _instance?._snapshot?.playerId ?? string.Empty;
        public static string PublicPlayerId =>
            !string.IsNullOrWhiteSpace(_instance?._snapshot?.publicId)
                ? _instance._snapshot.publicId
                : "ID INDISPONÍVEL";
        public static bool IsCatalogConfigured =>
            _instance?._settings?.enabled == true &&
            !string.IsNullOrWhiteSpace(_instance._settings.baseUrl);
        public static string CatalogBaseUrl =>
            _instance?._settings?.baseUrl ?? string.Empty;
        public static int CatalogRequestTimeoutSeconds =>
            _instance?._settings?.requestTimeoutSeconds ?? 10;

        public static bool TryGetAuthoritativeUtc(
            out long utcUnixSeconds,
            out string source)
        {
            utcUnixSeconds = 0;
            source = string.Empty;
            if (!_hasAuthoritativeUtc || _authoritativeUtcUnixSeconds <= 0)
                return false;
            float elapsed = Mathf.Max(
                0f,
                Time.realtimeSinceStartup - _authoritativeRealtimeAtCapture);
            utcUnixSeconds = _authoritativeUtcUnixSeconds +
                             (long)Math.Floor(elapsed);
            source = _authoritativeClockSource;
            return true;
        }

        public static async Task<bool> ValidateAuthoritativeTimeAsync()
        {
            EnsureRuntimeExists();
            await EnsureReadyAsync();
            if (_instance == null || _instance._settings?.enabled != true ||
                string.IsNullOrWhiteSpace(_instance._settings.baseUrl))
            {
                _hasAuthoritativeUtc = false;
                return false;
            }
            try
            {
                await _instance.RefreshFromCatalogAsync("heartbeat");
                return TryGetAuthoritativeUtc(out _, out _);
            }
            catch
            {
                _hasAuthoritativeUtc = false;
                return false;
            }
        }

        /// <summary>
        /// Acesso a serviços remotos só é seguro enquanto a sessão Unity ainda
        /// possui um token autorizado e não expirado. IsSignedIn isoladamente
        /// não basta: ele pode continuar verdadeiro durante a transição de uma
        /// sessão expirada.
        /// </summary>
        public static bool HasAuthorizedSession
        {
            get
            {
                try
                {
                    return UnityServices.State ==
                               ServicesInitializationState.Initialized &&
                           AuthenticationService.Instance.IsSignedIn &&
                           AuthenticationService.Instance.IsAuthorized &&
                           !AuthenticationService.Instance.IsExpired;
                }
                catch
                {
                    return false;
                }
            }
        }

        // Os pacotes UGS (inclusive Friends) registram suas dependências em
        // BeforeSceneLoad. Criar este runtime no mesmo estágio podia iniciar
        // UnityServices cedo demais e deixar o Friends fora do CoreRegistry.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeExists()
        {
            if (_instance != null)
                return;
            var root = new GameObject("Catálogo de Jogadores por ID");
            root.AddComponent<PlayerIdAccessRuntime>();
        }

        public static Task<PlayerIdAccessSnapshot> EnsureReadyAsync()
        {
            EnsureRuntimeExists();
            return _readyTask ?? Task.FromResult(Current);
        }

        public static bool Allows(
            string capability,
            out string rejection)
        {
            rejection = string.Empty;
            EnsureRuntimeExists();

            _instance.EnsureSnapshotHasNotExpired();

            bool unverifiedOnline = _instance._settings != null &&
                                     _instance._settings
                                         .allowOnlineWhenCatalogUnavailable;
            bool isOnlineCapability = string.Equals(
                                          capability,
                                          PlayerIdCapability.Online,
                                          StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(
                                          capability,
                                          PlayerIdCapability.Ranked,
                                          StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(
                                          capability,
                                          PlayerIdCapability.Economy,
                                          StringComparison.OrdinalIgnoreCase);
            bool allowWhenUnverified = !isOnlineCapability || unverifiedOnline;
            bool allowed = PlayerIdAccessPolicy.AllowsStandardCapability(
                _instance._snapshot,
                capability,
                allowWhenUnverified);
            if (allowed)
                return true;

            rejection = !string.IsNullOrWhiteSpace(
                _instance._snapshot?.message)
                ? _instance._snapshot.message
                : "Este recurso não está disponível para o ID desta conta.";
            return false;
        }

        public static bool HasFeature(string feature)
        {
            EnsureRuntimeExists();
            _instance.EnsureSnapshotHasNotExpired();
            return PlayerIdAccessPolicy.HasGrantedFeature(
                _instance?._snapshot,
                feature);
        }

        public static void SetPlayerDisplayName(string displayName)
        {
            EnsureRuntimeExists();
            _instance._playerDisplayName = (displayName ?? string.Empty)
                .Trim();
        }

        public static void SetPlayerPublicProfile(
            string equippedIconId,
            int rankedPoints,
            long duelsPlayed,
            long wins,
            long losses,
            long draws,
            int coinBalance,
            int ownedIconCount,
            int ownedArtworkCount,
            int ownedCardCopies,
            int uniqueCardCount,
            int deckCount,
            int unlockedDeckCount,
            int craftPointsN,
            int craftPointsR,
            int craftPointsSR,
            int craftPointsUR,
            string equippedArtworkId,
            long revisionUtcMilliseconds,
            bool readyForUpload)
        {
            EnsureRuntimeExists();
            _instance._equippedIconId = ProfileIconCatalog.ResolveId(
                equippedIconId);
            _instance._rankedPoints = Mathf.Clamp(rankedPoints, 0, 200);
            _instance._duelsPlayed = Math.Max(0, duelsPlayed);
            _instance._wins = Math.Max(0, wins);
            _instance._losses = Math.Max(0, losses);
            _instance._draws = Math.Max(0, draws);
            _instance._coinBalance = Math.Max(0, coinBalance);
            _instance._ownedIconCount = Math.Max(0, ownedIconCount);
            _instance._ownedArtworkCount = Math.Max(0, ownedArtworkCount);
            _instance._ownedCardCopies = Math.Max(0, ownedCardCopies);
            _instance._uniqueCardCount = Math.Max(0, uniqueCardCount);
            _instance._deckCount = Math.Max(0, deckCount);
            _instance._unlockedDeckCount = Math.Max(0, unlockedDeckCount);
            _instance._craftPointsN = Math.Max(0, craftPointsN);
            _instance._craftPointsR = Math.Max(0, craftPointsR);
            _instance._craftPointsSR = Math.Max(0, craftPointsSR);
            _instance._craftPointsUR = Math.Max(0, craftPointsUR);
            _instance._equippedArtworkId = ProfileArtworkCatalog.ResolveId(
                equippedArtworkId);
            _instance._publicProfileRevisionUtcMilliseconds = Math.Max(
                0,
                revisionUtcMilliseconds);
            if (readyForUpload)
                _instance._publicProfileReadyForUpload = true;
        }

        public static async Task RefreshNowAsync()
        {
            await EnsureReadyAsync();
            if (_instance != null)
                await _instance.RefreshFromCatalogAsync("open");
        }

        public static async Task<PlayerIdAccessSnapshot>
            RebindCurrentAuthenticationAsync()
        {
            EnsureRuntimeExists();
            if (!HasAuthorizedSession)
            {
                throw new InvalidOperationException(
                    "A sessão da Unity não está autorizada. Entre novamente " +
                    "para usar recursos online.");
            }

            _instance.SubscribeAuthenticationEvents();
            _instance._sessionId = Guid.NewGuid().ToString("N");
            _instance._publicProfileReadyForUpload = false;
            _instance._publicProfileRevisionUtcMilliseconds = 0;
            _instance.SetSnapshot(
                PlayerIdAccessPolicy.CreateUnverifiedFallback(
                    AuthenticationService.Instance.PlayerId));
            if (_instance._settings.enabled &&
                !string.IsNullOrWhiteSpace(_instance._settings.baseUrl))
            {
                try
                {
                    await _instance.RefreshFromCatalogAsync("open");
                }
                finally
                {
                    _instance.StartHeartbeatIfNeeded();
                }
            }
            return Current;
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
            _settings = LoadSettings();
            _sessionId = Guid.NewGuid().ToString("N");
            _snapshot = PlayerIdAccessPolicy.CreateUnverifiedFallback(
                string.Empty);
            _readyTask = InitializeAsync();
        }

        private async Task<PlayerIdAccessSnapshot> InitializeAsync()
        {
            try
            {
                await EnsureUnityAuthenticationAsync();
                SubscribeAuthenticationEvents();
                SetSnapshot(PlayerIdAccessPolicy.CreateUnverifiedFallback(
                    AuthenticationService.Instance.PlayerId));
                if (_settings.enabled &&
                    !string.IsNullOrWhiteSpace(_settings.baseUrl))
                {
                    try
                    {
                        await RefreshFromCatalogAsync("open");
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning(
                            "O catálogo de jogadores não pôde ser consultado " +
                            "neste momento. O jogo tentará novamente sem " +
                            "liberar recursos online: " +
                            exception.GetBaseException().Message);
                    }
                    StartHeartbeatIfNeeded();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "O catálogo de jogadores não pôde ser consultado. " +
                    "Privilégios exclusivos permanecerão bloqueados: " +
                    exception.GetBaseException().Message);
            }
            return Current;
        }

        private static async Task EnsureUnityAuthenticationAsync()
        {
            float initializationStartedAt = Time.realtimeSinceStartup;
            while (UnityServices.State ==
                   ServicesInitializationState.Initializing)
            {
                if (Time.realtimeSinceStartup - initializationStartedAt > 30f)
                {
                    throw new TimeoutException(
                        "Os serviços da Unity não concluíram a inicialização.");
                }
                await Task.Yield();
            }

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                string profile = CommandLineValue("-arcaneAuthProfile");
                if (string.IsNullOrWhiteSpace(profile))
                {
                    await UnityServices.InitializeAsync();
                }
                else
                {
                    var options = new InitializationOptions();
                    options.SetProfile(profile.Trim());
                    await UnityServices.InitializeAsync(options);
                }
            }

            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                throw new InvalidOperationException(
                    "Os serviços da Unity não estão inicializados.");
            }

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            if (!HasAuthorizedSession)
            {
                throw new InvalidOperationException(
                    "A sessão da Unity expirou. Entre novamente para " +
                    "restaurar os recursos online.");
            }
        }

        private IEnumerator HeartbeatLoop()
        {
            var delay = new WaitForSecondsRealtime(_settings.heartbeatSeconds);
            while (true)
            {
                yield return delay;
                Task task = RefreshFromCatalogAsync("heartbeat");
                while (!task.IsCompleted)
                    yield return null;
                if (task.IsFaulted)
                {
                    Debug.LogWarning(
                        "[Catálogo de jogadores] A autorização online não " +
                        "pôde ser renovada. Tentará novamente enquanto a " +
                        "sessão permanecer válida.");
                    if (!HasAuthorizedSession)
                    {
                        _heartbeat = null;
                        yield break;
                    }
                }
            }
        }

        private void StartHeartbeatIfNeeded()
        {
            if (_heartbeat == null && _settings.enabled &&
                !string.IsNullOrWhiteSpace(_settings.baseUrl) &&
                HasAuthorizedSession)
            {
                _heartbeat = StartCoroutine(HeartbeatLoop());
            }
        }

        private async Task RefreshFromCatalogAsync(string operation)
        {
            if (!_settings.enabled ||
                string.IsNullOrWhiteSpace(_settings.baseUrl))
            {
                return;
            }
            if (!HasAuthorizedSession)
            {
                InvalidateServerAuthorization(
                    "A sessão da conta expirou. Entre novamente para usar " +
                    "recursos online.");
                throw new InvalidOperationException(
                    "A sessão da Unity não está autorizada.");
            }
            try
            {
                string playerId = AuthenticationService.Instance.PlayerId ??
                                  string.Empty;
                var payload = new PresenceRequest
                {
                    publicProfileSchemaVersion =
                        PlayerIdAccessPolicy.PublicProfileUploadSchemaVersion(
                            _publicProfileReadyForUpload),
                    publicProfileRevisionUtcMilliseconds =
                        _publicProfileReadyForUpload
                            ? _publicProfileRevisionUtcMilliseconds
                            : 0,
                    sessionId = _sessionId,
                    playerId = playerId,
                    publicId = PlayerIdAccessPolicy.FormatPublicId(playerId),
                    playerDisplayName = _playerDisplayName,
                    equippedIconId = _equippedIconId,
                    rankedPoints = _rankedPoints,
                    duelsPlayed = _duelsPlayed,
                    wins = _wins,
                    losses = _losses,
                    draws = _draws,
                    privateProfileSchemaVersion =
                        _publicProfileReadyForUpload ? 1 : 0,
                    privateProfileRevisionUtcMilliseconds =
                        _publicProfileReadyForUpload
                            ? _publicProfileRevisionUtcMilliseconds
                            : 0,
                    coinBalance = _coinBalance,
                    ownedIconCount = _ownedIconCount,
                    ownedArtworkCount = _ownedArtworkCount,
                    ownedCardCopies = _ownedCardCopies,
                    uniqueCardCount = _uniqueCardCount,
                    deckCount = _deckCount,
                    unlockedDeckCount = _unlockedDeckCount,
                    craftPointsN = _craftPointsN,
                    craftPointsR = _craftPointsR,
                    craftPointsSR = _craftPointsSR,
                    craftPointsUR = _craftPointsUR,
                    equippedArtworkId = _equippedArtworkId,
                    buildVersion = Application.version ?? string.Empty,
                    platform = Application.platform.ToString()
                };
                string json = JsonUtility.ToJson(payload);
                string url = $"{_settings.baseUrl}/v1/player/{operation}";

                using var request = new UnityWebRequest(url, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(
                        Encoding.UTF8.GetBytes(json)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = _settings.requestTimeoutSeconds
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader(
                    "Authorization",
                    "Bearer " + AuthenticationService.Instance.AccessToken);

                UnityWebRequestAsyncOperation send = request.SendWebRequest();
                while (!send.isDone)
                    await Task.Yield();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new InvalidOperationException(
                        $"Catálogo respondeu HTTP {request.responseCode}: " +
                        request.error);
                }

                CaptureAuthoritativeUtc(request.GetResponseHeader("Date"));

                PlayerIdAccessSnapshot response = JsonUtility.FromJson<
                    PlayerIdAccessSnapshot>(request.downloadHandler.text);
                if (response == null ||
                    !string.Equals(
                        response.playerId,
                        playerId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "O catálogo devolveu um registro de outro jogador.");
                }

                long now = TryGetAuthoritativeUtc(
                    out long serverNow,
                    out _)
                    ? serverNow
                    : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (response.validUntilUtcUnixSeconds <= now)
                {
                    throw new InvalidOperationException(
                        "O catálogo devolveu uma autorização expirada.");
                }

                response.serverVerified = true;
                response.Normalize();
                SetSnapshot(response);
            }
            catch
            {
                _hasAuthoritativeUtc = false;
                InvalidateServerAuthorization(
                    "A autorização online não pôde ser confirmada. " +
                    "Recursos online permanecerão bloqueados até a reconexão.",
                    stopHeartbeat: false);
                throw;
            }
        }

        private static void CaptureAuthoritativeUtc(string httpDate)
        {
            if (string.IsNullOrWhiteSpace(httpDate) ||
                !DateTimeOffset.TryParse(
                    httpDate,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset parsed))
            {
                return;
            }
            _authoritativeUtcUnixSeconds = parsed.ToUnixTimeSeconds();
            _authoritativeRealtimeAtCapture = Time.realtimeSinceStartup;
            _authoritativeClockSource = "HTTP Date · catálogo de jogadores";
            _hasAuthoritativeUtc = _authoritativeUtcUnixSeconds > 0;
        }

        private void SubscribeAuthenticationEvents()
        {
            if (_authenticationEventsSubscribed)
                return;
            AuthenticationService.Instance.Expired += HandleAuthenticationExpired;
            AuthenticationService.Instance.SignedOut += HandleAuthenticationSignedOut;
            _authenticationEventsSubscribed = true;
        }

        private void UnsubscribeAuthenticationEvents()
        {
            if (!_authenticationEventsSubscribed)
                return;
            try
            {
                AuthenticationService.Instance.Expired -= HandleAuthenticationExpired;
                AuthenticationService.Instance.SignedOut -= HandleAuthenticationSignedOut;
            }
            catch
            {
                // O encerramento dos serviços da Unity não deve gerar erro
                // durante a destruição da cena/aplicativo.
            }
            _authenticationEventsSubscribed = false;
        }

        private void HandleAuthenticationExpired()
        {
            InvalidateServerAuthorization(
                "A sessão da conta expirou. Entre novamente para usar " +
                "recursos online.");
        }

        private void HandleAuthenticationSignedOut()
        {
            InvalidateServerAuthorization(
                "A sessão da conta foi encerrada. Entre novamente para usar " +
                "recursos online.");
        }

        private void EnsureSnapshotHasNotExpired()
        {
            if (_snapshot == null || !_snapshot.serverVerified)
                return;
            long validUntil = _snapshot.validUntilUtcUnixSeconds;
            if (validUntil > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                return;
            InvalidateServerAuthorization(
                "A autorização online expirou. Reconecte sua conta para " +
                "continuar usando recursos online.");
        }

        private void InvalidateServerAuthorization(
            string message,
            bool stopHeartbeat = true)
        {
            if (stopHeartbeat && _heartbeat != null)
            {
                StopCoroutine(_heartbeat);
                _heartbeat = null;
            }
            string playerId = _snapshot?.playerId ?? string.Empty;
            var fallback = PlayerIdAccessPolicy.CreateUnverifiedFallback(playerId);
            fallback.message = (message ?? string.Empty).Trim();
            SetSnapshot(fallback);
        }

        private void SetSnapshot(PlayerIdAccessSnapshot snapshot)
        {
            _snapshot = snapshot ??
                        PlayerIdAccessPolicy.CreateUnverifiedFallback(
                            string.Empty);
            _snapshot.Normalize();
            AccessChanged?.Invoke(_snapshot.Copy());
        }

        private static Settings LoadSettings()
        {
            TextAsset asset = Resources.Load<TextAsset>(SettingsResourcePath);
            Settings settings = asset == null
                ? new Settings()
                : JsonUtility.FromJson<Settings>(asset.text) ?? new Settings();
            settings.Normalize();
            return settings;
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

        private void OnDestroy()
        {
            UnsubscribeAuthenticationEvents();
            if (_heartbeat != null)
                StopCoroutine(_heartbeat);
            if (_instance == this)
            {
                _instance = null;
                _readyTask = null;
            }
        }
    }
}
