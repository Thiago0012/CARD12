using System;
using System.IO;
using System.Linq;
using System.Text;
using ArcaneDuel.Game;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArcaneDuel.Editor
{
    public static class ArcaneBuildAutomation
    {
        [MenuItem("Arcane Duel/Build Windows/Development")]
        public static void BuildWindowsDevelopment()
        {
            Build(
                "Windows-Development",
                BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        [MenuItem("Arcane Duel/Build Windows/Release")]
        public static void BuildWindowsRelease()
        {
            Build("Windows", BuildOptions.None);
        }

        [MenuItem("Arcane Duel/Build Windows/Development and Release")]
        public static void BuildAllWindows()
        {
            BuildWindowsDevelopment();
            BuildWindowsRelease();
            Debug.Log("ARCANE_DUEL_ALL_WINDOWS_BUILDS_OK");
        }

        // Kept for compatibility with earlier automation calls.
        public static void BuildWindowsArena()
        {
            BuildWindowsRelease();
        }

        [MenuItem("Arcane Duel/Build Android/Release APK")]
        public static void BuildAndroidRelease()
        {
            BuildAndroid("Android", BuildOptions.None);
        }

        [MenuItem("Arcane Duel/Build Android/Development APK")]
        public static void BuildAndroidDevelopment()
        {
            BuildAndroid(
                "Android-Development",
                BuildOptions.Development | BuildOptions.AllowDebugging);
        }

        // Public entry point for Unity -batchmode -executeMethod.
        public static void BuildAndroidFromCommandLine()
        {
            BuildAndroidRelease();
        }

        public static void BuildAndroidDevelopmentFromCommandLine()
        {
            BuildAndroidDevelopment();
        }

        private static void Build(string folderName, BuildOptions options)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            WriteBuildDiagnostics(projectRoot, options);

            string outputDirectory = Path.Combine(
                projectRoot,
                "Builds",
                folderName);
            Directory.CreateDirectory(outputDirectory);
            string output = Path.Combine(outputDirectory, "ArcaneDuel.exe");
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = options
            };
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows build failed: {report.summary.result}, " +
                    $"errors={report.summary.totalErrors}.");
            }
            PackageDocumentation(projectRoot, outputDirectory);
            Debug.Log(
                $"ARCANE_DUEL_WINDOWS_BUILD_OK path={output} " +
                $"bytes={report.summary.totalSize} options={options}");
        }

        private static void BuildAndroid(
            string folderName,
            BuildOptions options)
        {
            const string plugin =
                "Assets/Plugins/Android/arm64-v8a/libocgcore.so";
            if (!File.Exists(plugin))
            {
                throw new FileNotFoundException(
                    "Build the Android ocgcore plugin first with " +
                    "Tools/Build/Build-OcgCoreAndroid.ps1.",
                    plugin);
            }

            NativePluginConfigurator.Configure();
            if (!Application.isBatchMode &&
                EditorUserBuildSettings.activeBuildTarget !=
                    BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android,
                    BuildTarget.Android))
            {
                throw new InvalidOperationException(
                    "Unity could not switch to the Android build target. " +
                    "In batch mode, launch Unity with " +
                    "\"-buildTarget Android\".");
            }
            if (Application.isBatchMode &&
                EditorUserBuildSettings.activeBuildTarget !=
                    BuildTarget.Android)
            {
                Debug.LogWarning(
                    "Batch mode reports a different active target; " +
                    "BuildPlayer will use the explicit Android target.");
            }

            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion =
                AndroidSdkVersions.AndroidApiLevel26;
            EditorUserBuildSettings.buildAppBundle = false;

            string projectRoot =
                Directory.GetParent(Application.dataPath).FullName;
            WriteBuildDiagnostics(
                projectRoot,
                options,
                "Android arm64-v8a");
            string outputDirectory = Path.Combine(
                projectRoot,
                "Builds",
                folderName);
            Directory.CreateDirectory(outputDirectory);
            string output = Path.Combine(
                outputDirectory,
                "ArcaneDuel.apk");
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = options
            };
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Android build failed: {report.summary.result}, " +
                    $"errors={report.summary.totalErrors}.");
            }
            Debug.Log(
                $"ARCANE_DUEL_ANDROID_BUILD_OK path={output} " +
                $"bytes={report.summary.totalSize} options={options}");
        }

        private static void WriteBuildDiagnostics(
            string projectRoot,
            BuildOptions options,
            string platform = "Windows x64")
        {
            string directory = Path.Combine(
                projectRoot,
                "Assets",
                "StreamingAssets",
                "Build");
            Directory.CreateDirectory(directory);
            string json =
                "{\n" +
                "  \"schemaVersion\": 1,\n" +
                $"  \"product\": \"{ProjectIdentity.ProductName}\",\n" +
                $"  \"projectVersion\": \"{ProjectIdentity.ProjectVersion}\",\n" +
                $"  \"unityVersion\": \"{Application.unityVersion}\",\n" +
                $"  \"projectCommit\": \"{ReadGitRevision(projectRoot)}\",\n" +
                $"  \"coreApi\": \"{ProjectIdentity.CoreApiVersion}\",\n" +
                $"  \"coreCommit\": \"{ProjectIdentity.CoreCommit}\",\n" +
                $"  \"cardScriptsCommit\": \"{ProjectIdentity.CardScriptsCommit}\",\n" +
                $"  \"babelCdbCommit\": \"{ProjectIdentity.BabelCdbCommit}\",\n" +
                $"  \"buildKind\": \"{(options == BuildOptions.None ? "Release" : "Development")}\",\n" +
                $"  \"platform\": \"{platform}\",\n" +
                "  \"catalogCards\": 200,\n" +
                "  \"coreCatalogCards\": 261,\n" +
                "  \"legacyPresentationCards\": 193\n" +
                "}\n";
            File.WriteAllText(
                Path.Combine(directory, "build-diagnostics.json"),
                json,
                new UTF8Encoding(false));
            AssetDatabase.Refresh();
        }

        private static string ReadGitRevision(string projectRoot)
        {
            try
            {
                string gitDirectory = Path.Combine(projectRoot, ".git");
                string head = File.ReadAllText(
                    Path.Combine(gitDirectory, "HEAD")).Trim();
                if (!head.StartsWith("ref: ", StringComparison.Ordinal))
                {
                    return head;
                }
                string reference = head.Substring(5);
                string loose = Path.Combine(
                    gitDirectory,
                    reference.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(loose)) return File.ReadAllText(loose).Trim();

                string packed = Path.Combine(gitDirectory, "packed-refs");
                if (!File.Exists(packed)) return "unknown";
                foreach (string line in File.ReadLines(packed))
                {
                    if (line.StartsWith("#", StringComparison.Ordinal) ||
                        line.StartsWith("^", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string[] parts = line.Split(' ');
                    if (parts.Length == 2 && parts[1] == reference)
                    {
                        return parts[0];
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not read project revision: {exception.Message}");
            }
            return "unknown";
        }

        private static void PackageDocumentation(
            string projectRoot,
            string outputDirectory)
        {
            string target = Path.Combine(outputDirectory, "Documentation");
            Directory.CreateDirectory(target);
            string[] rootFiles =
            {
                "README.md",
                "THIRD_PARTY_NOTICES.md",
                "MODIFICATIONS.md",
                "SOURCE_CODE.md",
                "ThirdPartyVersions.json",
                "ToolchainVersions.json"
            };
            foreach (string name in rootFiles)
            {
                CopyIfPresent(
                    Path.Combine(projectRoot, name),
                    Path.Combine(target, name));
            }
            CopyTree(
                Path.Combine(projectRoot, "LICENSES"),
                Path.Combine(target, "LICENSES"));
            CopyTree(
                Path.Combine(projectRoot, "Documentation", "Rules"),
                Path.Combine(target, "Rules"));
            CopyIfPresent(
                Path.Combine(
                    projectRoot,
                    "Documentation",
                    "Reports",
                    "CardImportReport.md"),
                Path.Combine(target, "CardImportReport.md"));
            CopyIfPresent(
                Path.Combine(
                    projectRoot,
                    "Assets",
                    "StreamingAssets",
                    "Build",
                    "build-diagnostics.json"),
                Path.Combine(target, "build-diagnostics.json"));
        }

        private static void CopyTree(string source, string destination)
        {
            if (!Directory.Exists(source)) return;
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
            {
                CopyIfPresent(
                    file,
                    Path.Combine(destination, Path.GetFileName(file)));
            }
            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyTree(
                    directory,
                    Path.Combine(destination, Path.GetFileName(directory)));
            }
        }

        private static void CopyIfPresent(string source, string destination)
        {
            if (!File.Exists(source)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }
    }
}
