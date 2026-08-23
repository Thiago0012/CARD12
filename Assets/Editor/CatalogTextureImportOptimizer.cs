using System.Collections.Generic;
using ArcaneArena.Cards;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor
{
    public static class CatalogTextureImportOptimizer
    {
        private const string CatalogPath =
            "Assets/Cards/CardCatalog.asset";
        private const int CatalogPreviewSize = 512;

        [MenuItem("Arcane Arena/Performance/Optimize Card Previews")]
        public static void OptimizeCatalogTextures()
        {
            CardCatalog catalog =
                AssetDatabase.LoadAssetAtPath<CardCatalog>(CatalogPath);
            if (catalog == null)
                throw new MissingReferenceException(CatalogPath);

            var paths = new HashSet<string>();
            foreach (CardCatalogEntry entry in catalog.Entries)
            {
                if (entry?.AuthoredArtwork == null)
                    continue;
                string path = AssetDatabase.GetAssetPath(entry.AuthoredArtwork);
                if (!string.IsNullOrWhiteSpace(path))
                    paths.Add(path);
            }

            int changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    if (AssetImporter.GetAtPath(path) is not
                        TextureImporter importer)
                    {
                        continue;
                    }

                    bool dirty = ConfigureImporter(importer);
                    if (!dirty)
                        continue;

                    if (AssetDatabase.WriteImportSettingsIfDirty(path))
                        changed++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"ARCANE_CARD_PREVIEWS_OPTIMIZED assets={paths.Count}; " +
                $"changed={changed}; maxSize={CatalogPreviewSize}");
        }

        public static void OptimizeFromCommandLine()
        {
            OptimizeCatalogTextures();
        }

        public static bool ConfigureImporter(TextureImporter importer)
        {
            if (importer == null)
                return false;
            bool dirty = ConfigureBase(importer);
            dirty |= ConfigurePlatform(importer, "Android");
            dirty |= ConfigurePlatform(importer, "iPhone");
            return dirty;
        }

        private static bool ConfigureBase(TextureImporter importer)
        {
            bool changed = false;
            if (importer.maxTextureSize != CatalogPreviewSize)
            {
                importer.maxTextureSize = CatalogPreviewSize;
                changed = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }
            if (importer.isReadable)
            {
                importer.isReadable = false;
                changed = true;
            }
            if (importer.textureCompression !=
                TextureImporterCompression.Compressed)
            {
                importer.textureCompression =
                    TextureImporterCompression.Compressed;
                changed = true;
            }
            return changed;
        }

        private static bool ConfigurePlatform(
            TextureImporter importer,
            string platform)
        {
            TextureImporterPlatformSettings settings =
                importer.GetPlatformTextureSettings(platform);
            bool changed =
                !settings.overridden ||
                settings.maxTextureSize != CatalogPreviewSize ||
                settings.textureCompression !=
                    TextureImporterCompression.Compressed;
            if (!changed)
                return false;

            settings.overridden = true;
            settings.maxTextureSize = CatalogPreviewSize;
            settings.textureCompression =
                TextureImporterCompression.Compressed;
            settings.compressionQuality = 50;
            importer.SetPlatformTextureSettings(settings);
            return true;
        }
    }
}
