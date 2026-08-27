using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ArcaneDuel.DuelEngine.Content
{
    /// <summary>
    /// Resolves the deterministic YGO content root on every supported platform.
    /// Android packages StreamingAssets inside the APK, so the files required by
    /// the native Core are mirrored once into the application's private storage.
    /// </summary>
    public static class YgoContentLocator
    {
        private const string ContentFolder = "Ygo";
        private const string MarkerFile = ".arcane-content-build";
        private const string PatchManifestFile = "patch-manifest.json";
        private const string PatchPointerFile = "patches.json";
        private const string PatchedCoreMarker = ".arcane-patched-core";
        private const string AndroidMirrorSchema = "essential-v3";
        private const string AndroidMirrorClass =
            "com.arcaneduel.content.StreamingAssetsMirror";
        private static readonly string[] AndroidEagerDirectories =
        {
            "Data",
            "Scripts",
            "CustomScripts",
            "Visual"
        };
        private static readonly object Sync = new object();
        private static string cachedRoot;

        public static string Root
        {
            get
            {
                lock (Sync)
                {
                    if (!string.IsNullOrEmpty(cachedRoot))
                        return cachedRoot;

                    string bundledRoot = ResolveBundledRoot();
                    string remoteRoot = TryResolvePatchedCoreRoot(bundledRoot);
                    if (!string.IsNullOrWhiteSpace(remoteRoot))
                    {
                        ValidateEssentialContent(remoteRoot);
                        cachedRoot = remoteRoot;
                        return cachedRoot;
                    }

                    remoteRoot = TryResolveRemoteContentRoot();
                    if (!string.IsNullOrWhiteSpace(remoteRoot))
                    {
                        ValidateEssentialContent(remoteRoot);
                        cachedRoot = remoteRoot;
                        return cachedRoot;
                    }

                    cachedRoot = bundledRoot;
                    ValidateEssentialContent(cachedRoot);
                    return cachedRoot;
                }
            }
        }

        public static void InvalidateCachedRoot()
        {
            lock (Sync)
                cachedRoot = null;
        }

        public static string Resolve(params string[] relativeSegments)
        {
            if (relativeSegments == null || relativeSegments.Length == 0)
                return Root;
            string[] safeSegments = relativeSegments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();
            foreach (string segment in safeSegments)
                ValidateRelativeSegment(segment);
            if (safeSegments.Length == 0) return Root;

            string patchPath = TryResolvePatchedFile(safeSegments);
            if (!string.IsNullOrWhiteSpace(patchPath))
                return patchPath;

            // Native ocgcore receives Root and therefore needs an actual
            // merged directory for data and Lua changes. Art and UI are read
            // through Resolve, so they can remain tiny independent patches.
            if (IsCorePath(safeSegments))
                return Combine(Root, safeSegments);

            string path = Combine(ResolveBaseContentRoot(), safeSegments);
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureAndroidAssetAvailable(path, safeSegments);
#endif
            return path;
        }

        private static void ValidateRelativeSegment(string segment)
        {
            if (Path.IsPathRooted(segment) ||
                segment.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                segment.IndexOfAny(new[] { '/', '\\' }) >= 0)
            {
                throw new ArgumentException(
                    "YGO content paths must contain only safe relative segments.",
                    nameof(segment));
            }
        }

        private static void ValidateEssentialContent(string root)
        {
            string[] required =
            {
                Path.Combine(root, "Data", "cards.bin"),
                Path.Combine(root, "Data", "card-texts.json"),
                Path.Combine(root, "Scripts", "constant.lua"),
                Path.Combine(root, "Scripts", "utility.lua"),
                Path.Combine(root, "Visual", "card-visuals.json")
            };
            foreach (string path in required)
            {
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        "Required packaged duel content is missing.",
                        path);
                }
            }
        }

        [Serializable]
        private sealed class RemoteContentPointer
        {
            public int schemaVersion;
            public string contentVersion;
            public string releaseDirectory;
        }

        [Serializable]
        private sealed class RemotePatchPointer
        {
            public int schemaVersion;
            public string contentVersion;
            public string[] patchDirectories;
        }

        [Serializable]
        private sealed class RemotePatchManifest
        {
            public int schemaVersion;
            public string[] files;
            public string[] deletedFiles;
        }

        private static string ResolveBundledRoot()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return EnsureAndroidMirror();
#else
            return Path.Combine(Application.streamingAssetsPath, ContentFolder);
