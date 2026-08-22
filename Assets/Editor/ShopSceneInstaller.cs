#if UNITY_EDITOR
using System;
using ArcaneArena.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Editor
{
    public static class ShopSceneInstaller
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string RootName = "LOJA EDITAVEL";

        private static readonly Color DeepNavy =
            new(0.008f, 0.025f, 0.05f, 0.98f);
        private static readonly Color Panel =
            new(0.015f, 0.045f, 0.075f, 0.96f);
        private static readonly Color Cyan =
            new(0.204f, 0.867f, 0.957f, 1f);
        private static readonly Color Gold =
            new(0.949f, 0.78f, 0.4f, 1f);
        private static readonly Color Muted =
            new(0.57f, 0.65f, 0.73f, 1f);

        [MenuItem("Card Game/Loja/Instalar Shop View editavel na Scene")]
        public static void Install()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning(
                    "Pare o Play antes de instalar a Shop View permanente.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single);
            MainMenuSceneView mainMenu =
                UnityEngine.Object.FindAnyObjectByType<MainMenuSceneView>(
                    FindObjectsInactive.Include);
            if (mainMenu == null || !mainMenu.IsConfigured)
            {
                throw new InvalidOperationException(
                    "A MainMenu editavel precisa estar configurada antes " +
                    "da instalacao da loja.");
            }

            ShopSceneView view = EnsureForScene(scene, mainMenu);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = view.Root.gameObject;
            EditorGUIUtility.PingObject(view.Root.gameObject);
            Debug.Log(
                "ARCANE_SHOP_SCENE_EDITABLE=READY; edite LOJA EDITAVEL " +
                "diretamente na Hierarchy.");
        }

        internal static ShopSceneView EnsureForScene(
            Scene scene,
            MainMenuSceneView mainMenu)
        {
            RectTransform dynamicRoot = mainMenu.DynamicRoot;
            if (dynamicRoot == null || dynamicRoot.parent == null)
            {
                throw new InvalidOperationException(
                    "A raiz de conteudo da MainMenu nao foi encontrada.");
            }

            Transform parent = dynamicRoot.parent;
            Transform existing = parent.Find(RootName);
            if (existing != null)
            {
                ShopSceneView existingView =
                    existing.GetComponent<ShopSceneView>();
                if (existingView == null || !existingView.IsConfigured)
                {
                    throw new InvalidOperationException(
                        "Existe uma LOJA EDITAVEL incompleta. Preserve os " +
                        "ajustes manuais e preencha as referencias do " +
                        "ShopSceneView no Inspector.");
                }
                return existingView;
            }

            RectTransform root = CreateRect(parent, RootName,
                Vector2.zero, Vector2.one);
            root.SetSiblingIndex(dynamicRoot.GetSiblingIndex() + 1);

            BuildBackground(root);
            BuildHeader(root, out Button backButton);
            BuildCurrency(root, out Text balanceText);
            Text feedbackText = CreateText(
                root,
                "Feedback Text",
                "Moedas sao obtidas exclusivamente em duelos online PvP " +
                "concluidos.",
                15,
                FontStyle.Bold,
                Muted,
                new Vector2(0.08f, 0.805f),
                new Vector2(0.72f, 0.852f),
                TextAnchor.MiddleLeft);
            BuildTabs(
                root,
                out Button packagesButton,
                out Button structureDecksButton,
                out Button profileIconsButton);
            BuildCatalog(
                root,
                out ScrollRect catalogScroll,
                out RectTransform catalogContent,
                out GridLayoutGroup catalogGrid);

            ShopSceneView view = root.gameObject.AddComponent<ShopSceneView>();
            view.Configure(
                root,
                backButton,
                packagesButton,
                structureDecksButton,
                profileIconsButton,
                balanceText,
                feedbackText,
                catalogScroll,
                catalogContent,
                catalogGrid);
            root.gameObject.SetActive(false);
            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(scene);
            return view;
        }

        private static void BuildBackground(RectTransform root)
        {
            Image background = CreateImage(
                root,
                "Background",
                Vector2.zero,
                Vector2.one,
                Color.white);
            background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/Shop/ShopBackgroundGold-v2.png");
            background.raycastTarget = false;

            Image veil = CreateImage(
                root,
                "Background Contrast",
                Vector2.zero,
                Vector2.one,
                new Color(0.005f, 0.015f, 0.035f, 0.42f));
            veil.raycastTarget = false;
        }

        private static void BuildHeader(
            RectTransform root,
            out Button backButton)
        {
            RectTransform header = CreateRect(
                root,
                "Header",
                new Vector2(0.025f, 0.91f),
                new Vector2(0.735f, 0.98f));
            backButton = CreateButton(
                header,
                "Back Button",
                "VOLTAR",
                new Vector2(0f, 0.08f),
                new Vector2(0.13f, 0.92f),
                Cyan);
            CreateText(
                header,
                "Title",
                "LOJA ARCANE",
                31,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.16f, 0f),
                Vector2.one,
                TextAnchor.MiddleLeft);
        }

        private static void BuildCurrency(
            RectTransform root,
            out Text balanceText)
        {
            Image panel = CreateImage(
                root,
                "Currency Panel",
                new Vector2(0.76f, 0.895f),
                new Vector2(0.955f, 0.975f),
                Panel);
            AddOutline(panel.gameObject, Gold, new Vector2(2f, -2f));
            Image icon = CreateImage(
                panel.rectTransform,
                "Currency Icon",
                new Vector2(0.065f, 0.14f),
                new Vector2(0.26f, 0.86f),
                Color.white);
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Resources/Shop/CurrencyCrystal.png");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            balanceText = CreateText(
                panel.rectTransform,
                "Balance Text",
                "0",
                28,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.275f, 0.08f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleLeft);
        }

        private static void BuildTabs(
            RectTransform root,
            out Button packages,
            out Button structureDecks,
            out Button profileIcons)
        {
            RectTransform tabs = CreateRect(
                root,
                "Category Tabs",
                new Vector2(0.08f, 0.855f),
                new Vector2(0.72f, 0.905f));
            packages = CreateButton(
                tabs,
                "Packages Tab",
                "PACOTES",
                Vector2.zero,
                new Vector2(0.325f, 1f),
                Cyan);
            structureDecks = CreateButton(
                tabs,
                "Structure Decks Tab",
                "DECKS ESTRUTURAIS",
                new Vector2(0.345f, 0f),
                new Vector2(0.735f, 1f),
                Cyan);
            profileIcons = CreateButton(
                tabs,
                "Profile Icons Tab",
                "ICONES",
                new Vector2(0.755f, 0f),
                Vector2.one,
                Cyan);
        }

        private static void BuildCatalog(
            RectTransform root,
            out ScrollRect scroll,
            out RectTransform content,
            out GridLayoutGroup grid)
        {
            Image frame = CreateImage(
                root,
                "Catalog Scroll View",
                new Vector2(0.055f, 0.055f),
                new Vector2(0.945f, 0.79f),
                new Color(0.006f, 0.02f, 0.04f, 0.76f));
            AddOutline(
                frame.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f),
                new Vector2(1f, -1f));
            scroll = frame.gameObject.AddComponent<ScrollRect>();

            Image viewportImage = CreateImage(
                frame.rectTransform,
                "Viewport",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.01f));
            viewportImage.rectTransform.offsetMin = new Vector2(10f, 10f);
            viewportImage.rectTransform.offsetMax = new Vector2(-42f, -10f);
            viewportImage.gameObject.AddComponent<RectMask2D>();

            content = CreateRect(
                viewportImage.rectTransform,
                "Content - Dynamic Products",
                new Vector2(0f, 1f),
                Vector2.one);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(500f, 210f);
            grid.spacing = new Vector2(24f, 20f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image track = CreateImage(
                frame.rectTransform,
                "Scrollbar",
                new Vector2(0.972f, 0.02f),
                new Vector2(0.993f, 0.98f),
                new Color(0.04f, 0.09f, 0.13f, 0.96f));
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            RectTransform slidingArea = CreateRect(
                track.rectTransform,
                "Sliding Area",
                Vector2.zero,
                Vector2.one);
            slidingArea.offsetMin = new Vector2(3f, 3f);
            slidingArea.offsetMax = new Vector2(-3f, -3f);
            Image handle = CreateImage(
                slidingArea,
                "Handle",
                Vector2.zero,
                Vector2.one,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.92f));
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            scroll.content = content;
            scroll.viewport = viewportImage.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 32f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            RectTransform rect = CreateRect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 min,
            Vector2 max,
            TextAnchor alignment)
        {
            RectTransform rect = CreateRect(parent, name, min, max);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 min,
            Vector2 max,
            Color accent)
        {
            Image image = CreateImage(parent, name, min, max, DeepNavy);
            AddOutline(
                image.gameObject,
                new Color(accent.r, accent.g, accent.b, 0.86f),
                new Vector2(2f, -2f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.65f, 0.85f, 0.9f, 1f);
            button.colors = colors;
            CreateText(
                image.rectTransform,
                "Label",
                label,
                17,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            return button;
        }

        private static void AddOutline(
            GameObject target,
            Color color,
            Vector2 distance)
        {
            var outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
        }
    }
}
#endif
