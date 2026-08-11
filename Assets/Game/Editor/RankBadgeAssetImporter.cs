using UnityEditor;

namespace ArcaneDuel.Game.Editor
{
    internal sealed class RankBadgeAssetImporter : AssetPostprocessor
    {
        private const string BadgeFolder =
            "Assets/Resources/Frontend/Ranked/Badges/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(
                    BadgeFolder,
                    System.StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Compressed;
        }
    }
}
