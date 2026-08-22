#if UNITY_EDITOR
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEngine;

namespace ArcaneArena.Editor
{
    public static class MainMenuUiAssetSynchronizer
    {
        private const string Source = "Assets/Templates/UI/";
        private const string ResourceFolder = "Assets/Resources/Frontend";
        private const string AssetPath =
            ResourceFolder + "/MainMenuUiAssets.asset";
        private const string HudOverlayShaderPath =
            ResourceFolder + "/MainMenuHudOverlay.shader";
        internal const string HudOverlayMaterialPath =
            ResourceFolder + "/MainMenuHudOverlayMaterial.mat";
        private const string ConfirmationSoundFile =
            "ConfirmationSound.mp3";

        [MenuItem("Arcane Arena/Sincronizar UI da Tela Inicial")]
        public static void Sync()
        {
            EnsureResourceFolder();
            ConfigureTexture(Source + "NewMainMenu.png", 4096);
            ConfigureTexture(Source + "NewDuelMenu.png", 4096);
            ConfigureTexture(Source + "HUD.jpg");
            ConfigureTexture(Source + "Botaoduelar.png");
            ConfigureTexture(Source + "botaodecks.png");
            ConfigureTexture(Source + "botaoloja.png");
            ConfigureTexture(Source + "botaomultiplayer.png");
            ConfigureTexture(Source + "botaoconfig.png");
            ConfigureAudio(Source + ConfirmationSoundFile);
            EnsureHudOverlayMaterial();

            var assets =
                AssetDatabase.LoadAssetAtPath<MainMenuUiAssets>(AssetPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<MainMenuUiAssets>();
                AssetDatabase.CreateAsset(assets, AssetPath);
            }

            assets.mainMenu = Load<Texture2D>("NewMainMenu.png");
            assets.duelHub = Load<Texture2D>("NewDuelMenu.png");
            assets.hud = Load<Texture2D>("HUD.jpg");
            assets.duelButton = Load<Texture2D>("Botaoduelar.png");
            assets.decksButton = Load<Texture2D>("botaodecks.png");
            assets.shopButton = Load<Texture2D>("botaoloja.png");
            assets.multiplayerButton =
                Load<Texture2D>("botaomultiplayer.png");
            assets.settingsButton = Load<Texture2D>("botaoconfig.png");
            assets.interfaceClick =
                Load<AudioClip>(ConfirmationSoundFile);

            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                assets.IsReady
                    ? "ARCANE_MAIN_MENU_UI_SYNC=READY"
                    : "ARCANE_MAIN_MENU_UI_SYNC=INCOMPLETE");
        }

        private static T Load<T>(string fileName)
            where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(Source + fileName);
        }

        internal static Material EnsureHudOverlayMaterial()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(
                HudOverlayShaderPath);
            if (shader == null)
            {
                Debug.LogError(
                    "Shader persistente da moldura do Main Menu nao foi " +
                    "encontrado: " + HudOverlayShaderPath);
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                HudOverlayMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "Main Menu HUD Overlay"
                };
                AssetDatabase.CreateAsset(
                    material,
                    HudOverlayMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void ConfigureTexture(string path, int maxSize = 2048)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;
            bool changed =
                importer.mipmapEnabled ||
                importer.maxTextureSize != maxSize ||
                importer.textureCompression !=
                TextureImporterCompression.Compressed ||
                (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) &&
                 !importer.alphaIsTransparency);
            if (!changed)
                return;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void ConfigureAudio(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                return;
            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            if (settings.preloadAudioData && !importer.loadInBackground)
                return;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        private static void EnsureResourceFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourceFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "Frontend");
        }
    }
}
#endif