#endif
        }

        private static string ResolveBaseContentRoot()
        {
            return ResolveBaseContentRoot(ResolveBundledRoot());
        }

        private static string ResolveBaseContentRoot(string bundledRoot)
        {
            string snapshot = TryResolveRemoteContentRoot();
            return string.IsNullOrWhiteSpace(snapshot) ? bundledRoot : snapshot;
        }

        private static string TryResolvePatchedCoreRoot(string bundledRoot)
        {
            if (!TryReadPatchPointer(out RemotePatchPointer pointer,
                                     out string container,
                                     out string patches))
            {
                return string.Empty;
            }

            string[] directories = (pointer.patchDirectories ?? Array.Empty<string>())
                .Where(IsSafePatchDirectory)
                .Where(directory => Directory.Exists(Path.Combine(
                    patches,
                    directory)))
                .ToArray();
            if (directories.Length == 0)
                return string.Empty;

            string baseRoot = ResolveBaseContentRoot(bundledRoot);
            string coreRoot = Path.Combine(container, "runtime-core");
            string marker = Path.Combine(coreRoot, PatchedCoreMarker);
            string identity = (Application.buildGUID ?? Application.version) +
                              ":" + (pointer.contentVersion ?? string.Empty) +
                              ":" + string.Join("|", directories);
            try
            {
                if (File.Exists(marker) &&
                    string.Equals(File.ReadAllText(marker), identity,
                        StringComparison.Ordinal) &&
                    TryValidateEssentialContent(coreRoot))
                {
                    return coreRoot;
                }

                string staging = coreRoot + ".staging";
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
                Directory.CreateDirectory(staging);
                try
                {
                    foreach (string directory in AndroidEagerDirectories)
                    {
                        string source = Path.Combine(baseRoot, directory);
                        if (Directory.Exists(source))
                            CopyDirectory(source, Path.Combine(staging, directory));
                    }
                    ApplyPatchesToCore(staging, baseRoot, patches, directories);
                    ValidateEssentialContent(staging);
                    File.WriteAllText(Path.Combine(staging, PatchedCoreMarker),
                        identity);
                    if (Directory.Exists(coreRoot))
                        Directory.Delete(coreRoot, true);
                    Directory.Move(staging, coreRoot);
                    return coreRoot;
                }
                catch
                {
                    if (Directory.Exists(staging))
                        Directory.Delete(staging, true);
                    throw;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "ARCANE_REMOTE_PATCH_FALLBACK reason=" +
                    exception.GetBaseException().Message);
                return string.Empty;
            }
        }

        private static void ApplyPatchesToCore(
            string coreRoot,
            string baseRoot,
            string patchesRoot,
            IEnumerable<string> directories)
        {
            foreach (string directory in directories)
            {
                string patchRoot = Path.Combine(patchesRoot, directory);
                RemotePatchManifest manifest = ReadPatchManifest(patchRoot);
                foreach (string relative in manifest.files ?? Array.Empty<string>())
                {
                    string[] segments = ParsePatchPath(relative);
                    if (!IsCorePath(segments)) continue;
                    string source = Combine(patchRoot, segments);
                    string destination = Combine(coreRoot, segments);
                    EnsureWithin(coreRoot, destination);
                    if (!File.Exists(source))
                    {
                        throw new FileNotFoundException(
                            "O patch ativo não contém o arquivo declarado.",
                            source);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination) ??
                                              coreRoot);
                    File.Copy(source, destination, true);
                }
                foreach (string relative in manifest.deletedFiles ??
                         Array.Empty<string>())
                {
                    string[] segments = ParsePatchPath(relative);
                    if (!IsCorePath(segments)) continue;
                    string source = Combine(baseRoot, segments);
                    string destination = Combine(coreRoot, segments);
                    EnsureWithin(coreRoot, destination);
                    if (File.Exists(source))
                    {
                        Directory.CreateDirectory(
                            Path.GetDirectoryName(destination) ?? coreRoot);
                        File.Copy(source, destination, true);
                    }
                    else if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }
                }
            }
        }

        private static string TryResolvePatchedFile(string[] safeSegments)
        {
            if (!TryReadPatchPointer(out RemotePatchPointer pointer,
                                     out _,
                                     out string patches))
            {
                return string.Empty;
            }

            string relative = string.Join("/", safeSegments);
            string[] directories = pointer.patchDirectories ?? Array.Empty<string>();
            for (int index = directories.Length - 1; index >= 0; index--)
            {
                string directory = directories[index];
                if (!IsSafePatchDirectory(directory)) continue;
                string root = Path.Combine(patches, directory);
                if (!Directory.Exists(root)) continue;
                RemotePatchManifest manifest;
                try
                {
                    manifest = ReadPatchManifest(root);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "ARCANE_REMOTE_PATCH_IGNORED reason=" +
                        exception.GetBaseException().Message);
                    continue;
                }

                if ((manifest.deletedFiles ?? Array.Empty<string>())
                    .Any(file => string.Equals(NormalizePatchPath(file), relative,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    return string.Empty;
                }
                if ((manifest.files ?? Array.Empty<string>())
                    .Any(file => string.Equals(NormalizePatchPath(file), relative,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    string candidate = Combine(root, safeSegments);
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            return string.Empty;
        }

        private static bool TryReadPatchPointer(
            out RemotePatchPointer pointer,
            out string container,
            out string patches)
        {
            pointer = null;
            container = Path.Combine(
                Application.persistentDataPath,
                "ArcaneArena",
                "RemoteContent",
                ContentFolder);
            patches = Path.Combine(container, "patches");
            try
            {
                string path = Path.Combine(container, PatchPointerFile);
                if (!File.Exists(path)) return false;
                pointer = JsonUtility.FromJson<RemotePatchPointer>(
                    File.ReadAllText(path));
                return pointer != null && pointer.schemaVersion == 1;
            }
            catch
            {
                pointer = null;
                return false;
            }
        }

        private static RemotePatchManifest ReadPatchManifest(string root)
        {
            string path = Path.Combine(root, PatchManifestFile);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    "O patch não possui seu manifesto.",
                    path);
            RemotePatchManifest manifest = JsonUtility.FromJson<
                RemotePatchManifest>(File.ReadAllText(path));
            if (manifest == null || manifest.schemaVersion != 1)
                throw new InvalidDataException("O patch possui formato incompatível.");
            foreach (string relative in (manifest.files ?? Array.Empty<string>())
                         .Concat(manifest.deletedFiles ?? Array.Empty<string>()))
            {
                ParsePatchPath(relative);
            }
            return manifest;
        }

        private static bool TryValidateEssentialContent(string root)
        {
            try
            {
                ValidateEssentialContent(root);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSafePatchDirectory(string value)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf("..", StringComparison.Ordinal) < 0 &&
                   value.IndexOfAny(new[] { '/', '\\' }) < 0;
        }

        private static string NormalizePatchPath(string value)
        {
            return string.Join("/", ParsePatchPath(value));
        }

        private static string[] ParsePatchPath(string value)
        {
            string clean = (value ?? string.Empty).Replace('\\', '/').Trim('/');
            string[] segments = clean.Split('/');
            if (clean.Length == 0 || Path.IsPathRooted(clean) ||
                clean.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                segments.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException("O patch contém um caminho inválido.");
            }
            foreach (string segment in segments)
                ValidateRelativeSegment(segment);
            return segments;
        }

        private static bool IsCorePath(IReadOnlyList<string> segments)
        {
            if (segments == null || segments.Count == 0) return false;
            return AndroidEagerDirectories.Any(directory => string.Equals(
                directory,
                segments[0],
                StringComparison.OrdinalIgnoreCase));
        }

        private static string Combine(string root, IEnumerable<string> segments)
        {
            string result = root;
            foreach (string segment in segments)
                result = Path.Combine(result, segment);
            return result;
        }

        private static void EnsureWithin(string root, string candidate)
        {
            string prefix = Path.GetFullPath(root).TrimEnd(
                                Path.DirectorySeparatorChar,
                                Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(candidate).StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("O patch tentou gravar fora do conteúdo.");
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            foreach (string file in Directory.GetFiles(source, "*",
                         SearchOption.AllDirectories))
            {
                string relative = file.Substring(source.Length).TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ??
                                          destination);
                File.Copy(file, target, true);
            }
        }

        private static string TryResolveRemoteContentRoot()
        {
            try
            {
                string container = Path.Combine(
                    Application.persistentDataPath,
                    "ArcaneArena",
                    "RemoteContent",
                    ContentFolder);
                string pointerPath = Path.Combine(container, "active.json");
                if (!File.Exists(pointerPath))
                    return string.Empty;
                RemoteContentPointer pointer = JsonUtility.FromJson<
                    RemoteContentPointer>(File.ReadAllText(pointerPath));
                string directory = pointer?.releaseDirectory?.Trim() ??
                                   string.Empty;
                if (directory.Length == 0 ||
                    directory.IndexOf("..", StringComparison.Ordinal) >= 0 ||
                    directory.IndexOfAny(new[] { '/', '\\' }) >= 0)
                {
                    return string.Empty;
                }
                string root = Path.GetFullPath(Path.Combine(
                    container,
                    "releases",
                    directory));
                string releases = Path.GetFullPath(Path.Combine(
                    container,
                    "releases")) + Path.DirectorySeparatorChar;
                if (!root.StartsWith(releases, StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                ValidateEssentialContent(root);
                return root;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "ARCANE_REMOTE_CONTENT_FALLBACK reason=" +
                    exception.GetBaseException().Message);
                return string.Empty;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string EnsureAndroidMirror()
        {
            string container = Path.Combine(
                Application.persistentDataPath,
                "ArcaneDuel",
                "Content");
            string destination = Path.Combine(container, ContentFolder);
            string marker = Path.Combine(destination, MarkerFile);
            string buildIdentity = string.IsNullOrWhiteSpace(Application.buildGUID)
                ? Application.version
                : Application.buildGUID;
            buildIdentity = AndroidMirrorSchema + ":" + buildIdentity;

            if (File.Exists(marker) &&
                string.Equals(
                    File.ReadAllText(marker),
                    buildIdentity,
                    StringComparison.Ordinal) &&
                TryValidateAndroidMirror(destination))
            {
                return destination;
            }

            Directory.CreateDirectory(container);
            string staging = Path.Combine(
                container,
                ContentFolder + ".staging");
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
            Directory.CreateDirectory(staging);

            try
            {
                using (var unityPlayer = new AndroidJavaClass(
                           "com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity =
                       unityPlayer.GetStatic<AndroidJavaObject>(
                           "currentActivity"))
                using (var mirror = new AndroidJavaClass(AndroidMirrorClass))
                {
                    foreach (string directory in AndroidEagerDirectories)
                    {
                        string assetDirectory = ContentFolder + "/" + directory;
                        string destinationDirectory = Path.Combine(staging, directory);
                        Directory.CreateDirectory(destinationDirectory);
                        mirror.CallStatic<long>(
                            "copyDirectory",
                            activity,
                            assetDirectory,
                            destinationDirectory);
                    }
                }

                File.WriteAllText(
                    Path.Combine(staging, MarkerFile),
                    buildIdentity);
                ValidateEssentialContent(staging);
                ValidateCardDatabaseHeader(staging);

                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);
                Directory.Move(staging, destination);
                Debug.Log(
                    $"ARCANE_ANDROID_CONTENT_READY root={destination} " +
                    $"build={buildIdentity}");
                return destination;
            }
            catch (Exception exception)
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, true);
                throw new InvalidOperationException(
                    "Android could not materialize the packaged duel content.",
                    exception);
            }
        }

        private static void EnsureAndroidAssetAvailable(
            string resolvedPath,
            string[] relativeSegments)
        {
            if (relativeSegments == null || relativeSegments.Length == 0 ||
                File.Exists(resolvedPath) || Directory.Exists(resolvedPath))
            {
                return;
            }

            string[] safeSegments = relativeSegments
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();
            if (safeSegments.Length == 0) return;
            foreach (string segment in safeSegments)
                ValidateRelativeSegment(segment);

            string assetPath = ContentFolder + "/" +
                               string.Join("/", safeSegments);
            lock (Sync)
            {
                if (File.Exists(resolvedPath)) return;
                try
                {
                    using (var unityPlayer = new AndroidJavaClass(
                               "com.unity3d.player.UnityPlayer"))
                    using (AndroidJavaObject activity =
                           unityPlayer.GetStatic<AndroidJavaObject>(
                               "currentActivity"))
                    using (var mirror = new AndroidJavaClass(AndroidMirrorClass))
                    {
                        mirror.CallStatic<long>(
                            "copyFile",
                            activity,
                            assetPath,
                            resolvedPath);
                    }
                }
                catch (Exception exception)
                {
                    // Art and UI files are optional presentation content. The
                    // caller can keep its existing File.Exists fallback. Core
                    // data and scripts are copied eagerly and validated above.
                    Debug.LogWarning(
                        $"ARCANE_ANDROID_OPTIONAL_CONTENT_MISSING " +
                        $"asset={assetPath} reason={exception.GetBaseException().Message}");
                }
            }
        }

        private static bool TryValidateAndroidMirror(string root)
        {
            try
            {
                ValidateEssentialContent(root);
                ValidateCardDatabaseHeader(root);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "ARCANE_ANDROID_CONTENT_REBUILD reason=" +
                    exception.GetBaseException().Message);
                return false;
            }
        }

        private static void ValidateCardDatabaseHeader(string root)
        {
            string cardsPath = Path.Combine(root, "Data", "cards.bin");
            using (var stream = new FileStream(
                       cardsPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                if (stream.Length < 12 ||
                    stream.ReadByte() != 'A' ||
                    stream.ReadByte() != 'D' ||
                    stream.ReadByte() != 'C' ||
                    stream.ReadByte() != 'B')
                {
                    throw new InvalidDataException(
                        "The mirrored Android card database is incomplete or corrupted.");
                }
            }
        }
#endif
    }
}
