using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ArcaneArena.EditorTools
{
    /// <summary>
    /// Reproducible Android build entry point used by the Editor menu and CI.
    /// Gameplay remains platform agnostic; this class only validates and builds
    /// the Android presentation/runtime package.
    /// </summary>
    public static class AndroidBuildAutomation
    {
        private const string OutputDirectory = @"D:\APK";
        private const string ApplicationId = "com.arcaneduel.client";

        [MenuItem("Card Game/Build/Android APK (D:\\APK)")]
        public static void BuildFromMenu()
        {
            BuildApk(BuildOptions.None);
        }

        // Command-line entry point:
        // Unity.exe -batchmode -projectPath <project> -executeMethod
        // ArcaneArena.EditorTools.AndroidBuildAutomation.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            try
            {
                BuildApk(BuildOptions.CleanBuildCache);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void BuildApk(BuildOptions buildOptions)
        {
            ConfigureGradleUserHome();
            if (!BuildPipeline.IsBuildTargetSupported(
                    BuildTargetGroup.Android,
                    BuildTarget.Android))
            {
                throw new BuildFailedException(
                    "Android Build Support não está instalado para este Editor Unity.");
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new BuildFailedException("Nenhuma cena habilitada para build.");

            ValidateAndroidNativeRuntime();
            Directory.CreateDirectory(OutputDirectory);

            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Android,
                ApplicationId);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.SetScriptingBackend(
                NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            // Unity only exposes the 64-bit Android architecture when the
            // IL2CPP backend is active.  Set the backend first, otherwise the
            // Editor silently serializes AndroidTargetArchitectures as zero
            // and BuildPipeline later fails with "Target architecture not
            // specified".
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            AssetDatabase.SaveAssets();

            string safeVersion = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
                ? "dev"
                : PlayerSettings.bundleVersion.Replace(' ', '-');
            string apkPath = Path.Combine(
                OutputDirectory,
                $"ArcaneDuel-v{safeVersion}-arm64.apk");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = buildOptions
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Build Android falhou: {summary.result}; " +
                    $"erros={summary.totalErrors}; avisos={summary.totalWarnings}.");
            }

            Debug.Log(
                $"ANDROID_APK_READY path={apkPath}; bytes={summary.totalSize}; " +
                $"duration={summary.totalTime}; scenes={scenes.Length}; " +
                $"architecture=ARM64; backend=IL2CPP");
        }

        private static void ConfigureGradleUserHome()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 Application.dataPath;
            string gradleUserHome = Path.Combine(
                projectRoot,
                "Library",
                "GradleUserHome");
            Directory.CreateDirectory(gradleUserHome);
            Environment.SetEnvironmentVariable(
                "GRADLE_USER_HOME",
                gradleUserHome,
                EnvironmentVariableTarget.Process);
            Debug.Log("ANDROID_GRADLE_USER_HOME path=" + gradleUserHome);
        }

        private static void ValidateAndroidNativeRuntime()
        {
            string library = Path.Combine(
                Application.dataPath,
                "Plugins",
                "Android",
                "arm64-v8a",
                "libocgcore.so");
            if (!File.Exists(library) || new FileInfo(library).Length == 0)
            {
                throw new BuildFailedException(
                    "Runtime oficial ocgcore ARM64 ausente em " + library);
            }
        }
    }
}
