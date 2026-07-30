using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Editor
{
    public static class NativePluginConfigurator
    {
        private const string PluginPath =
            "Assets/Plugins/Windows/x86_64/ocgcore.dll";

        [MenuItem("Arcane Duel/Configure Native Plugin")]
        public static void Configure()
        {
            if (!System.IO.File.Exists(PluginPath))
            {
                throw new System.IO.FileNotFoundException(
                    "Build the pinned ygopro-core before configuring its importer.",
                    PluginPath);
            }

            AssetDatabase.ImportAsset(
                PluginPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(PluginPath) as PluginImporter;
            if (importer == null)
            {
                throw new UnityException(
                    $"Unity did not create a PluginImporter for {PluginPath}.");
            }

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
            importer.SetPlatformData(
                BuildTarget.StandaloneWindows64,
                "CPU",
                "x86_64");
            importer.SaveAndReimport();

            Debug.Log("ARCANE_DUEL_NATIVE_PLUGIN_OK");
        }
    }
}
