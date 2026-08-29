#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor
{
    /// <summary>
    /// Mantém as artes do menu nítidas na área de exibição sem carregar as
    /// fontes de até 4000 px integralmente na memória da loja.
    /// </summary>
    public sealed class ProfileArtworkTextureImporter : AssetPostprocessor
    {
        private const string ArtworkRoot =
            "Assets/Resources/Profile/Artworks/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(
                    ArtworkRoot,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (assetImporter is not TextureImporter importer)
                return;

            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
        }
    }
}
#endif
