using System;
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

#if UNITY_ANDROID && !UNITY_EDITOR
                    cachedRoot = EnsureAndroidMirror();
#else
                    cachedRoot = Path.Combine(
                        Application.streamingAssetsPath,
                        ContentFolder);
#endif
                    ValidateEssentialContent(cachedRoot);
                    return cachedRoot;
                }
            }
        }

        public static string Resolve(params string[] relativeSegments)
        {
            string path = Root;
            if (relativeSegments == null) return path;
            foreach (string segment in relativeSegments)
            {
                if (string.IsNullOrWhiteSpace(segment)) continue;
                ValidateRelativeSegment(segment);
                path = Path.Combine(path, segment);
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            EnsureAndroidAssetAvailable(path, relativeSegments);
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
