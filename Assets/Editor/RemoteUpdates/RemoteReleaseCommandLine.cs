using System;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Editor.RemoteUpdates
{
    /// <summary>
    /// Non-interactive release entry point used by Build-And-Publish-Release.
    /// It versions, signs and builds both platforms before a release can be
    /// made visible to players.
    /// </summary>
    public static class RemoteReleaseCommandLine
    {
        public static void BuildSignedRelease()
        {
            try
            {
                string projectRoot = ProjectRoot;
                RemoteReleaseManifest previous = ReadCurrentManifest();
                string version = Environment.GetEnvironmentVariable(
                    "MASTER_DUEL_RELEASE_VERSION");
                if (string.IsNullOrWhiteSpace(version))
                    version = NextPatch(previous?.latestClientVersion);
                if (RemoteUpdateRuntime.SemanticVersion.Compare(
                        version,
                        previous?.latestClientVersion ?? "0.0.0") <= 0)
                {
                    throw new InvalidOperationException(
                        "A nova versão precisa ser maior que a publicada.");
                }

                int previousCode = Math.Max(
                    previous?.android?.versionCode ?? 0,
                    PlayerSettings.Android.bundleVersionCode);
                int androidCode = ReadPositiveEnvironmentInt(
                    "MASTER_DUEL_ANDROID_VERSION_CODE",
                    previousCode + 1);
                int protocol = ReadPositiveEnvironmentInt(
                    "MASTER_DUEL_PROTOCOL_VERSION",
                    Math.Max(1, previous?.protocolVersion ?? 1));

                PlayerSettings.bundleVersion = version;
                PlayerSettings.Android.bundleVersionCode = androidCode;
                AssetDatabase.SaveAssets();

                ArcaneDuel.Editor.ArcaneBuildAutomation
                    .BuildWindowsRelease();
                ArcaneDuel.Editor.ArcaneBuildAutomation
                    .BuildAndroidRelease();

                string artifacts = Path.Combine(
                    projectRoot,
                    "ContentStaging",
                    "production",
                    "artifacts");
                string windowsName =
                    "MasterDuel2PlusUltra-Windows-v" + version + ".zip";
                string androidName =
                    "MasterDuel2PlusUltra-Android-v" + version + ".apk";
                string windowsPath = Path.Combine(artifacts, windowsName);
                string androidPath = Path.Combine(artifacts, androidName);
                RequireFile(windowsPath);
                RequireFile(androidPath);

                string tag = "v" + version;
                string releaseBase =
                    "https://github.com/Thiago0012/CARD12/releases/download/" +
                    tag + "/";
                string[] changes = (Environment.GetEnvironmentVariable(
                        "MASTER_DUEL_RELEASE_NOTES") ?? string.Empty)
                    .Split(new[] { '|', '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => value.Length > 0)
                    .Distinct()
                    .ToArray();

                var envelope = new RemoteReleaseEnvelope
                {
                    schemaVersion = 2,
                    payload = new RemoteReleaseManifest
                    {
                        schemaVersion = 2,
                        releaseId = "release-" +
                                    version.Replace('.', '-') + "-" +
                                    DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                        publishedUtc = DateTime.UtcNow.ToString(
                            "yyyy-MM-ddTHH:mm:ssZ"),
                        sequenceNumber = Math.Max(
                            1,
                            (previous?.sequenceNumber ?? 0) + 1),
                        channel = "production",
                        expiresUtc = DateTime.UtcNow.AddDays(365).ToString(
                            "yyyy-MM-ddTHH:mm:ssZ"),
                        protocolVersion = protocol,
                        minimumClientVersion = version,
                        latestClientVersion = version,
                        requiredClientUpdate = true,
                        title = "ATUALIZAÇÃO DO MASTER DUEL 2 PLUS ULTRA",
                        summary = "Nova versão validada para Android e Windows.",
                        changes = changes,
                        windowsUpdateUrl = releaseBase + windowsName,
                        androidUpdateUrl = releaseBase + androidName,
                        fallbackUpdateUrl =
                            "https://github.com/Thiago0012/CARD12/releases/tag/" +
                            tag,
                        windows = Artifact(
                            "windows",
                            version,
                            0,
                            protocol,
                            releaseBase + windowsName,
                            windowsPath,
                            "MasterDuel2PlusUltra.exe"),
                        android = Artifact(
                            "android",
                            version,
                            androidCode,
                            protocol,
                            releaseBase + androidName,
                            androidPath,
                            string.Empty),
                        // A full build already contains its current YGO
                        // content. Do not make a new installation download a
                        // previously published patch on top of that build.
                        contentVersion = previous?.contentVersion ?? "0.0.0",
                        requiredContentUpdate = false,
                        packages = Array.Empty<RemoteContentPackage>()
                    }
                };
                envelope.payload.android.signingCertificateSha256 =
                    ReleaseSigningConfiguration.AndroidCertificateSha256();
                ReleaseSigningConfiguration.SignEnvelope(envelope);
                string json = JsonUtility.ToJson(envelope, true) +
                              Environment.NewLine;
                WriteAtomically(ProductionEnvelopePath, json);
                WriteAtomically(BundledEnvelopePath, json);
                AssetDatabase.Refresh();
                Debug.Log(
                    "REMOTE_RELEASE_READY version=" + version +
                    "; androidVersionCode=" + androidCode +
                    "; protocol=" + protocol +
                    "; artifacts=" + artifacts);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                throw;
            }
        }

        private static RemoteClientArtifact Artifact(
            string platform,
            string version,
            int versionCode,
            int protocol,
            string url,
            string path,
            string executable)
        {
            return new RemoteClientArtifact
            {
                platform = platform,
                versionName = version,
                versionCode = versionCode,
                minimumVersionCode = versionCode,
                protocolVersion = protocol,
                url = url,
                sizeBytes = new FileInfo(path).Length,
                sha256 = Sha256(path),
                executableName = executable
            };
        }

        private static int ReadPositiveEnvironmentInt(
            string name,
            int fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return int.TryParse(value, out int parsed) && parsed > 0
                ? parsed
                : fallback;
        }

        private static string NextPatch(string value)
        {
            string[] parts = (value ?? "0.0.0").Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int a)
                ? a
                : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int b)
                ? b
                : 0;
            int patch = parts.Length > 2 && int.TryParse(parts[2], out int c)
                ? c + 1
                : 1;
            return major + "." + minor + "." + patch;
        }

        private static string Sha256(string path)
        {
            using Stream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static RemoteReleaseManifest ReadCurrentManifest()
        {
            if (!File.Exists(ProductionEnvelopePath))
                return null;
            return JsonUtility.FromJson<RemoteReleaseEnvelope>(
                File.ReadAllText(ProductionEnvelopePath))?.payload;
        }

        private static void RequireFile(string path)
        {
            if (!File.Exists(path) || new FileInfo(path).Length == 0)
                throw new FileNotFoundException(
                    "A build esperada não foi gerada.",
                    path);
        }

        private static void WriteAtomically(string path, string contents)
        {
            string temporary = path + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ProjectRoot);
            File.WriteAllText(temporary, contents);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temporary, path);
        }

        private static string ProjectRoot =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            Application.dataPath;

        private static string ProductionEnvelopePath => Path.Combine(
            ProjectRoot,
            "ContentStaging",
            "production",
            "v2",
            "release-envelope.json");

        private static string BundledEnvelopePath => Path.Combine(
            ProjectRoot,
            "Assets",
            "Resources",
            "RemoteUpdates",
            "BundledReleaseEnvelope.json");
    }
}
