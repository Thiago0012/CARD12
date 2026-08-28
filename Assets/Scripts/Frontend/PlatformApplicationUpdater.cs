using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ArcaneArena.Frontend
{
    internal enum ApplicationUpdateLaunchResult
    {
        Started,
        Preparing,
        PermissionRequested
    }

    internal readonly struct AndroidInstallSnapshot
    {
        public AndroidInstallSnapshot(
            string state,
            float progress,
            string message)
        {
            State = state ?? string.Empty;
            Progress = Mathf.Clamp01(progress);
            Message = message ?? string.Empty;
        }

        public string State { get; }
        public float Progress { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Downloads a full client artifact, verifies it and hands installation to
    /// the operating system. Android uses PackageInstaller; Windows performs a
    /// transactional file swap after this process exits.
    /// </summary>
    internal static class PlatformApplicationUpdater
    {
        [Serializable]
        private sealed class WindowsUpdatePlan
        {
            public int schemaVersion = 2;
            public int processId;
            public string operationId;
            public string versionName;
            public string stagingDirectory;
            public string installDirectory;
            public string backupDirectory;
            public string executableName;
            public string resultPath;
            public string managedFilesManifestName;
        }

        [Serializable]
        private sealed class WindowsManagedFilesManifest
        {
            public int schemaVersion = 1;
            public string[] files = Array.Empty<string>();
        }

        [Serializable]
        private sealed class WindowsUpdateResult
        {
            public bool success;
            public string state;
            public string message;
            public string error;
            public string operationId;
            public string versionName;
            public string appliedUtc;
            public string failedUtc;
        }

        private const string AndroidBridgeClass =
            "com.arcaneduel.updater.AndroidUpdateBridge";

        private const string WindowsManagedFilesManifestName =
            ".master-duel-update-files.json";
        private const string WindowsLastResultFileName =
            "last-windows-update-result.json";
        private static readonly TimeSpan WindowsAbandonedOperationAge =
            TimeSpan.FromHours(12);
        private const long WindowsUpdateSafetyReserveBytes =
            128L * 1024L * 1024L;
        private const long AndroidUpdateSafetyReserveBytes =
            128L * 1024L * 1024L;

        public static long GetInstalledAndroidVersionCode()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass(AndroidBridgeClass);
                return bridge.CallStatic<long>(
                    "getInstalledVersionCode",
                    activity);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Android] Não foi possível ler o versionCode: " +
                    exception.Message);
                return 0;
            }
#else
            return 0;
#endif
        }

        private static long GetAvailableAndroidUpdateBytes()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass(AndroidBridgeClass);
                return Math.Max(0L, bridge.CallStatic<long>(
                    "getAvailableUpdateBytes",
                    activity));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Android] Não foi possível consultar o " +
                    "espaço livre: " + exception.Message);
                return 0L;
            }
#else
            return 0L;
#endif
        }

        public static AndroidInstallSnapshot GetAndroidInstallSnapshot()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass(AndroidBridgeClass);
                return new AndroidInstallSnapshot(
                    bridge.CallStatic<string>("getInstallState", activity),
                    bridge.CallStatic<float>("getInstallProgress", activity),
                    bridge.CallStatic<string>("getInstallMessage", activity));
            }
            catch (Exception exception)
            {
                return new AndroidInstallSnapshot(
                    "FAILED",
                    0f,
                    "O estado do instalador não pôde ser consultado: " +
                    exception.Message);
            }
#else
            return new AndroidInstallSnapshot("UNSUPPORTED", 0f, string.Empty);
#endif
        }

        public static bool ReopenAndroidInstallerConfirmation()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass(AndroidBridgeClass);
                return bridge.CallStatic<bool>(
                    "reopenPendingUserAction",
                    activity);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Android] A confirmação não foi reaberta: " +
                    exception.Message);
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// Ends an Android PackageInstaller session that can no longer show
        /// its system confirmation. This only clears the updater's own
        /// session; it never uninstalls the currently installed game.
        /// </summary>
        public static void CancelAndroidPendingInstall()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer = new AndroidJavaClass(
                    "com.unity3d.player.UnityPlayer");
                using AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass(AndroidBridgeClass);
                bridge.CallStatic<bool>("cancelPendingInstall", activity);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Android] A sessão pendente não pôde ser " +
                    "cancelada: " + exception.Message);
            }
