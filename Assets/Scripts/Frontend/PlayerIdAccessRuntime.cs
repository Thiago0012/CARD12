using System;
using System.Collections;
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
            public bool allowOnlineWhenCatalogUnavailable = true;

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
            public string buildVersion;
            public string platform;
        }

        private const string SettingsResourcePath =
            "AccountControl/PlayerIdAccessSettings";
        private static PlayerIdAccessRuntime _instance;
        private static Task<PlayerIdAccessSnapshot> _readyTask;

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
        private long _publicProfileRevisionUtcMilliseconds;
        private bool _publicProfileReadyForUpload;
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

            bool unverifiedOnline = _instance._settings == null ||
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
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                throw new InvalidOperationException(
                    "Nenhuma conta da Unity está autenticada.");
            }

            _instance._sessionId = Guid.NewGuid().ToString("N");
            _instance._publicProfileReadyForUpload = false;
            _instance._publicProfileRevisionUtcMilliseconds = 0;
            _instance.SetSnapshot(
                PlayerIdAccessPolicy.CreateUnverifiedFallback(
                    AuthenticationService.Instance.PlayerId));
            if (_instance._settings.enabled &&
                !string.IsNullOrWhiteSpace(_instance._settings.baseUrl))
            {
                await _instance.RefreshFromCatalogAsync("open");
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
                SetSnapshot(PlayerIdAccessPolicy.CreateUnverifiedFallback(
                    AuthenticationService.Instance.PlayerId));
                if (_settings.enabled &&
                    !string.IsNullOrWhiteSpace(_settings.baseUrl))
                {
                    await RefreshFromCatalogAsync("open");
                    _heartbeat = StartCoroutine(HeartbeatLoop());
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
            }
        }

        private async Task RefreshFromCatalogAsync(string operation)
        {
            if (!_settings.enabled ||
                string.IsNullOrWhiteSpace(_settings.baseUrl) ||
                !AuthenticationService.Instance.IsSignedIn)
            {
                return;
            }

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

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (response.validUntilUtcUnixSeconds > 0 &&
                response.validUntilUtcUnixSeconds <= now)
            {
                throw new InvalidOperationException(
                    "O catálogo devolveu uma autorização expirada.");
            }

            response.serverVerified = true;
            response.Normalize();
            SetSnapshot(response);
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
