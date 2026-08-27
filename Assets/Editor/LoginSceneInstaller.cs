#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Editor
{
    public static class LoginSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/Login.unity";
        private const string LogoPath =
            "Assets/Templates/Login/LoginLogo.png";
        private const string ThemePath =
            "Assets/Templates/Login/LoginTheme.mp3";
        private const string TitleCallPath =
            "Assets/Templates/Login/TitleCall.mp3";
        private const string ShinePath =
            "Assets/Templates/Login/ShineSound.mp3";

        [MenuItem("Card Game/Frontend/Criar ou atualizar tela de Login")]
        public static void CreateOrUpdateLoginScene()
        {
            ConfigureLogoImporter();
            ConfigureAudioImporter(ThemePath, false);
            ConfigureAudioImporter(TitleCallPath, true);
            ConfigureAudioImporter(ShinePath, true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Sprite logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            AudioClip theme = AssetDatabase.LoadAssetAtPath<AudioClip>(ThemePath);
            AudioClip titleCall =
                AssetDatabase.LoadAssetAtPath<AudioClip>(TitleCallPath);
            AudioClip shine = AssetDatabase.LoadAssetAtPath<AudioClip>(ShinePath);
            if (logoSprite == null || theme == null ||
                titleCall == null || shine == null)
            {
                throw new InvalidOperationException(
                    "Nao foi possivel importar todos os assets da tela de Login.");
            }

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "Login";

            CreateCameraAndAudioListener();
            Canvas canvas = CreateCanvas();
            RectTransform canvasRect = (RectTransform)canvas.transform;
            CreateImage(
                "Fundo Preto",
                canvasRect,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero,
                null,
                Color.black,
                false);

            RectTransform content = CreateRect(
                "Conteudo Central",
                canvasRect,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            Image logoImage = CreateImage(
                "Logo",
                content,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 38f),
                new Vector2(1050f, 700f),
                logoSprite,
                Color.white,
                true);
            CanvasGroup logoGroup =
                logoImage.gameObject.AddComponent<CanvasGroup>();

            Button loginButton = CreateLoginButton(content);
            CanvasGroup buttonGroup =
                loginButton.gameObject.AddComponent<CanvasGroup>();

            var controllerObject = new GameObject(
                "Controlador da Tela de Login");
            var controller =
                controllerObject.AddComponent<LoginIntroController>();
            AudioSource themeSource = CreateAudioSource(
                controllerObject,
                "Musica - LoginTheme");
            AudioSource titleSource = CreateAudioSource(
                controllerObject,
                "Voz - Titulo");
            AudioSource shineSource = CreateAudioSource(
                controllerObject,
                "Efeito - Shine");
            controller.Configure(
                logoImage.rectTransform,
                logoGroup,
                loginButton,
                buttonGroup,
                themeSource,
                titleSource,
                shineSource,
                theme,
                titleCall,
                shine);

            CreateEventSystem();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException(
                    "A cena Login nao pôde ser salva.");

            AssetDatabase.ImportAsset(
                ScenePath,
                ImportAssetOptions.ForceSynchronousImport);
            PutLoginFirstInBuildSettings();
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"ARCANE_LOGIN_SCENE_OK: theme={theme.length:F2}s, " +
                $"title={titleCall.length:F2}s, shine={shine.length:F2}s.");
        }

        private static void ConfigureLogoImporter()
        {
            if (AssetImporter.GetAtPath(LogoPath) is not TextureImporter importer)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ConfigureAudioImporter(
            string path,
            bool decompressOnLoad)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                return;
            AudioImporterSampleSettings settings =
                importer.defaultSampleSettings;
            settings.loadType = decompressOnLoad
                ? AudioClipLoadType.DecompressOnLoad
                : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.78f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        private static void CreateCameraAndAudioListener()
        {
            var cameraObject = new GameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            camera.orthographic = true;
        }

        private static Canvas CreateCanvas()
        {
            var canvasObject = new GameObject(
                "Login Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Button CreateLoginButton(Transform parent)
        {
            var buttonObject = new GameObject(
                "Botao Login",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect =
                buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -262f);
            rect.sizeDelta = new Vector2(300f, 76f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.025f, 0.06f, 0.09f, 0.96f);
            Outline outline = buttonObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.86f, 1f, 0.92f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.72f, 0.96f, 1f, 1f);
            colors.pressedColor = new Color(0.38f, 0.78f, 0.9f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.65f);
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            var textObject = new GameObject(
                "Texto",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(Shadow));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect =
                textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.text = "LOGIN";
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.fontSize = 29;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            Shadow shadow = textObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return button;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Sprite sprite,
            Color color,
            bool preserveAspect)
        {
            var item = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            Image image = item.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = preserveAspect;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            return rect;
        }

        private static AudioSource CreateAudioSource(
            GameObject owner,
            string sourceName)
        {
            AudioSource source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = null;
            // Component names are not independently editable, so the label is
            // retained in the clip/source fields of the controller hierarchy.
            _ = sourceName;
            return source;
        }

        private static void CreateEventSystem()
        {
            var eventObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventObject
                .GetComponent<InputSystemUIInputModule>()
                .AssignDefaultActions();
        }

        private static void PutLoginFirstInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new(ScenePath, true)
            };
            scenes.AddRange(
                EditorBuildSettings.scenes.Where(
                    scene => !string.Equals(
                        scene.path,
                        ScenePath,
                        StringComparison.OrdinalIgnoreCase)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
