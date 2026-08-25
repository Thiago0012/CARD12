using System;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using UnityEngine;

namespace ArcaneArena.Frontend
{
    public enum PlayerCloudSaveState
    {
        Offline,
        Synchronizing,
        Synchronized,
        Conflict,
        Error
    }

    /// <summary>
    /// Mantém uma cópia recuperável do perfil vinculada ao PlayerId real da
    /// Unity. O JSON local continua sendo o cache offline e nunca é apagado
    /// quando a rede falha.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCloudSaveRuntime : MonoBehaviour
    {
        [Serializable]
        private sealed class Settings
        {
            public int schemaVersion = 1;
            public bool enabled = true;
            public string playerFileName = "arcane-arena-profile-v1.json";
            public float uploadDebounceSeconds = 2.5f;
            public int requestTimeoutSeconds = 30;

            public void Normalize()
            {
                schemaVersion = Math.Max(1, schemaVersion);
                playerFileName = string.IsNullOrWhiteSpace(playerFileName)
                    ? "arcane-arena-profile-v1.json"
                    : playerFileName.Trim();
                uploadDebounceSeconds = Mathf.Clamp(
                    uploadDebounceSeconds,
                    0.5f,
                    30f);
                requestTimeoutSeconds = Mathf.Clamp(
                    requestTimeoutSeconds,
                    5,
                    120);
            }
        }

        private const string SettingsResourcePath =
            "AccountControl/PlayerCloudSaveSettings";
        private static PlayerCloudSaveRuntime _instance;
        private static Task _initialSyncTask;

        private Settings _settings;
        private DeckRepository _repository;
        private Coroutine _pendingUpload;
        private string _writeLock;
        private string _cloudPlayerId;
        private bool _applyingRemote;
        private readonly SemaphoreSlim _operationGate = new(1, 1);

        public static event Action Changed;
        public static PlayerCloudSaveState State { get; private set; } =
            PlayerCloudSaveState.Offline;
        public static string Status { get; private set; } =
            "Save local disponível.";
        public static long LastSynchronizedUtcTicks { get; private set; }
        public static bool HasLocalProfile =>
            _instance?._repository?.HasPlayerProfile == true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeExists()
        {
            if (_instance != null)
                return;
            var root = new GameObject("Sincronização Segura da Conta");
            root.AddComponent<PlayerCloudSaveRuntime>();
        }

        public static void Attach(DeckRepository repository)
        {
            EnsureRuntimeExists();
            _instance.AttachRepository(repository);
        }

        public static Task EnsureSynchronizedAsync()
        {
            EnsureRuntimeExists();
            return _initialSyncTask ??=
                _instance.SynchronizeAsync(false);
        }

        public static async Task ReloadForCurrentAccountAsync()
        {
            EnsureRuntimeExists();
            _instance.CancelPendingUpload();
            _initialSyncTask = _instance.SynchronizeAsync(true);
            await _initialSyncTask;
        }

        public static async Task UploadNowAsync()
        {
            EnsureRuntimeExists();
            await _instance.UploadCurrentAsync();
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
        }

        private void AttachRepository(DeckRepository repository)
        {
            if (ReferenceEquals(_repository, repository))
                return;
            if (_repository != null)
                _repository.LocalSaveCommitted -= HandleLocalSave;
            _repository = repository;
            if (_repository != null)
                _repository.LocalSaveCommitted += HandleLocalSave;
            _initialSyncTask = null;
        }

        private void HandleLocalSave()
        {
            if (_applyingRemote || !_settings.enabled)
                return;
            if (_pendingUpload != null)
                StopCoroutine(_pendingUpload);
            _pendingUpload = StartCoroutine(UploadAfterDelay());
        }

        private IEnumerator UploadAfterDelay()
        {
            yield return new WaitForSecondsRealtime(
                _settings.uploadDebounceSeconds);
            _pendingUpload = null;
            Task upload = UploadCurrentAsync();
            while (!upload.IsCompleted)
                yield return null;
            if (upload.IsFaulted)
            {
                Debug.LogWarning(
                    "[Cloud Save] " +
                    upload.Exception?.GetBaseException().Message);
            }
        }

        private async Task SynchronizeAsync(bool forceRemote)
        {
            await _operationGate.WaitAsync();
            try
            {
                await SynchronizeCoreAsync(forceRemote);
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task SynchronizeCoreAsync(bool forceRemote)
        {
            if (!_settings.enabled || _repository == null)
            {
                SetState(
                    PlayerCloudSaveState.Offline,
                    "Sincronização na nuvem desativada; usando save local.");
                return;
            }

            SetState(
                PlayerCloudSaveState.Synchronizing,
                "Sincronizando os dados da conta...");
            try
            {
                await PlayerIdAccessRuntime.EnsureReadyAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    throw new InvalidOperationException(
                        "A conta ainda não foi autenticada.");

                string playerId = AuthenticationService.Instance.PlayerId;
                if (!string.Equals(
                        _cloudPlayerId,
                        playerId,
                        StringComparison.Ordinal))
                {
                    _cloudPlayerId = playerId;
                    _writeLock = string.Empty;
                }

                var files = await CloudSaveService.Instance.Files.Player
                    .ListAllAsync();
                FileItem remoteFile = files.FirstOrDefault(file =>
                    string.Equals(
                        file.Key,
                        _settings.playerFileName,
                        StringComparison.Ordinal));
                if (remoteFile == null)
                {
                    if (!_repository.TryBindAuthenticatedPlayerId(
                            playerId,
                            out string bindRejection))
                    {
                        throw new InvalidOperationException(bindRejection);
                    }
                    await UploadCurrentCoreAsync();
                    return;
                }

                _writeLock = remoteFile.WriteLock ?? string.Empty;
                byte[] bytes = await CloudSaveService.Instance.Files.Player
                    .LoadBytesAsync(_settings.playerFileName);
                string remoteJson = Encoding.UTF8.GetString(bytes);
                DeckCollectionState remoteState = JsonUtility.FromJson<
                    DeckCollectionState>(remoteJson);
                if (remoteState == null)
                    throw new InvalidOperationException(
                        "O arquivo de perfil da nuvem está vazio.");

                bool localBelongsToAnotherAccount =
                    !string.IsNullOrWhiteSpace(
                        _repository.AuthenticatedPlayerId) &&
                    !string.Equals(
                        _repository.AuthenticatedPlayerId,
                        playerId,
                        StringComparison.Ordinal);
                bool remoteIsNewer =
                    remoteState.lastModifiedUtcTicks >
                    (_repository.State?.lastModifiedUtcTicks ?? 0);
                bool shouldRestore = forceRemote ||
                                     localBelongsToAnotherAccount ||
                                     !_repository.HasPlayerProfile ||
                                     remoteIsNewer;
                if (shouldRestore)
                {
                    _applyingRemote = true;
                    try
                    {
                        if (!_repository.TryImportCloudJson(
                                remoteJson,
                                playerId,
                                out string importRejection))
                        {
                            throw new InvalidOperationException(
                                importRejection);
                        }
                    }
                    finally
                    {
                        _applyingRemote = false;
                    }
                }
                else if ((_repository.State?.lastModifiedUtcTicks ?? 0) >
                         remoteState.lastModifiedUtcTicks)
                {
                    await UploadCurrentCoreAsync();
                    return;
                }

                LastSynchronizedUtcTicks = DateTime.UtcNow.Ticks;
                SetState(
                    PlayerCloudSaveState.Synchronized,
                    "Conta e progresso sincronizados.");
            }
            catch (CloudSaveConflictException exception)
            {
                SetState(
                    PlayerCloudSaveState.Conflict,
                    "O perfil foi alterado em outro aparelho. Sincronize novamente.");
                Debug.LogWarning("[Cloud Save conflito] " + exception.Message);
            }
            catch (Exception exception)
            {
                SetState(
                    PlayerCloudSaveState.Error,
                    "Nuvem indisponível; seu save local foi preservado.");
                Debug.LogWarning(
                    "[Cloud Save] " +
                    exception.GetBaseException().Message);
            }
        }

        private async Task UploadCurrentAsync()
        {
            await _operationGate.WaitAsync();
            try
            {
                await UploadCurrentCoreAsync();
            }
            catch (CloudSaveConflictException exception)
            {
                SetState(
                    PlayerCloudSaveState.Conflict,
                    "O perfil foi alterado em outro aparelho. Sincronize novamente.");
                Debug.LogWarning("[Cloud Save conflito] " + exception.Message);
                throw;
            }
            catch (Exception exception)
            {
                SetState(
                    PlayerCloudSaveState.Error,
                    "Nuvem indisponível; seu save local foi preservado.");
                Debug.LogWarning(
                    "[Cloud Save] " + exception.GetBaseException().Message);
                throw;
            }
            finally
            {
                _operationGate.Release();
            }
        }

        private async Task UploadCurrentCoreAsync()
        {
            if (!_settings.enabled || _repository?.State == null ||
                !AuthenticationService.Instance.IsSignedIn)
            {
                return;
            }

            SetState(
                PlayerCloudSaveState.Synchronizing,
                "Salvando progresso na nuvem...");
            string playerId = AuthenticationService.Instance.PlayerId;
            if (!string.Equals(
                    _repository.AuthenticatedPlayerId,
                    playerId,
                    StringComparison.Ordinal))
            {
                if (!_repository.TryBindAuthenticatedPlayerId(
                        playerId,
                        out string rejection))
                {
                    throw new InvalidOperationException(rejection);
                }
            }

            _repository.State.cloudRevision = Math.Max(
                0,
                _repository.State.cloudRevision) + 1;
            _repository.Save(false);
            byte[] bytes = Encoding.UTF8.GetBytes(
                _repository.ExportJson(true));
            var options = new Unity.Services.CloudSave.SaveOptions
            {
                RequestTimeout = _settings.requestTimeoutSeconds
            };
            if (!string.IsNullOrWhiteSpace(_writeLock) &&
                string.Equals(_cloudPlayerId, playerId, StringComparison.Ordinal))
            {
                options.WriteLock = _writeLock;
            }

            await CloudSaveService.Instance.Files.Player.SaveAsync(
                _settings.playerFileName,
                bytes,
                options);
            FileItem metadata = await CloudSaveService.Instance.Files.Player
                .GetMetadataAsync(_settings.playerFileName);
            _writeLock = metadata?.WriteLock ?? string.Empty;
            _cloudPlayerId = playerId;
            LastSynchronizedUtcTicks = DateTime.UtcNow.Ticks;
            SetState(
                PlayerCloudSaveState.Synchronized,
                "Conta e progresso sincronizados.");
        }

        private void CancelPendingUpload()
        {
            if (_pendingUpload == null)
                return;
            StopCoroutine(_pendingUpload);
            _pendingUpload = null;
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused || !_settings.enabled || _repository == null)
                return;
            CancelPendingUpload();
            _ = UploadCurrentAsync();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused || !_settings.enabled || _repository == null)
                return;
            _initialSyncTask = SynchronizeAsync(false);
        }

        private void OnApplicationQuit()
        {
            CancelPendingUpload();
            if (_settings.enabled && _repository != null)
                _ = UploadCurrentAsync();
        }

        private static void SetState(
            PlayerCloudSaveState state,
            string status)
        {
            State = state;
            Status = status ?? string.Empty;
            Changed?.Invoke();
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

        private void OnDestroy()
        {
            if (_repository != null)
                _repository.LocalSaveCommitted -= HandleLocalSave;
            if (_instance == this)
            {
                _instance = null;
                _initialSyncTask = null;
            }
        }
    }
}
