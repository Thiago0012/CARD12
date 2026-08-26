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
            public int schemaVersion = 1;
            public int processId;
            public string stagingDirectory;
            public string installDirectory;
            public string backupDirectory;
            public string executableName;
            public string resultPath;
        }

        private const string AndroidBridgeClass =
            "com.arcaneduel.updater.AndroidUpdateBridge";

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
                if (!Directory.Exists(downloads))
                    return;
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
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    "[Atualização] Não foi possível limpar downloads antigos: " +
                    exception.Message);
            }
        }

        public static async Task<string> DownloadAndVerifyAsync(
            RemoteClientArtifact artifact,
            int requestTimeoutSeconds,
            Action<float, string> progress)
        {
            ValidateArtifactMetadata(artifact);
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
            string releaseRoot = Path.Combine(
                updateRoot,
                "staging",
                SafeFileName(artifact.versionName));
            RecreateDirectory(releaseRoot, Path.Combine(updateRoot, "staging"));
            ExtractZipSafely(artifactPath, releaseRoot);
            string payloadRoot = ResolvePayloadRoot(
                releaseRoot,
                artifact.executableName);
            string executableName = ResolveExecutableName(
                payloadRoot,
                artifact.executableName);

            // The release has already been extracted and validated. The ZIP
            // is no longer necessary while the helper performs the file swap.
            DiscardDownloadedArtifact(artifactPath);

            string operationId = DateTime.UtcNow.ToString("yyyyMMddHHmmss") +
                                 "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string operationRoot = Path.Combine(updateRoot, "operations", operationId);
            Directory.CreateDirectory(operationRoot);
            string scriptPath = Path.Combine(operationRoot, "apply-update.ps1");
            string planPath = Path.Combine(operationRoot, "plan.json");
            string resultPath = Path.Combine(operationRoot, "result.json");
            var plan = new WindowsUpdatePlan
            {
                processId = Process.GetCurrentProcess().Id,
                stagingDirectory = payloadRoot,
                installDirectory = installDirectory,
                backupDirectory = Path.Combine(operationRoot, "backup"),
                executableName = executableName,
                resultPath = resultPath
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
            Application.Quit();
            return ApplicationUpdateLaunchResult.Started;
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
                if (File.Exists(probe))
                    File.Delete(probe);
            }
        }

        private static void RecreateDirectory(string target, string parent)
        {
            string parentFull = Path.GetFullPath(parent).TrimEnd(
                                    Path.DirectorySeparatorChar,
                                    Path.AltDirectorySeparatorChar) +
                                Path.DirectorySeparatorChar;
            string targetFull = Path.GetFullPath(target);
            if (!targetFull.StartsWith(parentFull, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Diretório temporário de atualização inseguro.");
            if (Directory.Exists(targetFull))
                Directory.Delete(targetFull, true);
            Directory.CreateDirectory(targetFull);
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
$plan = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
$source = [IO.Path]::GetFullPath([string]$plan.stagingDirectory)
$install = [IO.Path]::GetFullPath([string]$plan.installDirectory)
$backup = [IO.Path]::GetFullPath([string]$plan.backupDirectory)
$result = [IO.Path]::GetFullPath([string]$plan.resultPath)
$exeName = [IO.Path]::GetFileName([string]$plan.executableName)
$installPrefix = $install.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
$sourcePrefix = $source.TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
$created = [Collections.Generic.List[string]]::new()
$copied = [Collections.Generic.List[string]]::new()
try {
    if ($source -eq $install -or $source.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A área temporária não pode ficar dentro da instalação.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $source $exeName) -PathType Leaf)) {
        throw 'O executável esperado não existe no pacote validado.'
    }
    Wait-Process -Id ([int]$plan.processId) -Timeout 120 -ErrorAction SilentlyContinue
    if (Get-Process -Id ([int]$plan.processId) -ErrorAction SilentlyContinue) {
        throw 'O jogo não encerrou dentro do prazo de segurança.'
    }
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    $files = Get-ChildItem -LiteralPath $source -File -Recurse
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($sourcePrefix.Length)
        $destination = [IO.Path]::GetFullPath((Join-Path $install $relative))
        if (-not $destination.StartsWith($installPrefix, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'O pacote tentou gravar fora da instalação.'
        }
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        if (Test-Path -LiteralPath $destination -PathType Leaf) {
            $backupFile = Join-Path $backup $relative
            New-Item -ItemType Directory -Path (Split-Path -Parent $backupFile) -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $backupFile -Force
            $copied.Add($relative)
        } else {
            $created.Add($destination)
        }
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
    @{ success=$true; appliedUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath $result -Encoding UTF8
    Start-Process -FilePath (Join-Path $install $exeName) -WorkingDirectory $install
    Remove-Item -LiteralPath $source -Recurse -Force -ErrorAction SilentlyContinue
    exit 0
} catch {
    foreach ($relative in $copied) {
        $backupFile = Join-Path $backup $relative
        $destination = Join-Path $install $relative
        if (Test-Path -LiteralPath $backupFile -PathType Leaf) {
            Copy-Item -LiteralPath $backupFile -Destination $destination -Force
        }
    }
    foreach ($path in $created) {
        Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue
    }
    @{ success=$false; error=$_.Exception.Message; failedUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath $result -Encoding UTF8
    $oldExe = Join-Path $install $exeName
    if (Test-Path -LiteralPath $oldExe -PathType Leaf) {
        Start-Process -FilePath $oldExe -WorkingDirectory $install
    }
    exit 1
}
";
    }
}
