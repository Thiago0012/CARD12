using UnityEditor;
using UnityEngine;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

namespace ArcaneDuel.Editor
{
    public static class NativePluginConfigurator
    {
        private const string SessionConfiguredKey =
            "ArcaneDuel.NativePlugins.Configured.v2";
        private const string WindowsPluginPath =
            "Assets/Plugins/Windows/x86_64/ocgcore.dll";
        private const string AndroidPluginPath =
            "Assets/Plugins/Android/arm64-v8a/libocgcore.so";

        [InitializeOnLoadMethod]
        private static void ConfigureOncePerEditorSession()
        {
            if (SessionState.GetBool(SessionConfiguredKey, false))
            {
                return;
            }

            SessionState.SetBool(SessionConfiguredKey, true);
            EditorApplication.delayCall += () =>
            {
                if (System.IO.File.Exists(WindowsPluginPath) ||
                    System.IO.File.Exists(AndroidPluginPath))
                {
                    Configure();
                }
            };
        }

        [MenuItem("Arcane Duel/Configure Native Plugin")]
        public static void Configure()
        {
            bool configured = false;
            if (System.IO.File.Exists(WindowsPluginPath))
            {
                ConfigureWindows();
                configured = true;
            }
            if (System.IO.File.Exists(AndroidPluginPath))
            {
                ConfigureAndroid();
                configured = true;
            }
            if (!configured)
            {
                throw new System.IO.FileNotFoundException(
                    "Build ygopro-core for Windows or Android before configuring its importer.");
            }

#if UNITY_EDITOR_WIN
            NativeSmokeTest.GetVersion(out int major, out int minor);
            Debug.Log(
                $"ARCANE_DUEL_NATIVE_PLUGINS_OK ocgcore={major}.{minor}");
#else
            Debug.Log("ARCANE_DUEL_NATIVE_PLUGINS_OK");
#endif
        }

        private static void ConfigureWindows()
        {
            PluginImporter importer = Import(WindowsPluginPath);
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(true);
            importer.SetEditorData("OS", "Windows");
            importer.SetEditorData("CPU", "x86_64");
            importer.SetCompatibleWithPlatform(
                BuildTarget.StandaloneWindows64,
                true);
            importer.SetCompatibleWithPlatform(
                BuildTarget.StandaloneWindows,
                false);
            importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
            importer.SetPlatformData(
                BuildTarget.StandaloneWindows64,
                "CPU",
                "x86_64");
            importer.SaveAndReimport();
        }

        private static void ConfigureAndroid()
        {
            PluginImporter importer = Import(AndroidPluginPath);
            importer.SetCompatibleWithAnyPlatform(false);
            importer.SetCompatibleWithEditor(false);
            importer.SetCompatibleWithPlatform(
                BuildTarget.StandaloneWindows64,
                false);
            importer.SetCompatibleWithPlatform(BuildTarget.Android, true);
            importer.SetPlatformData(BuildTarget.Android, "CPU", "ARM64");
            importer.SaveAndReimport();
        }

        private static PluginImporter Import(string path)
        {
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as PluginImporter;
            if (importer == null)
            {
                throw new UnityException(
                    $"Unity did not create a PluginImporter for {path}.");
            }
            return importer;
        }

#if UNITY_EDITOR_WIN
        private static class NativeSmokeTest
        {
            [DllImport(
                "ocgcore",
                CallingConvention = CallingConvention.Cdecl,
                ExactSpelling = true,
                EntryPoint = "OCG_GetVersion")]
            internal static extern void GetVersion(
                out int major,
                out int minor);
        }
#endif
    }
}