#endif
        }

        /// <summary>
        /// Removes a verified installer artifact once the operating system has
        /// copied it into its own installation session. Keeping a full APK or
        /// Windows ZIP in persistent data would otherwise make the game look
        /// as though two complete versions were installed.
        /// </summary>
        public static void DiscardDownloadedArtifact(string artifactPath)
        {
            if (string.IsNullOrWhiteSpace(artifactPath))
                return;

            try
            {
                string downloads = Path.GetFullPath(Path.Combine(
                    UpdateRoot(),
                    "downloads"));
                string prefix = downloads.TrimEnd(
                                    Path.DirectorySeparatorChar,
                                    Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
                string candidate = Path.GetFullPath(artifactPath);
                if (!candidate.StartsWith(
                        prefix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    UnityEngine.Debug.LogWarning(
                        "[Atualização] A limpeza recusou um caminho fora da " +
                        "área de downloads do jogo.");
                    return;
                }

                if (File.Exists(candidate))
                    File.Delete(candidate);
                string partial = candidate + ".partial";
                if (File.Exists(partial))
                    File.Delete(partial);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização] Não foi possível liberar o pacote temporário: " +
                    exception.Message);
            }
        }

        /// <summary>
        /// Clears abandoned full-client downloads from older attempts. The
        /// PackageInstaller session owns its own bytes after commit, so a
        /// subsequent launch never needs to retain these files in app storage.
        /// </summary>
        public static void CleanupAbandonedDownloads()
        {
            try
            {
                string downloads = Path.Combine(UpdateRoot(), "downloads");
                if (Directory.Exists(downloads))
                {
                    foreach (string path in Directory.GetFiles(
                                 downloads,
                                 "*",
                                 SearchOption.TopDirectoryOnly))
                    {
                        string extension = Path.GetExtension(path);
                        if (string.Equals(extension, ".apk",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(extension, ".zip",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(extension, ".download",
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(extension, ".partial",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(path);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização] Não foi possível limpar downloads antigos: " +
                    exception.Message);
            }

            CleanupWindowsUpdateWorkspace();
        }

        public static async Task<string> DownloadAndVerifyAsync(
            RemoteClientArtifact artifact,
            int requestTimeoutSeconds,
            Action<float, string> progress)
        {
            ValidateArtifactMetadata(artifact);
            EnsureAndroidUpdateSpace(artifact);
            string extension = Application.platform == RuntimePlatform.Android
                ? ".apk"
                : ".zip";
            string updateRoot = UpdateRoot();
            string downloads = Path.Combine(updateRoot, "downloads");
            Directory.CreateDirectory(downloads);
            string version = SafeFileName(artifact.versionName);
            string ready = Path.Combine(
                downloads,
                artifact.platform + "-" + version + extension);
            string partial = ready + ".partial";

            if (File.Exists(ready))
            {
                try
                {
                    VerifyFile(ready, artifact);
                    progress?.Invoke(0.92f, "PACOTE JÁ VALIDADO");
                    return ready;
                }
                catch
                {
                    File.Delete(ready);
                }
            }

            if (File.Exists(partial))
                File.Delete(partial);

            progress?.Invoke(0.05f, "PREPARANDO DOWNLOAD");
            using (UnityWebRequest request = UnityWebRequest.Get(artifact.url))
            {
                request.timeout = Math.Max(900, requestTimeoutSeconds);
                request.redirectLimit = 8;
                request.SetRequestHeader(
                    "Accept",
                    Application.platform == RuntimePlatform.Android
                        ? "application/vnd.android.package-archive,application/octet-stream"
                        : "application/zip,application/octet-stream");
                request.downloadHandler = new DownloadHandlerFile(partial)
                {
                    removeFileOnAbort = true
                };
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    float value = request.downloadProgress < 0f
                        ? 0.10f
                        : Mathf.Lerp(0.08f, 0.84f, request.downloadProgress);
                    progress?.Invoke(
                        value,
                        "BAIXANDO  " + FormatBytes((long)request.downloadedBytes));
                    await Task.Yield();
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new IOException(
                        "O download respondeu HTTP " + request.responseCode +
                        ": " + request.error);
                }
            }

            progress?.Invoke(0.88f, "VERIFICANDO INTEGRIDADE");
            VerifyFile(partial, artifact);
            if (File.Exists(ready))
                File.Delete(ready);
            File.Move(partial, ready);
            progress?.Invoke(0.94f, "PACOTE AUTÊNTICO");
            return ready;
        }

        private static void EnsureAndroidUpdateSpace(
            RemoteClientArtifact artifact)
        {
            if (Application.platform != RuntimePlatform.Android ||
                artifact == null || artifact.sizeBytes <= 0)
            {
                return;
            }

            long available = GetAvailableAndroidUpdateBytes();
            if (available <= 0)
            {
                // Some Android OEMs do not expose StatFs to the Unity process.
                // The verified download still has its normal write error path.
                return;
            }

            long required;
            try
            {
                required = checked(artifact.sizeBytes * 2L +
                                   AndroidUpdateSafetyReserveBytes);
            }
            catch (OverflowException)
            {
                throw new InvalidDataException(
                    "O tamanho informado para a atualização Android é inválido.");
            }

            if (available < required)
            {
                throw new IOException(
                    "Espaço livre insuficiente para preparar esta atualização. " +
                    "Libere ao menos " + FormatBytes(required - available) +
                    ". O Android precisa manter temporariamente a cópia " +
                    "baixada e a cópia de instalação.");
            }
        }

        public static ApplicationUpdateLaunchResult BeginInstall(
            RemoteClientArtifact artifact,
            string artifactPath)
        {
            VerifyFile(artifactPath, artifact);
            return Application.platform switch
            {
                RuntimePlatform.Android => BeginAndroidInstall(
                    artifact,
                    artifactPath),
                RuntimePlatform.WindowsPlayer => BeginWindowsInstall(
                    artifact,
                    artifactPath),
                _ => throw new PlatformNotSupportedException(
                    "A instalação integrada está disponível no Android e Windows.")
            };
        }

        private static ApplicationUpdateLaunchResult BeginAndroidInstall(
            RemoteClientArtifact artifact,
            string artifactPath)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass(
                "com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity =
                unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass(AndroidBridgeClass);
            bool allowed = bridge.CallStatic<bool>(
                "canRequestPackageInstalls",
                activity);
            if (!allowed)
            {
                bridge.CallStatic("openInstallPermissionSettings", activity);
                return ApplicationUpdateLaunchResult.PermissionRequested;
            }

            string result = bridge.CallStatic<string>(
                "installPackage",
                activity,
                Path.GetFullPath(artifactPath),
                Application.identifier,
                artifact.signingCertificateSha256 ?? string.Empty);
            if (string.IsNullOrWhiteSpace(result) ||
                result.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(result)
                        ? "O Android não iniciou o instalador do pacote."
                        : result.Substring("ERROR:".Length).Trim());
            }
            if (result.StartsWith(
                    "INSTALL_PREPARING",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ApplicationUpdateLaunchResult.Preparing;
            }
            return ApplicationUpdateLaunchResult.Started;
#else
            throw new PlatformNotSupportedException(
                "O instalador Android só pode ser executado no aparelho.");
#endif
        }

        private static ApplicationUpdateLaunchResult BeginWindowsInstall(
            RemoteClientArtifact artifact,
            string artifactPath)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            string installDirectory = Directory.GetParent(Application.dataPath)
                ?.FullName;
            if (string.IsNullOrWhiteSpace(installDirectory) ||
                !Directory.Exists(installDirectory))
            {
                throw new DirectoryNotFoundException(
                    "A pasta de instalação do jogo não foi localizada.");
            }

            EnsureInstallDirectoryIsWritable(installDirectory);
            string updateRoot = UpdateRoot();
            string stagingRoot = Path.Combine(updateRoot, "staging");
            string operationsRoot = Path.Combine(updateRoot, "operations");
            string operationId = DateTime.UtcNow.ToString("yyyyMMddHHmmss") +
                                 "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string releaseRoot = null;
            string operationRoot = null;
            try
            {
                // A full release needs temporary room for the extracted
                // payload, rollback files and the new copies being written.
                // Check before changing the installation, rather than relying
                // on a late disk-full error during the transaction.
                EnsureWindowsUpdateFreeSpace(
                    artifactPath,
                    stagingRoot,
                    installDirectory);

                releaseRoot = CreateWindowsStagingDirectory(
                    stagingRoot,
                    operationId);
                ExtractZipSafely(artifactPath, releaseRoot);
                string payloadRoot = ResolvePayloadRoot(
                    releaseRoot,
                    artifact.executableName);
                string executableName = ResolveExecutableName(
                    payloadRoot,
                    artifact.executableName);
                WriteWindowsManagedFilesManifest(payloadRoot);

                operationRoot = Path.Combine(operationsRoot, operationId);
                if (!IsDirectChildDirectory(operationRoot, operationsRoot) ||
                    Directory.Exists(operationRoot))
                {
                    throw new IOException(
                        "A operação temporária de atualização é inválida.");
                }
                Directory.CreateDirectory(operationRoot);

                string scriptPath = Path.Combine(operationRoot, "apply-update.ps1");
                string planPath = Path.Combine(operationRoot, "plan.json");
                string resultPath = Path.Combine(operationRoot, "result.json");
                var plan = new WindowsUpdatePlan
                {
                    operationId = operationId,
                    versionName = artifact.versionName,
                    processId = Process.GetCurrentProcess().Id,
                    stagingDirectory = payloadRoot,
                    installDirectory = installDirectory,
                    backupDirectory = Path.Combine(operationRoot, "backup"),
                    executableName = executableName,
                    resultPath = resultPath,
                    managedFilesManifestName = WindowsManagedFilesManifestName
                };
                File.WriteAllText(planPath, JsonUtility.ToJson(plan, true));
                File.WriteAllText(scriptPath, WindowsUpdateScript);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoLogo -NoProfile -NonInteractive " +
                                "-ExecutionPolicy Bypass -File " + Quote(scriptPath) +
                                " -PlanPath " + Quote(planPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = operationRoot
                };
                Process helper = Process.Start(startInfo);
                if (helper == null)
                    throw new InvalidOperationException(
                        "O processo seguro de atualização não foi iniciado.");

                // Keep the verified ZIP until the helper really starts.  If
                // PowerShell cannot be launched the caller can retry without
                // downloading the entire client again.
                DiscardDownloadedArtifact(artifactPath);
                Application.Quit();
                return ApplicationUpdateLaunchResult.Started;
            }
            catch
            {
                // Only directories created for this exact operation are
                // removed.  Other interrupted updates remain recoverable.
                if (!string.IsNullOrWhiteSpace(operationRoot))
                    TryDeleteManagedDirectory(operationRoot, operationsRoot);
                if (!string.IsNullOrWhiteSpace(releaseRoot))
                    TryDeleteManagedDirectory(releaseRoot, stagingRoot);
                throw;
            }
#else
            throw new PlatformNotSupportedException(
                "O instalador Windows só pode ser executado na build do jogo.");
#endif
        }

        private static void ValidateArtifactMetadata(RemoteClientArtifact artifact)
        {
            if (artifact == null)
                throw new InvalidDataException("O manifesto não possui o pacote.");
            if (!Uri.TryCreate(artifact.url, UriKind.Absolute, out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException(
                    "O pacote precisa usar um endereço HTTPS válido.");
            }
            if (artifact.sizeBytes <= 0)
                throw new InvalidDataException(
                    "O manifesto não informa o tamanho do pacote.");
            string hash = NormalizeHash(artifact.sha256);
            if (hash.Length != 64 || !hash.All(Uri.IsHexDigit))
                throw new InvalidDataException(
                    "O manifesto não possui um SHA-256 válido.");
        }

        private static void VerifyFile(
            string path,
            RemoteClientArtifact artifact)
        {
            ValidateArtifactMetadata(artifact);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "O pacote de atualização não foi encontrado.",
                    path);
            long length = new FileInfo(path).Length;
            if (length != artifact.sizeBytes)
                throw new InvalidDataException(
                    "O pacote chegou com tamanho diferente do publicado.");
            using Stream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            string actual = BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            if (!string.Equals(
                    actual,
                    NormalizeHash(artifact.sha256),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicException(
                    "O pacote falhou na verificação SHA-256.");
            }
        }

        private static void ExtractZipSafely(string zipPath, string destination)
        {
            string root = Path.GetFullPath(destination).TrimEnd(
                              Path.DirectorySeparatorChar,
                              Path.AltDirectorySeparatorChar) +
                          Path.DirectorySeparatorChar;
            long compressedLength = new FileInfo(zipPath).Length;
            long maximumExpandedBytes = Math.Max(
                1024L * 1024L * 1024L,
                Math.Min(8L * 1024L * 1024L * 1024L, compressedLength * 30L));
            long expandedBytes = 0;
            int fileCount = 0;

            using Stream input = File.OpenRead(zipPath);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string relative = entry.FullName.Replace(
                    '/',
                    Path.DirectorySeparatorChar);
                string output = Path.GetFullPath(Path.Combine(root, relative));
                if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("O ZIP contém um caminho inseguro.");
                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(output);
                    continue;
                }

                fileCount++;
                expandedBytes += entry.Length;
                if (fileCount > 50000 || expandedBytes > maximumExpandedBytes)
                    throw new InvalidDataException(
                        "O pacote excede os limites seguros de extração.");
                Directory.CreateDirectory(Path.GetDirectoryName(output) ?? root);
                using Stream source = entry.Open();
                using var target = new FileStream(
                    output,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);
                source.CopyTo(target);
            }
        }

        private static string ResolvePayloadRoot(
            string stagingRoot,
            string expectedExecutable)
        {
            if (ContainsExpectedExecutable(stagingRoot, expectedExecutable))
                return stagingRoot;
            string[] directories = Directory.GetDirectories(stagingRoot);
            string[] files = Directory.GetFiles(stagingRoot);
            if (files.Length == 0 && directories.Length == 1 &&
                ContainsExpectedExecutable(directories[0], expectedExecutable))
            {
                return directories[0];
            }
            throw new InvalidDataException(
                "O pacote Windows não contém o executável esperado na raiz.");
        }

        private static bool ContainsExpectedExecutable(
            string directory,
            string expectedExecutable)
        {
            if (!string.IsNullOrWhiteSpace(expectedExecutable))
                return File.Exists(Path.Combine(directory, expectedExecutable));
            return Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
                .Any(path => Path.GetFileName(path).IndexOf(
                    "MasterDuel2PlusUltra",
                    StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string ResolveExecutableName(
            string directory,
            string expectedExecutable)
        {
            if (!string.IsNullOrWhiteSpace(expectedExecutable))
            {
                string safe = Path.GetFileName(expectedExecutable);
                if (!string.Equals(
                        safe,
                        expectedExecutable,
                        StringComparison.Ordinal) ||
                    !File.Exists(Path.Combine(directory, safe)))
                {
                    throw new InvalidDataException(
                        "O nome do executável publicado é inválido.");
                }
                return safe;
            }
            return Path.GetFileName(Directory.GetFiles(
                    directory,
                    "*.exe",
                    SearchOption.TopDirectoryOnly)
                .First(path => Path.GetFileName(path).IndexOf(
                    "MasterDuel2PlusUltra",
                    StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static void EnsureInstallDirectoryIsWritable(string directory)
        {
            string probe = Path.Combine(
                directory,
                ".master-duel-update-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (File.Create(probe)) { }
            }
            catch (Exception exception)
            {
                throw new UnauthorizedAccessException(
                    "A pasta do jogo não permite atualização. Mova a build " +
                    "para uma pasta do usuário ou execute com permissão adequada.",
                    exception);
            }
            finally
            {
                try
                {
                    if (File.Exists(probe))
                        File.Delete(probe);
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogWarning(
                        "[Atualização Windows] O arquivo temporário de " +
                        "permissão não pôde ser removido: " +
                        exception.Message);
                }
            }
        }

        private static string CreateWindowsStagingDirectory(
            string stagingRoot,
            string operationId)
        {
            if (!IsSafeManagedRelativePath(operationId))
                throw new IOException(
                    "O identificador da área temporária é inválido.");

            string root = Path.GetFullPath(stagingRoot);
            string target = Path.Combine(root, operationId);
            if (!IsDirectChildDirectory(target, root) ||
                Directory.Exists(target))
            {
                throw new IOException(
                    "A área temporária desta atualização já existe ou é " +
                    "insegura.");
            }

            Directory.CreateDirectory(target);
            return Path.GetFullPath(target);
        }

        /// <summary>
        /// Calculates a conservative space requirement before extracting a
        /// Windows release. The ZIP is already stored in the downloads area;
        /// this check covers the extra space needed for staging, rollback and
        /// the replacement copies in the installation directory.
        /// </summary>
        private static void EnsureWindowsUpdateFreeSpace(
            string zipPath,
            string stagingRoot,
            string installDirectory)
        {
            long expandedBytes = ReadWindowsArchiveExpandedBytes(zipPath);
            if (expandedBytes <= 0)
                throw new InvalidDataException(
                    "O pacote Windows não possui arquivos para instalar.");

            // At the point of the swap, a worst-case release needs one
            // expanded copy in staging, one rollback copy and one new copy at
            // the installation. Group the requirements by volume so that a
            // game installed on a different drive is measured correctly.
            var requiredByVolume = new System.Collections.Generic.Dictionary<
                string,
                long>(StringComparer.OrdinalIgnoreCase);
            AddWindowsSpaceRequirement(
                requiredByVolume,
                stagingRoot,
                checked(expandedBytes * 2L));
            AddWindowsSpaceRequirement(
                requiredByVolume,
                installDirectory,
                expandedBytes);

            foreach (var requirement in requiredByVolume)
            {
                long required = checked(
                    requirement.Value + WindowsUpdateSafetyReserveBytes);
                var drive = new DriveInfo(requirement.Key);
                if (!drive.IsReady)
                {
                    throw new IOException(
                        "A unidade " + drive.Name +
                        " não está pronta para concluir a atualização.");
                }
                if (drive.AvailableFreeSpace < required)
                {
                    long missing = required - drive.AvailableFreeSpace;
                    throw new IOException(
                        "Espaço livre insuficiente na unidade " +
                        drive.Name + ". Libere pelo menos " +
                        FormatBytes(missing) +
                        " antes de instalar a atualização; o jogo mantém " +
                        "uma cópia de segurança para poder reverter com " +
                        "segurança.");
                }
            }
        }

        private static long ReadWindowsArchiveExpandedBytes(string zipPath)
        {
            long expandedBytes = 0;
            using Stream input = File.OpenRead(zipPath);
            using var archive = new ZipArchive(input, ZipArchiveMode.Read, false);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;
                expandedBytes = checked(expandedBytes + Math.Max(0, entry.Length));
            }
            return expandedBytes;
        }

        private static void AddWindowsSpaceRequirement(
            System.Collections.Generic.IDictionary<string, long> requirements,
            string path,
            long bytes)
        {
            if (bytes < 0)
                throw new ArgumentOutOfRangeException(nameof(bytes));

            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                throw new IOException(
                    "Não foi possível determinar a unidade da atualização.");
            }

            string volume = new DriveInfo(root).RootDirectory.FullName;
            requirements.TryGetValue(volume, out long current);
            requirements[volume] = checked(current + bytes);
        }

        /// <summary>
        /// Records exactly which files belong to a Windows release.  On a
        /// later update the helper may safely remove only files that were
        /// installed by an earlier release and are no longer in the new one;
        /// it never treats arbitrary player files as update payload.
        /// </summary>
        private static void WriteWindowsManagedFilesManifest(string payloadRoot)
        {
            string root = FullPathWithTrailingSeparator(payloadRoot);
            var managedFiles = Directory.GetFiles(
                    payloadRoot,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path => Path.GetFullPath(path))
                .Where(path => path.StartsWith(
                    root,
                    StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Substring(root.Length))
                .Where(relative => !string.Equals(
                    relative,
                    WindowsManagedFilesManifestName,
                    StringComparison.OrdinalIgnoreCase))
                .Where(IsSafeManagedRelativePath)
                .OrderBy(relative => relative, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string manifestPath = Path.Combine(
                payloadRoot,
                WindowsManagedFilesManifestName);
            File.WriteAllText(
                manifestPath,
                JsonUtility.ToJson(new WindowsManagedFilesManifest
                {
                    files = managedFiles
                }, true));
        }

        /// <summary>
        /// A Windows update needs a temporary copy of the incoming release
        /// and a rollback copy of overwritten files while its helper runs.
        /// They are deliberately kept through the process restart, then this
        /// routine removes only completed/clearly abandoned workspaces inside
        /// the game's private RemoteUpdates folder.
        /// </summary>
        private static void CleanupWindowsUpdateWorkspace()
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
            {
                return;
            }

            try
            {
                string updateRoot = UpdateRoot();
                string operationsRoot = Path.Combine(updateRoot, "operations");
                string stagingRoot = Path.Combine(updateRoot, "staging");
                if (!Directory.Exists(operationsRoot) &&
                    !Directory.Exists(stagingRoot))
                {
                    return;
                }

                var activeStagingDirectories =
                    new System.Collections.Generic.HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
                var completedOperations =
                    new System.Collections.Generic.List<WindowsUpdateWorkspace>();
                DateTime now = DateTime.UtcNow;

                if (Directory.Exists(operationsRoot))
                {
                    foreach (string operationDirectory in Directory.GetDirectories(
                                 operationsRoot,
                                 "*",
                                 SearchOption.TopDirectoryOnly))
                    {
                        if (!TryReadWindowsUpdateWorkspace(
                                operationDirectory,
                                operationsRoot,
                                stagingRoot,
                                out WindowsUpdateWorkspace workspace))
                        {
                            continue;
                        }

                        if (workspace.Result != null)
                        {
                            completedOperations.Add(workspace);
                            continue;
                        }

                        DateTime lastWriteUtc = Directory.GetLastWriteTimeUtc(
                            workspace.OperationDirectory);
                        if (now - lastWriteUtc >= WindowsAbandonedOperationAge)
                        {
                            workspace.Result = new WindowsUpdateResult
                            {
                                success = false,
                                state = "ABANDONED",
                                message = "A atualização do Windows foi " +
                                          "interrompida antes da conclusão.",
                                operationId = workspace.Plan.operationId,
                                versionName = workspace.Plan.versionName,
                                failedUtc = now.ToString("o")
                            };
                            completedOperations.Add(workspace);
                        }
                        else
                        {
                            activeStagingDirectories.Add(
                                workspace.StagingReleaseDirectory);
                        }
                    }
                }

                foreach (WindowsUpdateWorkspace workspace in completedOperations)
                {
                    PersistWindowsUpdateResult(workspace.Result);
                    LogWindowsUpdateResult(workspace.Result);

                    // Do not remove a release folder if another, nonterminal
                    // operation still references it.
                    if (!activeStagingDirectories.Contains(
                            workspace.StagingReleaseDirectory))
                    {
                        TryDeleteManagedDirectory(
                            workspace.StagingReleaseDirectory,
                            stagingRoot);
                    }

                    TryDeleteManagedDirectory(
                        workspace.OperationDirectory,
                        operationsRoot);
                }

                CleanupOrphanedWindowsStagingDirectories(
                    stagingRoot,
                    activeStagingDirectories,
                    now);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Windows] Não foi possível limpar a " +
                    "área temporária: " + exception.Message);
            }
        }

        private sealed class WindowsUpdateWorkspace
        {
            public string OperationDirectory;
            public string StagingReleaseDirectory;
            public WindowsUpdatePlan Plan;
            public WindowsUpdateResult Result;
        }

        private static bool TryReadWindowsUpdateWorkspace(
            string operationDirectory,
            string operationsRoot,
            string stagingRoot,
            out WindowsUpdateWorkspace workspace)
        {
            workspace = null;
            if (!IsDirectChildDirectory(operationDirectory, operationsRoot))
                return false;

            string planPath = Path.Combine(operationDirectory, "plan.json");
            if (!TryReadJson(planPath, out WindowsUpdatePlan plan) ||
                string.IsNullOrWhiteSpace(plan.stagingDirectory) ||
                string.IsNullOrWhiteSpace(plan.backupDirectory) ||
                string.IsNullOrWhiteSpace(plan.resultPath))
            {
                return false;
            }

            string expectedResultPath = Path.Combine(
                operationDirectory,
                "result.json");
            if (!IsPathInside(plan.backupDirectory, operationDirectory) ||
                !string.Equals(
                    Path.GetFullPath(plan.resultPath),
                    Path.GetFullPath(expectedResultPath),
                    StringComparison.OrdinalIgnoreCase) ||
                !TryGetStagingReleaseDirectory(
                    plan.stagingDirectory,
                    stagingRoot,
                    out string stagingReleaseDirectory))
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Windows] Uma operação temporária com " +
                    "caminhos inválidos foi mantida para inspeção.");
                return false;
            }

            TryReadJson(expectedResultPath, out WindowsUpdateResult result);
            workspace = new WindowsUpdateWorkspace
            {
                OperationDirectory = Path.GetFullPath(operationDirectory),
                StagingReleaseDirectory = stagingReleaseDirectory,
                Plan = plan,
                Result = result
            };
            return true;
        }

        private static void CleanupOrphanedWindowsStagingDirectories(
            string stagingRoot,
            System.Collections.Generic.ISet<string> activeDirectories,
            DateTime now)
        {
            if (!Directory.Exists(stagingRoot))
                return;

            foreach (string directory in Directory.GetDirectories(
                         stagingRoot,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                if (!IsDirectChildDirectory(directory, stagingRoot) ||
                    activeDirectories.Contains(Path.GetFullPath(directory)))
                {
                    continue;
                }

                DateTime lastWriteUtc = Directory.GetLastWriteTimeUtc(directory);
                if (now - lastWriteUtc >= WindowsAbandonedOperationAge)
                {
                    TryDeleteManagedDirectory(directory, stagingRoot);
                }
            }
        }

        private static bool TryGetStagingReleaseDirectory(
            string stagingDirectory,
            string stagingRoot,
            out string releaseDirectory)
        {
            releaseDirectory = null;
            if (!IsPathInside(stagingDirectory, stagingRoot))
                return false;

            string root = FullPathWithTrailingSeparator(stagingRoot);
            string candidate = Path.GetFullPath(stagingDirectory);
            string relative = candidate.Substring(root.Length);
            string[] parts = relative.Split(
                new[]
                {
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || !IsSafeManagedRelativePath(parts[0]))
                return false;

            string release = Path.Combine(stagingRoot, parts[0]);
            if (!IsDirectChildDirectory(release, stagingRoot))
                return false;

            releaseDirectory = Path.GetFullPath(release);
            return true;
        }

        private static void PersistWindowsUpdateResult(WindowsUpdateResult result)
        {
            if (result == null)
                return;

            string path = Path.Combine(UpdateRoot(), WindowsLastResultFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? UpdateRoot());
            File.WriteAllText(path, JsonUtility.ToJson(new WindowsUpdateResult
            {
                success = result.success,
                state = LimitStatusText(result.state, 48),
                message = LimitStatusText(
                    string.IsNullOrWhiteSpace(result.message)
                        ? result.error
                        : result.message,
                    512),
                error = LimitStatusText(result.error, 512),
                operationId = LimitStatusText(result.operationId, 64),
                versionName = LimitStatusText(result.versionName, 96),
                appliedUtc = LimitStatusText(result.appliedUtc, 48),
                failedUtc = LimitStatusText(result.failedUtc, 48)
            }, true));
        }

        private static void LogWindowsUpdateResult(WindowsUpdateResult result)
        {
            string version = string.IsNullOrWhiteSpace(result?.versionName)
                ? string.Empty
                : " " + result.versionName;
            string message = string.IsNullOrWhiteSpace(result?.message)
                ? result?.error
                : result.message;
            if (result != null && result.success)
            {
                UnityEngine.Debug.Log(
                    "[Atualização Windows] Atualização" + version +
                    " concluída; arquivos temporários foram liberados.");
            }
            else
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Windows] A última atualização" + version +
                    " não foi concluída: " +
                    (string.IsNullOrWhiteSpace(message)
                        ? "sem detalhe informado."
                        : message));
            }
        }

        private static bool TryReadJson<T>(string path, out T value)
            where T : class
        {
            value = null;
            try
            {
                if (!File.Exists(path))
                    return false;
                value = JsonUtility.FromJson<T>(File.ReadAllText(path));
                return value != null;
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Windows] Não foi possível ler um registro " +
                    "temporário: " + exception.Message);
                return false;
            }
        }

        private static void TryDeleteManagedDirectory(string target, string parent)
        {
            try
            {
                if (!IsDirectChildDirectory(target, parent))
                {
                    UnityEngine.Debug.LogWarning(
                        "[Atualização Windows] A limpeza recusou um " +
                        "diretório fora da área temporária do jogo.");
                    return;
                }
                if (Directory.Exists(target))
                    Directory.Delete(target, true);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização Windows] Um diretório temporário não pôde " +
                    "ser removido: " + exception.Message);
            }
        }

        private static bool IsDirectChildDirectory(string candidate, string parent)
        {
            try
            {
                string candidateFull = Path.GetFullPath(candidate).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string parentFull = Path.GetFullPath(parent).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string actualParent = Path.GetDirectoryName(candidateFull)?.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                return !string.IsNullOrWhiteSpace(actualParent) &&
                       string.Equals(
                           actualParent,
                           parentFull,
                           StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool IsPathInside(string candidate, string parent)
        {
            try
            {
                return Path.GetFullPath(candidate).StartsWith(
                    FullPathWithTrailingSeparator(parent),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string FullPathWithTrailingSeparator(string path)
        {
            return Path.GetFullPath(path).TrimEnd(
                       Path.DirectorySeparatorChar,
                       Path.AltDirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        }

        private static bool IsSafeManagedRelativePath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) ||
                Path.IsPathRooted(relative) ||
                relative.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return relative.IndexOfAny(Path.GetInvalidPathChars()) < 0;
        }

        private static string LimitStatusText(string value, int maximumLength)
        {
            string text = value ?? string.Empty;
            return text.Length <= maximumLength
                ? text
                : text.Substring(0, maximumLength);
        }

        private static string UpdateRoot()
        {
            return Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "RemoteUpdates");
        }

        private static string SafeFileName(string value)
        {
            string source = string.IsNullOrWhiteSpace(value)
                ? "release"
                : value.Trim();
            return new string(source.Select(character =>
                    char.IsLetterOrDigit(character) || character == '-' ||
                    character == '_' || character == '.'
                        ? character
                        : '-')
                .ToArray())
                .Trim('-', '.');
        }

        private static string NormalizeHash(string value)
        {
            return (value ?? string.Empty)
                .Replace("-", string.Empty)
                .Replace(":", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024d && unit < units.Length - 1)
            {
                value /= 1024d;
                unit++;
            }
            return value.ToString(value >= 10d ? "0" : "0.0") + " " +
                   units[unit];
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        }

        private const string WindowsUpdateScript = @"
param([Parameter(Mandatory=$true)][string]$PlanPath)
$ErrorActionPreference = 'Stop'
$manifestName = '.master-duel-update-files.json'

function Get-FullPath([string]$path) {
    return [IO.Path]::GetFullPath($path)
}

function Get-Prefix([string]$path) {
    return (Get-FullPath $path).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
}

function Test-PathInside([string]$candidate, [string]$parent) {
    return (Get-FullPath $candidate).StartsWith((Get-Prefix $parent), [StringComparison]::OrdinalIgnoreCase)
}

function Test-DirectChild([string]$candidate, [string]$parent) {
    $candidatePath = (Get-FullPath $candidate).TrimEnd('\','/')
    $parentPath = (Get-FullPath $parent).TrimEnd('\','/')
    $candidateParent = [IO.Path]::GetDirectoryName($candidatePath)
    return -not [string]::IsNullOrWhiteSpace($candidateParent) -and
        $candidateParent.TrimEnd('\','/').Equals($parentPath, [StringComparison]::OrdinalIgnoreCase)
}

function Get-ManagedFiles([string]$manifestPath, [string]$root, [bool]$required) {
    $files = @{}
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        if ($required) { throw 'O pacote validado não possui o inventário de arquivos.' }
        return $files
    }

    $document = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($null -eq $document -or [int]$document.schemaVersion -ne 1) {
        throw 'O inventário de arquivos da atualização é inválido.'
    }

    foreach ($entry in @($document.files)) {
        $relative = [string]$entry
        if ([string]::IsNullOrWhiteSpace($relative) -or
            [IO.Path]::IsPathRooted($relative) -or
            $relative.Contains('..')) {
            throw 'O inventário contém um caminho inseguro.'
        }
        $resolved = Get-FullPath (Join-Path $root $relative)
        if (-not (Test-PathInside $resolved $root)) {
            throw 'O inventário tentou acessar um caminho fora do pacote.'
        }
        $files[$resolved.Substring((Get-Prefix $root).Length)] = $true
    }
    return $files
}

$planPathFull = Get-FullPath $PlanPath
$operation = Get-FullPath (Split-Path -Parent $planPathFull)
$operationsRoot = Get-FullPath (Split-Path -Parent $operation)
$updateRoot = Get-FullPath (Split-Path -Parent $operationsRoot)
$stagingRoot = Join-Path $updateRoot 'staging'
$plan = Get-Content -LiteralPath $planPathFull -Raw | ConvertFrom-Json
$source = Get-FullPath ([string]$plan.stagingDirectory)
$install = Get-FullPath ([string]$plan.installDirectory)
$backup = Get-FullPath ([string]$plan.backupDirectory)
$result = Get-FullPath ([string]$plan.resultPath)
$exeName = [IO.Path]::GetFileName([string]$plan.executableName)
$installPrefix = Get-Prefix $install
$sourcePrefix = Get-Prefix $source
$canWriteResult = $false
$canStartFallback = $false
$created = [Collections.Generic.List[string]]::new()
$copied = [Collections.Generic.List[string]]::new()
$removed = [Collections.Generic.List[string]]::new()

function Write-Result([bool]$success, [string]$state, [string]$message) {
    $payload = @{
        success = $success
        state = $state
        message = $message
        operationId = [string]$plan.operationId
        versionName = [string]$plan.versionName
    }
    if ($success) {
        $payload.appliedUtc = [DateTime]::UtcNow.ToString('o')
    } else {
        $payload.failedUtc = [DateTime]::UtcNow.ToString('o')
    }
    $payload | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $result -Encoding UTF8
}

function Backup-ExistingFile([string]$relative, [string]$destination) {
    $backupFile = Get-FullPath (Join-Path $backup $relative)
    if (-not (Test-PathInside $backupFile $backup)) {
        throw 'O backup tentou gravar fora da operação temporária.'
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $backupFile) -Force | Out-Null
    Copy-Item -LiteralPath $destination -Destination $backupFile -Force
}

try {
    if (-not ([IO.Path]::GetFileName($operationsRoot).Equals('operations', [StringComparison]::OrdinalIgnoreCase)) -or
        -not (Test-DirectChild $operation $operationsRoot) -or
        -not (Test-PathInside $source $stagingRoot) -or
        -not (Test-PathInside $backup $operation) -or
        -not $result.Equals((Join-Path $operation 'result.json'), [StringComparison]::OrdinalIgnoreCase) -or
        -not ([string]$plan.managedFilesManifestName).Equals($manifestName, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'O plano de atualização possui caminhos inválidos.'
    }
    if ($source -eq $install -or $source.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A área temporária não pode ficar dentro da instalação.'
    }
    if ([string]::IsNullOrWhiteSpace($exeName) -or $exeName -ne [string]$plan.executableName) {
        throw 'O plano possui um nome de executável inválido.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $source $exeName) -PathType Leaf)) {
        throw 'O executável esperado não existe no pacote validado.'
    }
    $canWriteResult = $true
    $canStartFallback = $true
    Wait-Process -Id ([int]$plan.processId) -Timeout 120 -ErrorAction SilentlyContinue
    if (Get-Process -Id ([int]$plan.processId) -ErrorAction SilentlyContinue) {
        throw 'O jogo não encerrou dentro do prazo de segurança.'
    }
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    $newManagedFiles = Get-ManagedFiles (Join-Path $source $manifestName) $source $true
    $previousManagedFiles = Get-ManagedFiles (Join-Path $install $manifestName) $install $false
    $files = Get-ChildItem -LiteralPath $source -File -Recurse -Force
    foreach ($file in $files) {
        $sourceFile = Get-FullPath $file.FullName
        if (-not (Test-PathInside $sourceFile $source) -or
            ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw 'O pacote contém um arquivo de origem inseguro.'
        }
        $relative = $sourceFile.Substring($sourcePrefix.Length)
        $destination = Get-FullPath (Join-Path $install $relative)
        if (-not $destination.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'O pacote tentou gravar fora da instalação.'
        }
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            Backup-ExistingFile $relative $destination
            $copied.Add($relative)
        } else {
            $created.Add($destination)
        }
        Copy-Item -LiteralPath $sourceFile -Destination $destination -Force
    }

    foreach ($relative in $previousManagedFiles.Keys) {
        if ($newManagedFiles.ContainsKey($relative)) { continue }
        $destination = Get-FullPath (Join-Path $install $relative)
        if (-not $destination.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'O inventário tentou remover um caminho fora da instalação.'
        }
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            Backup-ExistingFile $relative $destination
            Remove-Item -LiteralPath $destination -Force
            $removed.Add($relative)
        }
    }

    # Persist the successful transaction before launching the replacement.
    # If the process launch fails, the catch below replaces this journal with
    # FAILED and restores the exact backup. This avoids a rollback racing a
    # newly started executable because of a late disk-write error.
    Write-Result $true 'SUCCESS' 'Os arquivos da nova versão foram aplicados.'
    Start-Process -FilePath (Join-Path $install $exeName) -WorkingDirectory $install
    exit 0
} catch {
    foreach ($relative in $copied) {
        $backupFile = Join-Path $backup $relative
        $destination = Join-Path $install $relative
        if (Test-Path -LiteralPath $backupFile -PathType Leaf) {
            Copy-Item -LiteralPath $backupFile -Destination $destination -Force
        }
    }
    foreach ($relative in $removed) {
        $backupFile = Join-Path $backup $relative
        $destination = Join-Path $install $relative
        if (Test-Path -LiteralPath $backupFile -PathType Leaf) {
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            Copy-Item -LiteralPath $backupFile -Destination $destination -Force
        }
    }
    foreach ($path in $created) {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }
    if ($canWriteResult) {
        Write-Result $false 'FAILED' $_.Exception.Message
    }
    if ($canStartFallback) {
        $oldExe = Join-Path $install $exeName
        if (Test-Path -LiteralPath $oldExe -PathType Leaf) {
            Start-Process -FilePath $oldExe -WorkingDirectory $install
        }
    }
    exit 1
}
";
    }
}
