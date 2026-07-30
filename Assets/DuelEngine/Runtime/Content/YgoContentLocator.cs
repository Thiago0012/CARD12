using System;
using System.IO;
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
                path = Path.Combine(path, segment);
            }
            return path;
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

            if (File.Exists(marker) &&
                string.Equals(
                    File.ReadAllText(marker),
                    buildIdentity,
                    StringComparison.Ordinal))
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
                using (AndroidJavaObject assets =
                       activity.Call<AndroidJavaObject>("getAssets"))
                {
                    CopyAssetDirectory(assets, ContentFolder, staging);
                }

                File.WriteAllText(
                    Path.Combine(staging, MarkerFile),
                    buildIdentity);
                ValidateEssentialContent(staging);

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

        private static void CopyAssetDirectory(
            AndroidJavaObject assets,
            string assetDirectory,
            string destinationDirectory)
        {
            string[] children =
                assets.Call<string[]>("list", assetDirectory) ??
                Array.Empty<string>();
            foreach (string child in children)
            {
                string assetPath = assetDirectory + "/" + child;
                string destinationPath =
                    Path.Combine(destinationDirectory, child);
                string[] nested =
                    assets.Call<string[]>("list", assetPath) ??
                    Array.Empty<string>();
                if (nested.Length > 0)
                {
                    Directory.CreateDirectory(destinationPath);
                    CopyAssetDirectory(
                        assets,
                        assetPath,
                        destinationPath);
                    continue;
                }

                CopyAssetFile(assets, assetPath, destinationPath);
            }
        }

        private static void CopyAssetFile(
            AndroidJavaObject assets,
            string assetPath,
            string destinationPath)
        {
            string directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (AndroidJavaObject input =
                   assets.Call<AndroidJavaObject>("open", assetPath))
            using (var output = new FileStream(
                       destinationPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            {
                var buffer = new byte[64 * 1024];
                while (true)
                {
                    int read = input.Call<int>("read", buffer);
                    if (read < 0) break;
                    if (read == 0)
                    {
                        int single = input.Call<int>("read");
                        if (single < 0) break;
                        output.WriteByte((byte)single);
                        continue;
                    }
                    output.Write(buffer, 0, read);
                }
                input.Call("close");
            }
        }
#endif
    }
}
