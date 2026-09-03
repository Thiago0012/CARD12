using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ArcaneArena.Cards;
using ArcaneDuel.DuelEngine.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    [Serializable]
    public sealed class RemoteReleaseEnvelope
    {
        public int schemaVersion = 2;
        public string keyId;
        public string signatureBase64;
        public RemoteReleaseManifest payload = new RemoteReleaseManifest();
    }

    [Serializable]
    public sealed class RemoteReleaseManifest
    {
        public int schemaVersion = 2;
        public string releaseId;
        public string publishedUtc;
        public long sequenceNumber = 1;
        public string channel = "production";
        public string expiresUtc;
        public int protocolVersion = 1;
        public string minimumClientVersion;
        public string latestClientVersion;
        public bool requiredClientUpdate;
        public string title;
        public string summary;
        public string[] changes = Array.Empty<string>();
        public string windowsUpdateUrl;
        public string androidUpdateUrl;
        public string fallbackUpdateUrl;
        public RemoteClientArtifact windows = new RemoteClientArtifact();
        public RemoteClientArtifact android = new RemoteClientArtifact();
        public string contentVersion;
        public bool requiredContentUpdate;
        public RemoteContentPackage[] packages =
            Array.Empty<RemoteContentPackage>();
    }

    [Serializable]
    public sealed class RemoteClientArtifact
    {
        public string platform;
        public string versionName;
        public int versionCode;
        public int minimumVersionCode;
        public int protocolVersion = 1;
        public string url;
        public long sizeBytes;
        public string sha256;
        public string signingCertificateSha256;
        public string executableName;

        public bool HasInstallableArtifact =>
            !string.IsNullOrWhiteSpace(url) &&
            sizeBytes > 0 &&
            !string.IsNullOrWhiteSpace(sha256);
    }

    [Serializable]
    public sealed class RemoteContentPackage
    {
        public string packageId;
        public string version;
        public string platform;
        public string target;
        public string url;
        public long sizeBytes;
        public string sha256;
    }

    /// <summary>
    /// Gate de entrada inspirado no fluxo de jogos de serviço: consulta a
    /// versão publicada antes do menu, bloqueia clientes incompatíveis e
    /// instala pacotes de conteúdo com validação e troca atômica.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RemoteUpdateRuntime : MonoBehaviour
    {
        [Serializable]
        private sealed class Settings
        {
            public int schemaVersion = 1;
            public bool enabled = true;
            public string manifestUrl;
            public string releaseChannel = "production";
            public int requestTimeoutSeconds = 15;
            public bool failOpenWhenUnavailable;
            public bool allowBundledManifestInEditor = true;
            public bool requireSignature;
            public string expectedKeyId;
            public string rsaPublicKeyPem;
            public string rsaModulusBase64;
            public string rsaExponentBase64;
            public string bundledEnvelopeResource =
                "RemoteUpdates/BundledReleaseEnvelope";

            public void Normalize()
            {
                schemaVersion = Math.Max(1, schemaVersion);
                manifestUrl = (manifestUrl ?? string.Empty).Trim();
                releaseChannel = string.IsNullOrWhiteSpace(releaseChannel)
                    ? "production"
                    : releaseChannel.Trim().ToLowerInvariant();
                requestTimeoutSeconds = Mathf.Clamp(
                    requestTimeoutSeconds,
                    5,
                    60);
                rsaPublicKeyPem = (rsaPublicKeyPem ?? string.Empty).Trim();
                rsaModulusBase64 =
                    (rsaModulusBase64 ?? string.Empty).Trim();
                rsaExponentBase64 =
                    (rsaExponentBase64 ?? string.Empty).Trim();
                expectedKeyId = (expectedKeyId ?? string.Empty).Trim();
                bundledEnvelopeResource = string.IsNullOrWhiteSpace(
                    bundledEnvelopeResource)
                    ? "RemoteUpdates/BundledReleaseEnvelope"
                    : bundledEnvelopeResource.Trim();
            }
        }

        [Serializable]
        private sealed class ActiveContentPointer
        {
            public int schemaVersion = 1;
            public string contentVersion;
            public string releaseDirectory;
            public long installedUtcTicks;
        }

        [Serializable]
        private sealed class ActivePatchPointer
        {
            public int schemaVersion = 1;
            public string contentVersion;
            public string[] patchDirectories = Array.Empty<string>();
            public long installedUtcTicks;
        }

        [Serializable]
        private sealed class YgoPatchArchiveManifest
        {
            public int schemaVersion = 1;
            public string[] files = Array.Empty<string>();
            public string[] deletedFiles = Array.Empty<string>();
        }

        [Serializable]
        private sealed class TrustedReleaseState
        {
            public int schemaVersion = 1;
            public string channel;
            public long highestSequenceNumber;
            public string releaseId;
            public long acceptedUtcTicks;
        }

        private enum GateState
        {
            Checking,
            Ready,
            AppUpdate,
            ContentUpdate,
            Downloading,
            Failed
        }

        private const string SettingsResourcePath =
            "RemoteUpdates/RemoteUpdateSettings";
        private const string CacheFileName = "last-release-envelope.json";
        private const string TrustedStateFileName =
            "trusted-release-state.json";
        private const string ClientContentBaselineFile =
            "bundled-content-baseline.txt";
        // Keep a single active overlay after every content delivery.  This is
        // deliberately stricter than a "keep the last N patches" policy: a
        // player who installs many small revisions must not retain every
        // superseded copy of an art, card or script file.
        private const int MaximumActiveYgoPatchDirectories = 1;
        private static RemoteUpdateRuntime _instance;

        private Settings _settings;
        private Canvas _canvas;
        private RectTransform _root;
        private Font _font;
        private Text _title;
        private Text _message;
        private Text _details;
        private Text _progressText;
        private Image _progressFill;
        private RectTransform _actions;
        private RemoteReleaseManifest _manifest;
        private RemoteClientArtifact _activeArtifact;
        private string _activeArtifactPath;
        private List<RemoteContentPackage> _pendingPackages =
            new List<RemoteContentPackage>();
        private bool _mandatory;
        private GateState _state;

        public static bool EntryReady =>
            _instance != null && _instance._state == GateState.Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeExists()
        {
            if (_instance != null)
                return;
            var root = new GameObject("Verificação de Atualização");
            root.AddComponent<RemoteUpdateRuntime>();
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

        private async void Start()
        {
            try
            {
                await CheckForUpdatesAsync();
            }
            catch (Exception exception)
            {
                HandleCheckFailure(exception);
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            _state = GateState.Checking;
            PlatformApplicationUpdater.CleanupAbandonedDownloads();
            RefreshBundledContentBaseline();
            CleanupYgoPatchStorage();
            SetCopy(
                "CONECTANDO À ARENA",
                "VERIFICANDO A VERSÃO MAIS RECENTE...",
                "Comparando cliente, dados de duelo e conteúdo remoto.");
            SetProgress(0.08f, "CONSULTANDO SERVIDOR");

            RemoteReleaseEnvelope envelope = null;
            Exception remoteFailure = null;
            if (_settings.enabled &&
                !string.IsNullOrWhiteSpace(_settings.manifestUrl))
            {
                try
                {
                    envelope = await DownloadEnvelopeAsync(
                        _settings.manifestUrl);
                    ValidateEnvelope(envelope, true);
                    NormalizeManifest(envelope.payload);
                    ValidateAndTrustRemoteManifest(envelope.payload);
                    SaveCachedEnvelope(envelope);
                }
                catch (Exception exception)
                {
                    remoteFailure = exception;
                }
            }

            // In strict mode a cached envelope cannot prove that it is still
            // the latest publication. Refuse entry until the version source
            // can be contacted instead of silently accepting a stale client.
            bool editorCanUseBundledEnvelope =
                Application.isEditor &&
                _settings.allowBundledManifestInEditor;
            if (envelope == null && remoteFailure != null &&
                !_settings.failOpenWhenUnavailable &&
                !editorCanUseBundledEnvelope)
            {
                throw remoteFailure;
            }

            if (envelope == null)
            {
                envelope = LoadCachedEnvelope() ?? LoadBundledEnvelope();
                if (envelope != null)
                    ValidateEnvelope(envelope, false);
            }

            if (envelope == null)
            {
                if (remoteFailure != null)
                    throw remoteFailure;
                CompleteEntry();
                return;
            }

            _manifest = envelope.payload;
            NormalizeManifest(_manifest);
            SetProgress(0.35f, "VERSÃO LOCAL  " + Application.version);

            _activeArtifact = SelectClientArtifactForCurrentPlatform(_manifest);
            if (RequiresApplicationUpdate(_manifest, _activeArtifact))
            {
                // Every published client version is mandatory. The legacy
                // manifest flag remains serialized for compatibility, but it
                // can never turn a newer build into an optional update.
                _mandatory = true;
                ShowApplicationUpdate();
                return;
            }

            string installedContentVersion = ReadInstalledContentVersion();
            _pendingPackages = SelectPackagesForCurrentPlatform(
                    _manifest.packages)
                .Where(package =>
                    SemanticVersion.Compare(
                        installedContentVersion,
                        package.version) < 0)
                .ToList();
            if (_pendingPackages.Count > 0)
            {
                // Content publications follow the same entry contract: the
                // player must install the latest package before continuing.
                _mandatory = true;
                ShowContentUpdate(installedContentVersion);
                return;
            }

            CompleteEntry();
        }

        private async Task<RemoteReleaseEnvelope> DownloadEnvelopeAsync(
            string url)
        {
            string separator = url.Contains("?") ? "&" : "?";
            string cacheBusted = url + separator + "client=" +
                                 UnityWebRequest.EscapeURL(Application.version) +
                                 "&t=" + DateTimeOffset.UtcNow
                                     .ToUnixTimeSeconds();
            using UnityWebRequest request = UnityWebRequest.Get(cacheBusted);
            request.timeout = _settings.requestTimeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                SetProgress(
                    Mathf.Lerp(0.08f, 0.30f, operation.progress),
                    "CONSULTANDO SERVIDOR");
                await Task.Yield();
            }
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new InvalidOperationException(
                    $"O servidor de versões respondeu HTTP " +
                    $"{request.responseCode}: {request.error}");
            }
            RemoteReleaseEnvelope envelope = JsonUtility.FromJson<
                RemoteReleaseEnvelope>(request.downloadHandler.text);
            if (envelope == null)
                throw new InvalidDataException(
                    "O manifesto remoto não contém uma versão válida.");
            return envelope;
        }

        private void ValidateEnvelope(
            RemoteReleaseEnvelope envelope,
            bool remote)
        {
            _settings ??= LoadSettings();
            if (envelope?.payload == null ||
                envelope.schemaVersion < 1 || envelope.schemaVersion > 2)
                throw new InvalidDataException(
                    "Envelope de atualização incompatível.");
            if (!_settings.requireSignature)
                return;
            if (!string.IsNullOrWhiteSpace(_settings.expectedKeyId) &&
                !string.Equals(
                    envelope.keyId?.Trim(),
                    _settings.expectedKeyId,
                    StringComparison.Ordinal))
            {
                throw new CryptographicException(
                    "O manifesto foi assinado por uma chave não autorizada.");
            }
            bool hasPortablePublicKey =
                !string.IsNullOrWhiteSpace(_settings.rsaModulusBase64) &&
                !string.IsNullOrWhiteSpace(_settings.rsaExponentBase64);
            if (string.IsNullOrWhiteSpace(envelope.signatureBase64) ||
                (!hasPortablePublicKey &&
                 string.IsNullOrWhiteSpace(_settings.rsaPublicKeyPem)))
            {
                throw new CryptographicException(
                    remote
                        ? "O manifesto remoto não possui assinatura."
                        : "O manifesto local não possui assinatura.");
            }

            byte[] payload = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(envelope.payload, false));
            byte[] signature = Convert.FromBase64String(
                envelope.signatureBase64);
            using RSA rsa = RSA.Create();
            if (hasPortablePublicKey)
            {
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = Convert.FromBase64String(
                        _settings.rsaModulusBase64),
                    Exponent = Convert.FromBase64String(
                        _settings.rsaExponentBase64)
                });
            }
            else
            {
                string publicKey = _settings.rsaPublicKeyPem
                    .Replace("-----BEGIN PUBLIC KEY-----", string.Empty)
                    .Replace("-----END PUBLIC KEY-----", string.Empty)
                    .Replace("\r", string.Empty)
                    .Replace("\n", string.Empty)
                    .Trim();
                byte[] publicKeyBytes = Convert.FromBase64String(publicKey);
                rsa.ImportSubjectPublicKeyInfo(publicKeyBytes, out _);
            }
            if (!rsa.VerifyData(
                    payload,
                    signature,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1))
            {
                throw new CryptographicException(
                    "A assinatura do manifesto de atualização é inválida.");
            }
        }

        private void ShowApplicationUpdate()
        {
            _state = GateState.AppUpdate;
            PresentUpdateShortcut(BeginApplicationUpdate, _mandatory);
        }

        private async void BeginApplicationUpdate()
        {
            if (_state == GateState.Downloading)
                return;

            if (_activeArtifact == null ||
                !_activeArtifact.HasInstallableArtifact ||
                Application.isEditor)
            {
                OpenApplicationUpdatePage();
                return;
            }

            _state = GateState.Downloading;
            ClearActions();
            try
            {
                string artifactPath = await PlatformApplicationUpdater
                    .DownloadAndVerifyAsync(
                        _activeArtifact,
                        _settings.requestTimeoutSeconds,
                        (progress, status) => SetProgress(progress, status));
                _activeArtifactPath = artifactPath;
                ApplicationUpdateLaunchResult launchResult =
                    PlatformApplicationUpdater.BeginInstall(
                        _activeArtifact,
                        artifactPath);
                if (launchResult == ApplicationUpdateLaunchResult
                        .PermissionRequested)
                {
                    _state = GateState.AppUpdate;
                    SetCopy(
                        "AUTORIZAR ATUALIZAÇÃO",
                        "PERMITA A INSTALAÇÃO DESTA FONTE",
                        "Ao voltar ao jogo, pressione continuar. O pacote já " +
                        "foi baixado e validado.");
                    PresentUpdateShortcut(
                        BeginApplicationUpdate,
                        true,
                        "CONTINUAR INSTALAÇÃO");
                    return;
                }

                if (launchResult == ApplicationUpdateLaunchResult.Preparing)
                {
                    await MonitorAndroidInstallerAsync();
                    return;
                }

                SetCopy(
                    "ATUALIZAÇÃO PRONTA",
                    "CONCLUA A INSTALAÇÃO NO SISTEMA",
                    Application.platform == RuntimePlatform.Android
                        ? "O Android exibirá a confirmação oficial do pacote."
                        : "O jogo será reiniciado quando os arquivos forem trocados.");
                SetProgress(1f, "PACOTE VALIDADO");
                SetUpdateShortcutStatus("CONFIRME NO ANDROID", false);
            }
            catch (Exception exception)
            {
                PlatformApplicationUpdater.DiscardDownloadedArtifact(
                    _activeArtifactPath);
                _activeArtifactPath = null;
                _state = GateState.Failed;
                Debug.LogWarning(
                    "[Atualização do aplicativo] " +
                    exception.GetBaseException().Message);
                SetCopy(
                    "ATUALIZAÇÃO INTERROMPIDA",
                    "NÃO FOI POSSÍVEL INSTALAR A NOVA VERSÃO",
                    exception.GetBaseException().Message);
                PresentUpdateShortcut(
                    BeginApplicationUpdate,
                    true,
                    "TENTAR NOVAMENTE");
            }
        }

        private async Task MonitorAndroidInstallerAsync()
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(30);
            DateTimeOffset? committedSince = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                AndroidInstallSnapshot snapshot = PlatformApplicationUpdater
                    .GetAndroidInstallSnapshot();
                string state = (snapshot.State ?? string.Empty)
                    .Trim()
                    .ToUpperInvariant();
                switch (state)
                {
                    case "PREPARING":
                        SetCopy(
                            "PREPARANDO ATUALIZAÇÃO",
                            "VALIDANDO O PACOTE NO ANDROID",
                            snapshot.Message);
                        SetUpdateShortcutStatus("VALIDANDO PACOTE", false);
                        break;
                    case "COPYING":
                        int percentage = Mathf.RoundToInt(
                            snapshot.Progress * 100f);
                        SetCopy(
                            "PREPARANDO ATUALIZAÇÃO",
                            "ENVIANDO O APK AO INSTALADOR",
                            "Esta etapa ocorre no aparelho e pode demorar " +
                            "alguns minutos.");
                        SetUpdateShortcutStatus(
                            "PREPARANDO  " + percentage + "%",
                            false);
                        break;
                    case "COMMITTING":
                    case "COMMITTED":
                        committedSince ??= DateTimeOffset.UtcNow;
                        SetCopy(
                            "ABRINDO INSTALADOR",
                            "AGUARDANDO O ANDROID",
                            snapshot.Message);
                        SetUpdateShortcutStatus("ABRINDO INSTALADOR", false);
                        if (DateTimeOffset.UtcNow - committedSince.Value >
                            TimeSpan.FromMinutes(2))
                        {
                            PresentAndroidInstallerRecovery(
                                "O Android não abriu a confirmação dentro " +
                                "do tempo esperado. A sessão pendente foi " +
                                "encerrada para evitar que a tela fique presa.");
                            return;
                        }
                        break;
                    case "AWAITING_CONFIRMATION":
                        _state = GateState.AppUpdate;
                        SetCopy(
                            "CONFIRMAR ATUALIZAÇÃO",
                            "CONCLUA A INSTALAÇÃO NO ANDROID",
                            snapshot.Message);
                        PresentUpdateShortcut(
                            ReopenAndroidInstaller,
                            true,
                            "ABRIR INSTALADOR");
                        return;
                    case "RECOVERY_REQUIRED":
                    case "CANCELLED":
                        PresentAndroidInstallerRecovery(
                            string.IsNullOrWhiteSpace(snapshot.Message)
                                ? "A confirmação do Android foi interrompida."
                                : snapshot.Message);
                        return;
                    case "SUCCESS":
                        PlatformApplicationUpdater.DiscardDownloadedArtifact(
                            _activeArtifactPath);
                        _activeArtifactPath = null;
                        _state = GateState.AppUpdate;
                        SetCopy(
                            "ATUALIZAÇÃO INSTALADA",
                            "REABRA O MASTER DUEL 2 PLUS ULTRA",
                            snapshot.Message);
                        SetUpdateShortcutStatus("ATUALIZAÇÃO CONCLUÍDA", false);
                        return;
                    case "FAILED":
                        throw new InvalidOperationException(
                            string.IsNullOrWhiteSpace(snapshot.Message)
                                ? "O Android recusou a instalação."
                                : snapshot.Message);
                    case "IDLE":
                        SetUpdateShortcutStatus("INICIANDO INSTALADOR", false);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Estado inesperado do instalador: " + state);
                }

                float waitUntil = Time.realtimeSinceStartup + 0.35f;
                while (Time.realtimeSinceStartup < waitUntil)
                    await Task.Yield();
            }
            throw new TimeoutException(
                "O Android excedeu o tempo de preparação da instalação.");
        }

        private void ReopenAndroidInstaller()
        {
            AndroidInstallSnapshot snapshot = PlatformApplicationUpdater
                .GetAndroidInstallSnapshot();
            if (string.Equals(
                    snapshot.State,
                    "FAILED",
                    StringComparison.OrdinalIgnoreCase))
            {
                _state = GateState.AppUpdate;
                PlatformApplicationUpdater.DiscardDownloadedArtifact(
                    _activeArtifactPath);
                _activeArtifactPath = null;
                PresentUpdateShortcut(
                    BeginApplicationUpdate,
                    true,
                    "TENTAR INSTALAÇÃO");
                return;
            }
            if (PlatformApplicationUpdater
                .ReopenAndroidInstallerConfirmation())
            {
                SetUpdateShortcutStatus("CONFIRME NO ANDROID", false);
                return;
            }

            PresentAndroidInstallerRecovery(
                "A confirmação oficial do Android não está mais disponível. " +
                "A instalação será reiniciada com um pacote verificado.");
        }

        private void PresentAndroidInstallerRecovery(string detail)
        {
            PlatformApplicationUpdater.CancelAndroidPendingInstall();
            PlatformApplicationUpdater.DiscardDownloadedArtifact(
                _activeArtifactPath);
            _activeArtifactPath = null;
            _state = GateState.AppUpdate;
            SetCopy(
                "INSTALAÇÃO DO ANDROID INTERROMPIDA",
                "REINICIE A INSTALAÇÃO",
                detail);
            PresentUpdateShortcut(
                BeginApplicationUpdate,
                true,
                "REINICIAR INSTALAÇÃO");
        }

        private static void SetUpdateShortcutStatus(
            string label,
            bool interactable)
        {
            LoginIntroController intro = FindAnyObjectByType<
                LoginIntroController>(FindObjectsInactive.Include);
            intro?.SetUpdateStatus(label, interactable);
        }

        private void ShowContentUpdate(string installedVersion)
        {
            _state = GateState.ContentUpdate;
            PresentUpdateShortcut(BeginContentDownload, _mandatory);
        }

        private async void BeginContentDownload()
        {
            if (_state == GateState.Downloading)
                return;
            _state = GateState.Downloading;
            try
            {
                for (int index = 0; index < _pendingPackages.Count; index++)
                {
                    RemoteContentPackage package = _pendingPackages[index];
                    await DownloadAndInstallPackageAsync(
                        package,
                        index,
                        _pendingPackages.Count);
                }
                SetProgress(1f, "CONTEÚDO ATUALIZADO");
                CompleteEntry();
            }
            catch (Exception exception)
            {
                _state = GateState.Failed;
                Debug.LogWarning(
                    "[Atualização] Download não ativado: " +
                    exception.GetBaseException().Message);
                PresentUpdateShortcut(
                    BeginContentDownload,
                    _mandatory,
                    "TENTAR NOVAMENTE");
            }
        }

        private async Task DownloadAndInstallPackageAsync(
            RemoteContentPackage package,
            int packageIndex,
            int packageCount)
        {
            if (package == null || string.IsNullOrWhiteSpace(package.url))
                throw new InvalidDataException(
                    "Um pacote da atualização não possui endereço de download.");
            string downloadRoot = Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "RemoteUpdates",
                "downloads");
            Directory.CreateDirectory(downloadRoot);
            string safePackage = SafeFileName(package.packageId);
            string temporary = Path.Combine(
                downloadRoot,
                safePackage + ".download");
            if (File.Exists(temporary))
                File.Delete(temporary);

            try
            {
                using (UnityWebRequest request = UnityWebRequest.Get(package.url))
                {
                    request.timeout = Math.Max(
                        30,
                        _settings.requestTimeoutSeconds);
                    request.downloadHandler = new DownloadHandlerFile(temporary)
                    {
                        removeFileOnAbort = true
                    };
                    UnityWebRequestAsyncOperation operation =
                        request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        float overall =
                            (packageIndex + request.downloadProgress) /
                            Math.Max(1f, packageCount);
                        SetProgress(
                            overall * 0.86f,
                            $"BAIXANDO {packageIndex + 1}/{packageCount}  " +
                            FormatBytes((long)request.downloadedBytes));
                        await Task.Yield();
                    }
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        throw new IOException(
                            $"Falha ao baixar {package.packageId}: " +
                            request.error);
                    }
                }

                SetProgress(
                    (packageIndex + 0.90f) / Math.Max(1f, packageCount),
                    "VERIFICANDO INTEGRIDADE");
                string actualHash = ComputeSha256(temporary);
                if (!string.IsNullOrWhiteSpace(package.sha256) &&
                    !string.Equals(
                        actualHash,
                        package.sha256.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new CryptographicException(
                        $"O pacote {package.packageId} falhou na verificação SHA-256.");
                }
                if (package.sizeBytes > 0 &&
                    new FileInfo(temporary).Length != package.sizeBytes)
                {
                    throw new InvalidDataException(
                        $"O pacote {package.packageId} chegou com tamanho diferente.");
                }

                if (string.Equals(
                        package.target,
                        "ygo",
                        StringComparison.OrdinalIgnoreCase))
                {
                    // Compatibility path for older full-content snapshots.
                    InstallYgoArchive(package, temporary);
                }
                else if (string.Equals(
                             package.target,
                             "ygo-patch",
                             StringComparison.OrdinalIgnoreCase))
                {
                    InstallYgoPatchArchive(package, temporary);
                }
                else
                {
                    throw new NotSupportedException(
                        $"Destino remoto não suportado: {package.target}");
                }
            }
            finally
            {
                // A content patch is staged atomically before reaching the
                // active pointer, so its downloaded ZIP is never needed again.
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
        }

        private void InstallYgoArchive(
            RemoteContentPackage package,
            string archivePath)
        {
            string container = Path.GetFullPath(Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "RemoteContent",
                "Ygo"));
            string releases = Path.Combine(container, "releases");
            Directory.CreateDirectory(releases);
            string releaseDirectory = SafeFileName(
                _manifest.releaseId + "-" + package.version);
            string staging = Path.GetFullPath(Path.Combine(
                releases,
                releaseDirectory + ".staging"));
            string final = Path.GetFullPath(Path.Combine(
                releases,
                releaseDirectory));
            EnsureChildPath(releases, staging);
            EnsureChildPath(releases, final);
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);

            try
            {
                using var file = new FileStream(
                    archivePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                using var zip = new ZipArchive(
                    file,
                    ZipArchiveMode.Read,
                    false);
                string stagingPrefix = staging + Path.DirectorySeparatorChar;
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string destination = Path.GetFullPath(Path.Combine(
                        staging,
                        entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(
                            stagingPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "O pacote tentou gravar fora da área segura.");
                    }
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(destination) ?? staging);
                    using Stream source = entry.Open();
                    using var destinationStream = new FileStream(
                        destination,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);
                    source.CopyTo(destinationStream);
                }
                ValidateEssentialYgoContent(staging);
                if (Directory.Exists(final))
                    Directory.Delete(final, true);
                Directory.Move(staging, final);

                var pointer = new ActiveContentPointer
                {
                    contentVersion = package.version,
                    releaseDirectory = releaseDirectory,
                    installedUtcTicks = DateTime.UtcNow.Ticks
                };
                Directory.CreateDirectory(container);
                WriteTextAtomically(
                    Path.Combine(container, "active.json"),
                    JsonUtility.ToJson(pointer, true));
                YgoContentLocator.InvalidateCachedRoot();
                CardPresentationText.InvalidateDatabaseCache();
            }
            catch
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
                throw;
            }
        }

        private void InstallYgoPatchArchive(
            RemoteContentPackage package,
            string archivePath)
        {
            string container = GetYgoRemoteContainer();
            string patches = Path.Combine(container, "patches");
            Directory.CreateDirectory(patches);
            string patchDirectory = SafeFileName(
                _manifest.releaseId + "-" + package.packageId + "-" +
                package.version);
            string staging = Path.GetFullPath(Path.Combine(
                patches,
                patchDirectory + ".staging"));
            string final = Path.GetFullPath(Path.Combine(
                patches,
                patchDirectory));
            EnsureChildPath(patches, staging);
            EnsureChildPath(patches, final);
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);

            try
            {
                ExtractYgoPatchSafely(archivePath, staging);
                ValidateYgoPatchDirectory(staging);
                if (Directory.Exists(final))
                    Directory.Delete(final, true);
                Directory.Move(staging, final);

                ActivePatchPointer pointer = ReadActivePatchPointer(container);
                var directories = (pointer.patchDirectories ??
                                   Array.Empty<string>())
                    .Where(IsSafePatchDirectory)
                    .Where(directory => Directory.Exists(Path.Combine(
                        patches,
                        directory)))
                    .Where(directory => !string.Equals(
                        directory,
                        patchDirectory,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
                directories.Add(patchDirectory);
                directories = CompactYgoPatchDirectoriesIfNeeded(
                    patches,
                    directories,
                    package.version);
                pointer.contentVersion = package.version;
                pointer.patchDirectories = directories.ToArray();
                pointer.installedUtcTicks = DateTime.UtcNow.Ticks;
                WriteTextAtomically(
                    Path.Combine(container, "patches.json"),
                    JsonUtility.ToJson(pointer, true));
                CleanupInactiveYgoPatchDirectories(patches, directories);
                YgoContentLocator.InvalidateCachedRoot();
                CardPresentationText.InvalidateDatabaseCache();
            }
            catch
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
                throw;
            }
        }

        /// <summary>
        /// Merges the active overlay into one patch directory.  Only the last
        /// state of each changed path is copied, therefore a file replaced ten
        /// times consumes the space of one replacement, not ten.  The pointer
        /// is changed by the caller only after the compact patch is complete.
        /// </summary>
        private static List<string> CompactYgoPatchDirectoriesIfNeeded(
            string patches,
            List<string> directories,
            string contentVersion)
        {
            if (directories == null ||
                directories.Count <= MaximumActiveYgoPatchDirectories)
            {
                return directories ?? new List<string>();
            }

            try
            {
                string compactDirectory = BuildCompactedYgoPatchDirectory(
                    patches,
                    directories,
                    contentVersion);
                return new List<string> { compactDirectory };
            }
            catch (Exception exception)
            {
                // The original patches remain valid.  Prefer a retry on the
                // next launch over losing a playable content revision when a
                // device runs out of storage during compaction.
                Debug.LogWarning(
                    "[Atualização] A compactação do conteúdo será tentada " +
                    "novamente: " + exception.GetBaseException().Message);
                return directories;
            }
        }

        private static string BuildCompactedYgoPatchDirectory(
            string patches,
            IEnumerable<string> sourceDirectories,
            string contentVersion)
        {
            var activeFiles = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            var deletedFiles = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string directory in sourceDirectories ??
                     Array.Empty<string>())
            {
                if (!IsSafePatchDirectory(directory))
                    throw new InvalidDataException(
                        "O histórico de patches contém uma pasta inválida.");
                string root = Path.GetFullPath(Path.Combine(patches, directory));
                YgoPatchArchiveManifest manifest = ReadYgoPatchArchiveManifest(
                    root);
                foreach (string relative in manifest.files ?? Array.Empty<string>())
                {
                    string normalized = NormalizePatchRelativePath(relative);
                    string source = Path.GetFullPath(Path.Combine(
                        root,
                        normalized.Replace('/', Path.DirectorySeparatorChar)));
                    EnsureChildPath(root, source);
                    if (!File.Exists(source))
                    {
                        throw new FileNotFoundException(
                            "O patch ativo não contém o arquivo declarado.",
                            source);
                    }
                    activeFiles[normalized] = source;
                    deletedFiles.Remove(normalized);
                }
                foreach (string relative in manifest.deletedFiles ??
                         Array.Empty<string>())
                {
                    string normalized = NormalizePatchRelativePath(relative);
                    activeFiles.Remove(normalized);
                    deletedFiles.Add(normalized);
                }
            }

            if (activeFiles.Count == 0 && deletedFiles.Count == 0)
            {
                throw new InvalidDataException(
                    "Não há alterações ativas para compactar.");
            }

            string compactDirectory = "compact-" +
                                      SafeFileName(contentVersion) + "-" +
                                      DateTime.UtcNow.Ticks.ToString(
                                          CultureInfo.InvariantCulture);
            string staging = Path.GetFullPath(Path.Combine(
                patches,
                compactDirectory + ".staging"));
            string final = Path.GetFullPath(Path.Combine(
                patches,
                compactDirectory));
            EnsureChildPath(patches, staging);
            EnsureChildPath(patches, final);
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);

            try
            {
                foreach (KeyValuePair<string, string> file in activeFiles
                             .OrderBy(pair => pair.Key,
                                 StringComparer.OrdinalIgnoreCase))
                {
                    string destination = Path.GetFullPath(Path.Combine(
                        staging,
                        file.Key.Replace('/', Path.DirectorySeparatorChar)));
                    EnsureChildPath(staging, destination);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(destination) ?? staging);
                    File.Copy(file.Value, destination, true);
                }
                WriteTextAtomically(
                    Path.Combine(staging, "patch-manifest.json"),
                    JsonUtility.ToJson(new YgoPatchArchiveManifest
                    {
                        files = activeFiles.Keys
                            .OrderBy(path => path,
                                StringComparer.OrdinalIgnoreCase)
                            .ToArray(),
                        deletedFiles = deletedFiles
                            .OrderBy(path => path,
                                StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                    }, true));
                ValidateYgoPatchDirectory(staging);
                if (Directory.Exists(final))
                    Directory.Delete(final, true);
                Directory.Move(staging, final);
                return compactDirectory;
            }
            catch
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
                throw;
            }
        }

        private static void CleanupYgoPatchStorage()
        {
            try
            {
                string container = GetYgoRemoteContainer();
                string patches = Path.Combine(container, "patches");
                if (!Directory.Exists(patches))
                    return;

                string pointerPath = Path.Combine(container, "patches.json");
                if (!File.Exists(pointerPath))
                {
                    // No pointer means no patch has ever become active. Only
                    // interrupted staging folders may be discarded here.
                    CleanupInactiveYgoPatchDirectories(
                        patches,
                        Array.Empty<string>());
                    return;
                }
                ActivePatchPointer pointer = JsonUtility.FromJson<
                    ActivePatchPointer>(File.ReadAllText(pointerPath));
                if (pointer == null || pointer.schemaVersion != 1)
                {
                    throw new InvalidDataException(
                        "O índice de conteúdo remoto é incompatível.");
                }
                List<string> directories = (pointer.patchDirectories ??
                                             Array.Empty<string>())
                    .Where(IsSafePatchDirectory)
                    .Where(directory => Directory.Exists(Path.Combine(
                        patches,
                        directory)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (directories.Count > MaximumActiveYgoPatchDirectories)
                {
                    directories = CompactYgoPatchDirectoriesIfNeeded(
                        patches,
                        directories,
                        pointer.contentVersion);
                    pointer.patchDirectories = directories.ToArray();
                    pointer.installedUtcTicks = DateTime.UtcNow.Ticks;
                    WriteTextAtomically(
                        Path.Combine(container, "patches.json"),
                        JsonUtility.ToJson(pointer, true));
                    YgoContentLocator.InvalidateCachedRoot();
                    CardPresentationText.InvalidateDatabaseCache();
                }
                CleanupInactiveYgoPatchDirectories(patches, directories);
            }
            catch (Exception exception)
            {
                // Storage maintenance must never block a player from reaching
                // a known-good installed content revision.
                Debug.LogWarning(
                    "[Atualização] A manutenção de conteúdo foi adiada: " +
                    exception.GetBaseException().Message);
            }
        }

        /// <summary>
        /// A full APK/Windows build already ships a complete content baseline.
        /// When that baseline changes, old remote overlays must not survive and
        /// override the newly installed files. This also reclaims historic
        /// snapshot folders left by earlier updater versions.
        /// </summary>
        private static void RefreshBundledContentBaseline()
        {
            try
            {
                string container = GetYgoRemoteContainer();
                string markerPath = Path.Combine(
                    container,
                    ClientContentBaselineFile);
                string identity = Application.identifier + ":" +
                                  Application.version + ":" +
                                  (string.IsNullOrWhiteSpace(Application.buildGUID)
                                      ? "no-build-guid"
                                      : Application.buildGUID);
                if (File.Exists(markerPath) &&
                    string.Equals(File.ReadAllText(markerPath), identity,
                        StringComparison.Ordinal))
                {
                    return;
                }

                Directory.CreateDirectory(container);
                foreach (string directory in new[]
                         {
                             "patches",
                             "releases",
                             "runtime-core",
                             "runtime-core.staging"
                         })
                {
                    string candidate = Path.GetFullPath(Path.Combine(
                        container,
                        directory));
                    EnsureChildPath(container, candidate);
                    if (Directory.Exists(candidate))
                        Directory.Delete(candidate, true);
                }
                string activeSnapshot = Path.Combine(container, "active.json");
                string activePatches = Path.Combine(container, "patches.json");
                if (File.Exists(activeSnapshot)) File.Delete(activeSnapshot);
                if (File.Exists(activePatches)) File.Delete(activePatches);
                WriteTextAtomically(markerPath, identity);
                YgoContentLocator.InvalidateCachedRoot();
                CardPresentationText.InvalidateDatabaseCache();
            }
            catch (Exception exception)
            {
                // Keeping an old, verified overlay is safer than blocking the
                // application if storage is temporarily unavailable.
                Debug.LogWarning(
                    "[Atualização] A base de conteúdo será renovada depois: " +
                    exception.GetBaseException().Message);
            }
        }

        private static void CleanupInactiveYgoPatchDirectories(
            string patches,
            IEnumerable<string> activeDirectories)
        {
            if (!Directory.Exists(patches))
                return;
            var active = new HashSet<string>(
                (activeDirectories ?? Array.Empty<string>())
                    .Where(IsSafePatchDirectory),
                StringComparer.OrdinalIgnoreCase);
            foreach (string directoryPath in Directory.GetDirectories(
                         patches,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                string directory = Path.GetFileName(directoryPath);
                bool isStaging = directory.EndsWith(
                    ".staging",
                    StringComparison.OrdinalIgnoreCase);
                if (!isStaging && active.Contains(directory))
                    continue;
                if (!isStaging && !IsSafePatchDirectory(directory))
                    continue;
                Directory.Delete(directoryPath, true);
            }
        }

        private static void ExtractYgoPatchSafely(
            string archivePath,
            string destination)
        {
            string prefix = Path.GetFullPath(destination).TrimEnd(
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            const long maxUnpackedBytes = 1024L * 1024L * 1024L;
            const int maxFiles = 10000;
            long unpackedBytes = 0;
            int fileCount = 0;
            using var stream = new FileStream(
                archivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, false);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                string name = entry.FullName.Replace(
                    '/',
                    Path.DirectorySeparatorChar);
                string output = Path.GetFullPath(Path.Combine(destination, name));
                if (!output.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        "O pacote tentou gravar fora da área segura.");
                }
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(output);
                    continue;
                }
                fileCount++;
                unpackedBytes += entry.Length;
                if (fileCount > maxFiles || unpackedBytes > maxUnpackedBytes)
                {
                    throw new InvalidDataException(
                        "O pacote de conteúdo excede o limite seguro.");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(output) ??
                                          destination);
                using Stream source = entry.Open();
                using var target = new FileStream(
                    output,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);
                source.CopyTo(target);
            }
        }

        private static void ValidateYgoPatchDirectory(string root)
        {
            YgoPatchArchiveManifest manifest = ReadYgoPatchArchiveManifest(root);

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string relative in manifest.files ?? Array.Empty<string>())
            {
                string safe = ValidatePatchRelativePath(relative);
                if (!files.Add(safe))
                    throw new InvalidDataException(
                        "O pacote incremental repete um arquivo.");
                string candidate = Path.GetFullPath(Path.Combine(root, safe));
                EnsureChildPath(root, candidate);
                if (!File.Exists(candidate))
                {
                    throw new FileNotFoundException(
                        "O pacote incremental não contém o arquivo declarado.",
                        candidate);
                }
            }
            foreach (string relative in manifest.deletedFiles ??
                     Array.Empty<string>())
            {
                string safe = ValidatePatchRelativePath(relative);
                if (files.Contains(safe))
                {
                    throw new InvalidDataException(
                        "Um arquivo não pode ser incluído e removido no mesmo pacote.");
                }
            }
            if (files.Count == 0 &&
                !(manifest.deletedFiles ?? Array.Empty<string>()).Any())
            {
                throw new InvalidDataException(
                    "O pacote incremental não possui alterações.");
            }
        }

        private static YgoPatchArchiveManifest ReadYgoPatchArchiveManifest(
            string root)
        {
            string manifestPath = Path.Combine(root, "patch-manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new InvalidDataException(
                    "O pacote incremental não possui patch-manifest.json.");
            }
            YgoPatchArchiveManifest manifest = JsonUtility.FromJson<
                YgoPatchArchiveManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.schemaVersion != 1)
            {
                throw new InvalidDataException(
                    "O manifesto do pacote incremental é incompatível.");
            }
            return manifest;
        }

        private static ActivePatchPointer ReadActivePatchPointer(string container)
        {
            try
            {
                string path = Path.Combine(container, "patches.json");
                if (!File.Exists(path))
                    return new ActivePatchPointer();
                ActivePatchPointer pointer = JsonUtility.FromJson<
                    ActivePatchPointer>(File.ReadAllText(path));
                return pointer ?? new ActivePatchPointer();
            }
            catch
            {
                return new ActivePatchPointer();
            }
        }

        private static string GetYgoRemoteContainer()
        {
            return Path.GetFullPath(Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "RemoteContent",
                "Ygo"));
        }

        private static bool IsSafePatchDirectory(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf("..", StringComparison.Ordinal) < 0 &&
                   value.IndexOfAny(new[] { '/', '\\' }) < 0;
        }

        private static string ValidatePatchRelativePath(string value)
        {
            string safe = (value ?? string.Empty).Replace('\\', '/').Trim('/');
            if (safe.Length == 0 ||
                safe.Equals("patch-manifest.json", StringComparison.OrdinalIgnoreCase) ||
                safe.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                safe.StartsWith("/", StringComparison.Ordinal) ||
                Path.IsPathRooted(safe) ||
                safe.Split('/').Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(
                    "O pacote incremental contém um caminho inválido.");
            }
            return safe.Replace('/', Path.DirectorySeparatorChar);
        }

        private static string NormalizePatchRelativePath(string value)
        {
            return ValidatePatchRelativePath(value).Replace(
                Path.DirectorySeparatorChar,
                '/');
        }

        private void OpenApplicationUpdatePage()
        {
            string url = _activeArtifact?.url;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = Application.platform switch
                {
                    RuntimePlatform.Android => _manifest.androidUpdateUrl,
                    RuntimePlatform.WindowsPlayer => _manifest.windowsUpdateUrl,
                    RuntimePlatform.WindowsEditor => _manifest.windowsUpdateUrl,
                    _ => _manifest.fallbackUpdateUrl
                };
            }
            if (string.IsNullOrWhiteSpace(url))
                url = _manifest.fallbackUpdateUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                if (_message != null)
                    _message.text =
                        "O endereço da atualização ainda não foi publicado.";
                return;
            }
            Application.OpenURL(url);
        }

        private void CompleteEntry()
        {
            _state = GateState.Ready;
            LoginIntroController intro = FindAnyObjectByType<
                LoginIntroController>(FindObjectsInactive.Include);
            intro?.HideUpdateOffer();
            if (_canvas != null)
                Destroy(_canvas.gameObject);
        }

        private void PresentUpdateShortcut(
            Action action,
            bool blocksEntry,
            string label = "ATUALIZAR")
        {
            LoginIntroController intro = FindAnyObjectByType<
                LoginIntroController>(FindObjectsInactive.Include);
            if (intro == null)
            {
                Debug.LogWarning(
                    "[Atualização] A oferta existe, mas a cena de abertura " +
                    "não está ativa. Exibindo o painel de segurança.");
                if (_canvas == null)
                    BuildGate();
                SetCopy(
                    _manifest?.title ?? "ATUALIZAÇÃO OBRIGATÓRIA",
                    label,
                    (_manifest?.summary ?? string.Empty) +
                    FormatChanges(_manifest?.changes));
                ClearActions();
                CreateActionButton(
                    label,
                    new Color(0.08f, 0.78f, 0.98f, 1f),
                    action);
                if (!blocksEntry)
                {
                    CreateActionButton(
                        "CONTINUAR",
                        new Color(0.62f, 0.78f, 0.22f, 1f),
                        CompleteEntry);
                }
                return;
            }
            intro.ShowUpdateOffer(action, blocksEntry, label);
        }

        private void HandleCheckFailure(Exception exception)
        {
            Debug.LogWarning(
                "[Atualização remota] " +
                exception.GetBaseException().Message);
            if (_settings.failOpenWhenUnavailable)
            {
                CompleteEntry();
                return;
            }
            _state = GateState.Failed;
            PresentUpdateShortcut(
                async () =>
                {
                    try
                    {
                        await CheckForUpdatesAsync();
                    }
                    catch (Exception retryException)
                    {
                        HandleCheckFailure(retryException);
                    }
                },
                true,
                "TENTAR CONEXÃO");
        }

        private void BuildGate()
        {
            GameObject canvasObject = new GameObject(
                "Arcane Update Gate",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue - 8;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            _root = canvasObject.GetComponent<RectTransform>();
            Stretch(_root);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (FindAnyObjectByType<EventSystem>(
                    FindObjectsInactive.Include) == null)
            {
                var eventSystem = new GameObject(
                    "Sistema de Entrada da Atualização",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(transform, false);
            }

            Image backdrop = CreateImage(
                _root,
                "Fundo",
                Vector2.zero,
                Vector2.one,
                new Color(0.001f, 0.007f, 0.018f, 1f));
            Image glow = CreateImage(
                backdrop.transform,
                "Brilho Central",
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                new Color(0.002f, 0.022f, 0.042f, 0.97f));
            AddOutline(glow.gameObject, new Color(0.08f, 0.75f, 0.95f, 0.72f));
            for (int index = 0; index < 5; index++)
            {
                float y = 0.14f + index * 0.17f;
                CreateImage(
                    glow.transform,
                    "Linha de Dados " + index,
                    new Vector2(index % 2 == 0 ? 0.025f : 0.12f, y),
                    new Vector2(index % 2 == 0 ? 0.88f : 0.975f, y + 0.0025f),
                    new Color(0.08f, 0.66f, 0.88f, 0.18f))
                    .raycastTarget = false;
            }
            Image card = CreateImage(
                glow.transform,
                "Painel de Atualização",
                new Vector2(0.19f, 0.14f),
                new Vector2(0.81f, 0.86f),
                new Color(0.004f, 0.025f, 0.052f, 0.985f));
            AddOutline(card.gameObject, new Color(0.68f, 0.48f, 0.92f, 0.8f));
            GameObject sheenObject = new GameObject(
                "Superfície Arcana",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ArcanePanelSheenGraphic));
            sheenObject.transform.SetParent(card.transform, false);
            RectTransform sheenRect =
                sheenObject.GetComponent<RectTransform>();
            Stretch(sheenRect);
            ArcanePanelSheenGraphic sheen =
                sheenObject.GetComponent<ArcanePanelSheenGraphic>();
            sheen.SetStyle(
                new Color(0.55f, 0.32f, 0.95f, 1f),
                true,
                0.96f);
            sheen.raycastTarget = false;
            CreateLabel(
                card.transform,
                "MASTER DUEL 2 PLUS ULTRA  •  ATUALIZAÇÃO",
                18,
                new Color(0.15f, 0.83f, 1f, 1f),
                new Vector2(0.08f, 0.88f),
                new Vector2(0.92f, 0.95f));
            _title = CreateLabel(
                card.transform,
                string.Empty,
                38,
                Color.white,
                new Vector2(0.07f, 0.74f),
                new Vector2(0.93f, 0.88f));
            _message = CreateLabel(
                card.transform,
                string.Empty,
                23,
                new Color(0.92f, 0.82f, 1f, 1f),
                new Vector2(0.08f, 0.64f),
                new Vector2(0.92f, 0.75f));
            _details = CreateLabel(
                card.transform,
                string.Empty,
                17,
                new Color(0.78f, 0.86f, 0.94f, 1f),
                new Vector2(0.10f, 0.34f),
                new Vector2(0.90f, 0.63f));
            _details.alignment = TextAnchor.UpperCenter;

            Image progressTrack = CreateImage(
                card.transform,
                "Trilha de Progresso",
                new Vector2(0.12f, 0.25f),
                new Vector2(0.88f, 0.285f),
                new Color(0.06f, 0.11f, 0.17f, 1f));
            _progressFill = CreateImage(
                progressTrack.transform,
                "Progresso",
                Vector2.zero,
                Vector2.one,
                new Color(0.08f, 0.78f, 0.98f, 1f));
            _progressFill.type = Image.Type.Filled;
            _progressFill.fillMethod = Image.FillMethod.Horizontal;
            _progressFill.fillOrigin = 0;
            _progressText = CreateLabel(
                card.transform,
                string.Empty,
                14,
                new Color(0.62f, 0.78f, 0.88f, 1f),
                new Vector2(0.10f, 0.19f),
                new Vector2(0.90f, 0.245f));

            GameObject actionsObject = new GameObject(
                "Ações",
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup));
            actionsObject.transform.SetParent(card.transform, false);
            _actions = actionsObject.GetComponent<RectTransform>();
            _actions.anchorMin = new Vector2(0.12f, 0.06f);
            _actions.anchorMax = new Vector2(0.88f, 0.16f);
            _actions.offsetMin = Vector2.zero;
            _actions.offsetMax = Vector2.zero;
            HorizontalLayoutGroup layout =
                actionsObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 22f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        private void SetCopy(string title, string message, string details)
        {
            if (_title != null) _title.text = title ?? string.Empty;
            if (_message != null) _message.text = message ?? string.Empty;
            if (_details != null) _details.text = details ?? string.Empty;
        }

        private void SetProgress(float value, string label)
        {
            if (_progressFill != null)
                _progressFill.fillAmount = Mathf.Clamp01(value);
            if (_progressText != null)
                _progressText.text = label ?? string.Empty;
            if (_state == GateState.Downloading)
            {
                LoginIntroController intro = FindAnyObjectByType<
                    LoginIntroController>(FindObjectsInactive.Include);
                intro?.SetUpdateProgress(value);
            }
        }

        private void ClearActions()
        {
            if (_actions == null)
                return;
            for (int index = _actions.childCount - 1; index >= 0; index--)
                Destroy(_actions.GetChild(index).gameObject);
        }

        private void CreateActionButton(string label, Color color, Action action)
        {
            Image image = CreateImage(
                _actions,
                label,
                Vector2.zero,
                Vector2.one,
                new Color(color.r * 0.16f, color.g * 0.16f,
                    color.b * 0.16f, 0.98f));
            AddOutline(image.gameObject, color);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action?.Invoke());
            CreateLabel(
                image.transform,
                label,
                17,
                Color.white,
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f));
        }

        private Image CreateImage(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private Text CreateLabel(
            Transform parent,
            string value,
            int size,
            Color color,
            Vector2 min,
            Vector2 max)
        {
            GameObject gameObject = new GameObject(
                "Texto",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            gameObject.transform.SetParent(parent, false);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Text text = gameObject.GetComponent<Text>();
            text.font = _font;
            text.text = value ?? string.Empty;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static void AddOutline(GameObject target, Color color)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SaveCachedEnvelope(RemoteReleaseEnvelope envelope)
        {
            try
            {
                string path = CachePath();
                Directory.CreateDirectory(Path.GetDirectoryName(path) ??
                                          Application.persistentDataPath);
                WriteTextAtomically(path, JsonUtility.ToJson(envelope, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Atualização] O manifesto não pôde ser armazenado: " +
                    exception.Message);
            }
        }

        private void ValidateAndTrustRemoteManifest(
            RemoteReleaseManifest manifest)
        {
            if (manifest == null)
                throw new InvalidDataException("Manifesto remoto ausente.");
            if (!string.Equals(
                    manifest.channel,
                    _settings.releaseChannel,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "O manifesto pertence a outro canal de atualização.");
            }
            if (manifest.sequenceNumber <= 0)
                throw new InvalidDataException(
                    "O manifesto não possui uma sequência de segurança válida.");
            if (!DateTimeOffset.TryParse(
                    manifest.publishedUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset publishedUtc))
            {
                throw new InvalidDataException(
                    "A data de publicação do manifesto é inválida.");
            }
            if (!DateTimeOffset.TryParse(
                    manifest.expiresUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTimeOffset expiresUtc))
            {
                throw new InvalidDataException(
                    "O manifesto não possui uma validade verificável.");
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (publishedUtc > now.AddHours(24))
                throw new InvalidDataException(
                    "A data do manifesto está adiantada em relação ao dispositivo.");
            if (expiresUtc <= now || expiresUtc <= publishedUtc)
                throw new InvalidDataException(
                    "O manifesto de atualização expirou ou possui validade inválida.");

            TrustedReleaseState trusted = LoadTrustedReleaseState();
            if (trusted != null &&
                string.Equals(
                    trusted.channel,
                    manifest.channel,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (manifest.sequenceNumber < trusted.highestSequenceNumber)
                {
                    throw new CryptographicException(
                        "Uma versão antiga do catálogo de atualização foi bloqueada.");
                }
                if (manifest.sequenceNumber == trusted.highestSequenceNumber &&
                    !string.Equals(
                        manifest.releaseId,
                        trusted.releaseId,
                        StringComparison.Ordinal))
                {
                    throw new CryptographicException(
                        "A sequência do catálogo foi reutilizada por outra versão.");
                }
            }

            if (trusted == null ||
                manifest.sequenceNumber > trusted.highestSequenceNumber)
            {
                SaveTrustedReleaseState(new TrustedReleaseState
                {
                    channel = manifest.channel,
                    highestSequenceNumber = manifest.sequenceNumber,
                    releaseId = manifest.releaseId,
                    acceptedUtcTicks = now.UtcDateTime.Ticks
                });
            }
        }

        private static TrustedReleaseState LoadTrustedReleaseState()
        {
            try
            {
                string path = TrustedStatePath();
                if (!File.Exists(path))
                    return null;
                TrustedReleaseState state = JsonUtility.FromJson<
                    TrustedReleaseState>(File.ReadAllText(path));
                return state != null && state.highestSequenceNumber > 0
                    ? state
                    : null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[Atualização] O estado de confiança foi ignorado: " +
                    exception.Message);
                return null;
            }
        }

        private static void SaveTrustedReleaseState(TrustedReleaseState state)
        {
            string path = TrustedStatePath();
            WriteTextAtomically(path, JsonUtility.ToJson(state, true));
        }

        private RemoteReleaseEnvelope LoadCachedEnvelope()
        {
            try
            {
                string path = CachePath();
                return File.Exists(path)
                    ? JsonUtility.FromJson<RemoteReleaseEnvelope>(
                        File.ReadAllText(path))
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private RemoteReleaseEnvelope LoadBundledEnvelope()
        {
            TextAsset asset = Resources.Load<TextAsset>(
                _settings.bundledEnvelopeResource);
            return asset == null
                ? null
                : JsonUtility.FromJson<RemoteReleaseEnvelope>(asset.text);
        }

        private static void NormalizeManifest(RemoteReleaseManifest manifest)
        {
            if (manifest == null)
                throw new InvalidDataException("Manifesto ausente.");
            manifest.releaseId = SafeFileName(manifest.releaseId);
            manifest.channel = string.IsNullOrWhiteSpace(manifest.channel)
                ? "production"
                : manifest.channel.Trim().ToLowerInvariant();
            manifest.expiresUtc = (manifest.expiresUtc ?? string.Empty).Trim();
            manifest.minimumClientVersion = string.IsNullOrWhiteSpace(
                manifest.minimumClientVersion)
                ? Application.version
                : manifest.minimumClientVersion.Trim();
            manifest.latestClientVersion = string.IsNullOrWhiteSpace(
                manifest.latestClientVersion)
                ? manifest.minimumClientVersion
                : manifest.latestClientVersion.Trim();
            manifest.contentVersion = string.IsNullOrWhiteSpace(
                manifest.contentVersion)
                ? "0.0.0"
                : manifest.contentVersion.Trim();
            manifest.title = string.IsNullOrWhiteSpace(manifest.title)
                ? "ATUALIZAÇÃO DO MASTER DUEL 2 PLUS ULTRA"
                : manifest.title.Trim();
            manifest.summary = (manifest.summary ?? string.Empty).Trim();
            manifest.changes ??= Array.Empty<string>();
            manifest.packages ??= Array.Empty<RemoteContentPackage>();
            manifest.windows ??= new RemoteClientArtifact();
            manifest.android ??= new RemoteClientArtifact();
            NormalizeClientArtifact(
                manifest.windows,
                "windows",
                manifest.latestClientVersion,
                manifest.windowsUpdateUrl,
                manifest.protocolVersion);
            NormalizeClientArtifact(
                manifest.android,
                "android",
                manifest.latestClientVersion,
                manifest.androidUpdateUrl,
                manifest.protocolVersion);
        }

        private static void NormalizeClientArtifact(
            RemoteClientArtifact artifact,
            string platform,
            string legacyVersion,
            string legacyUrl,
            int protocolVersion)
        {
            artifact.platform = platform;
            artifact.versionName = string.IsNullOrWhiteSpace(
                artifact.versionName)
                ? legacyVersion
                : artifact.versionName.Trim();
            artifact.url = string.IsNullOrWhiteSpace(artifact.url)
                ? (legacyUrl ?? string.Empty).Trim()
                : artifact.url.Trim();
            artifact.sha256 = (artifact.sha256 ?? string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToLowerInvariant();
            artifact.signingCertificateSha256 =
                (artifact.signingCertificateSha256 ?? string.Empty)
                .Replace(":", string.Empty)
                .Replace("-", string.Empty)
                .Trim()
                .ToLowerInvariant();
            artifact.executableName = (artifact.executableName ?? string.Empty)
                .Trim();
            artifact.protocolVersion = Math.Max(
                1,
                artifact.protocolVersion > 0
                    ? artifact.protocolVersion
                    : protocolVersion);
        }

        private static RemoteClientArtifact
            SelectClientArtifactForCurrentPlatform(
                RemoteReleaseManifest manifest)
        {
            return Application.platform switch
            {
                RuntimePlatform.Android => manifest.android,
                RuntimePlatform.WindowsPlayer => manifest.windows,
                RuntimePlatform.WindowsEditor => manifest.windows,
                _ => null
            };
        }

        private static bool RequiresApplicationUpdate(
            RemoteReleaseManifest manifest,
            RemoteClientArtifact artifact)
        {
            if (Application.platform == RuntimePlatform.Android &&
                artifact != null && artifact.versionCode > 0)
            {
                long installedCode = PlatformApplicationUpdater
                    .GetInstalledAndroidVersionCode();
                int minimumCode = artifact.minimumVersionCode > 0
                    ? artifact.minimumVersionCode
                    : artifact.versionCode;
                if (installedCode > 0)
                    return installedCode < minimumCode ||
                           installedCode < artifact.versionCode;
            }

            string latest = string.IsNullOrWhiteSpace(artifact?.versionName)
                ? manifest.latestClientVersion
                : artifact.versionName;
            return SemanticVersion.Compare(
                       Application.version,
                       manifest.minimumClientVersion) < 0 ||
                   SemanticVersion.Compare(
                       Application.version,
                       latest) < 0;
        }

        private static IEnumerable<RemoteContentPackage>
            SelectPackagesForCurrentPlatform(
                IEnumerable<RemoteContentPackage> packages)
        {
            string platform = Application.platform switch
            {
                RuntimePlatform.Android => "android",
                RuntimePlatform.WindowsPlayer => "windows",
                RuntimePlatform.WindowsEditor => "windows",
                _ => "any"
            };
            return (packages ?? Array.Empty<RemoteContentPackage>())
                .Where(package => package != null)
                .Where(package =>
                    string.IsNullOrWhiteSpace(package.platform) ||
                    string.Equals(package.platform, "any",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(package.platform, platform,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string ReadInstalledContentVersion()
        {
            try
            {
                string container = Path.Combine(
                    Application.persistentDataPath,
                    "ArcaneArena",
                    "RemoteContent",
                    "Ygo");
                string patchPath = Path.Combine(container, "patches.json");
                if (File.Exists(patchPath))
                {
                    ActivePatchPointer patches = JsonUtility.FromJson<
                        ActivePatchPointer>(File.ReadAllText(patchPath));
                    if (!string.IsNullOrWhiteSpace(patches?.contentVersion))
                        return patches.contentVersion;
                }
                string path = Path.Combine(container, "active.json");
                if (!File.Exists(path)) return "0.0.0";
                ActiveContentPointer pointer = JsonUtility.FromJson<
                    ActiveContentPointer>(File.ReadAllText(path));
                return string.IsNullOrWhiteSpace(pointer?.contentVersion)
                    ? "0.0.0"
                    : pointer.contentVersion;
            }
            catch
            {
                return "0.0.0";
            }
        }

        private static void ValidateEssentialYgoContent(string root)
        {
            string[] required =
            {
                Path.Combine(root, "Data", "cards.bin"),
                Path.Combine(root, "Data", "card-texts.json"),
                Path.Combine(root, "Scripts", "constant.lua"),
                Path.Combine(root, "Scripts", "utility.lua"),
                Path.Combine(root, "Visual", "card-visuals.json")
            };
            foreach (string file in required)
            {
                if (!File.Exists(file))
                    throw new FileNotFoundException(
                        "O pacote de duelo está incompleto.",
                        file);
            }
        }

        private static string ComputeSha256(string path)
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static string FormatChanges(IEnumerable<string> changes)
        {
            string[] clean = (changes ?? Array.Empty<string>())
                .Where(change => !string.IsNullOrWhiteSpace(change))
                .Take(5)
                .Select(change => "\n• " + change.Trim())
                .ToArray();
            return clean.Length == 0
                ? string.Empty
                : "\n\nNOVIDADES" + string.Concat(clean);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "TAMANHO CALCULADO NO DOWNLOAD";
            string[] units = { "B", "KB", "MB", "GB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024d && unit < units.Length - 1)
            {
                value /= 1024d;
                unit++;
            }
            return value.ToString(value >= 10d ? "0" : "0.0",
                       CultureInfo.InvariantCulture) + " " + units[unit];
        }

        private static string SafeFileName(string value)
        {
            string source = string.IsNullOrWhiteSpace(value)
                ? "release"
                : value.Trim();
            var result = new StringBuilder(source.Length);
            foreach (char character in source)
            {
                result.Append(char.IsLetterOrDigit(character) ||
                              character == '-' || character == '_'
                    ? character
                    : '-');
            }
            return result.ToString().Trim('-');
        }

        private static void EnsureChildPath(string parent, string child)
        {
            string prefix = Path.GetFullPath(parent).TrimEnd(
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(child);
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Destino de atualização inseguro.");
        }

        private static void WriteTextAtomically(string path, string contents)
        {
            string temporary = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path) ??
                                      Application.persistentDataPath);
            File.WriteAllText(temporary, contents ?? string.Empty);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }

        private static string CachePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "RemoteUpdates",
                CacheFileName);
        }

        private static string TrustedStatePath()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "RemoteUpdates",
                TrustedStateFileName);
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
            if (_instance == this)
                _instance = null;
        }

        public static class SemanticVersion
        {
            public static int Compare(string left, string right)
            {
                int[] a = Parse(left);
                int[] b = Parse(right);
                for (int index = 0; index < 4; index++)
                {
                    int comparison = a[index].CompareTo(b[index]);
                    if (comparison != 0)
                        return comparison;
                }
                return 0;
            }

            private static int[] Parse(string value)
            {
                string clean = (value ?? string.Empty).Trim();
                int separator = clean.IndexOfAny(new[] { '-', '+' });
                if (separator >= 0)
                    clean = clean.Substring(0, separator);
                string[] parts = clean.Split('.');
                int[] result = new int[4];
                for (int index = 0;
                     index < result.Length && index < parts.Length;
                     index++)
                {
                    string digits = new string(parts[index]
                        .TakeWhile(char.IsDigit)
                        .ToArray());
                    if (!int.TryParse(digits, out result[index]))
                        result[index] = 0;
                }
                return result;
            }
        }
    }
}
