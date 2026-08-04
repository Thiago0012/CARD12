using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ArcaneArena.Editor.AutoPacks
{
    internal sealed class CardCatalogChangePostprocessor : AssetPostprocessor
    {
        private static bool scheduled;
        private static double notBefore;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (AutoPackGenerationCoordinator.IsRunning)
                return;
            IEnumerable<string> paths =
                (importedAssets ?? Array.Empty<string>())
                .Concat(deletedAssets ?? Array.Empty<string>())
                .Concat(movedAssets ?? Array.Empty<string>())
                .Concat(movedFromAssetPaths ?? Array.Empty<string>());
            if (!paths.Any(IsRelevantPath))
                return;
            ScheduleAgain();
        }

        internal static void ScheduleAgain()
        {
            notBefore = EditorApplication.timeSinceStartup + 0.75d;
            if (scheduled)
                return;
            scheduled = true;
            EditorApplication.update -= RunWhenStable;
            EditorApplication.update += RunWhenStable;
        }

        private static void RunWhenStable()
        {
            if (EditorApplication.timeSinceStartup < notBefore ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                BuildPipeline.isBuildingPlayer)
            {
                return;
            }
            EditorApplication.update -= RunWhenStable;
            scheduled = false;
            AutoPackGenerationCoordinator.RequestRebuild("AssetImport");
        }

        private static bool IsRelevantPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            AutoPackGenerationSettings settings =
                AssetDatabase.LoadAssetAtPath<AutoPackGenerationSettings>(
                    AutoPackPaths.Settings);
            IEnumerable<string> watched = settings != null
                ? settings.WatchedFolders
                : new[]
                {
                    AutoPackPaths.CardCatalog,
                    "Assets/Cards/Cards",
                    "Assets/StreamingAssets/Ygo/Data/cards.bin",
                    "Assets/StreamingAssets/Ygo/Data/card-texts.json"
                };
            return watched.Any(root =>
                !string.IsNullOrWhiteSpace(root) &&
                path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
        }
    }
}
