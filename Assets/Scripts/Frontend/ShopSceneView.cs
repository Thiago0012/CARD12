using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Casca visual editavel da loja. Elementos permanentes vivem na cena;
    /// somente os produtos do catalogo sao recriados em runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShopSceneView : MonoBehaviour
    {
        [Header("Raiz e navegacao")]
        [SerializeField] private RectTransform root;
        [SerializeField] private Button backButton;

        [Header("Categorias")]
        [SerializeField] private Button packagesButton;
        [SerializeField] private Button structureDecksButton;
        [SerializeField] private Button profileIconsButton;

        [Header("Dados dinamicos")]
        [SerializeField] private Text coinBalanceText;
        [SerializeField] private Text feedbackText;
        [SerializeField] private ScrollRect catalogScroll;
        [SerializeField] private RectTransform catalogContent;
        [SerializeField] private GridLayoutGroup catalogGrid;

        [Header("Layout de pacotes e decks")]
        [SerializeField] private Vector2 productCellSize = new Vector2(500f, 210f);
        [SerializeField] private Vector2 productSpacing = new Vector2(24f, 20f);
        [SerializeField, Min(1)] private int productColumns = 3;

        [Header("Layout de icones")]
        [SerializeField] private Vector2 iconCellSize = new Vector2(370f, 210f);
        [SerializeField] private Vector2 iconSpacing = new Vector2(18f, 20f);
        [SerializeField, Min(1)] private int iconColumns = 4;

        private UnityAction _backAction;
        private UnityAction _packagesAction;
        private UnityAction _structureDecksAction;
        private UnityAction _profileIconsAction;
        private bool _professionalThemeApplied;
        private Sprite _runtimeProfessionalBackground;

        private static readonly Color ShopAmber =
            new(0.98f, 0.68f, 0.18f, 1f);
        private static readonly Color ShopGold =
            new(0.98f, 0.82f, 0.42f, 1f);
        private static readonly Color ShopMuted =
            new(0.72f, 0.69f, 0.62f, 1f);

        public RectTransform Root => root;
        public RectTransform CatalogContent => catalogContent;
        public ScrollRect CatalogScroll => catalogScroll;
        public GridLayoutGroup CatalogGrid => catalogGrid;
        public bool IsConfigured =>
            root != null &&
            backButton != null &&
            packagesButton != null &&
            structureDecksButton != null &&
            profileIconsButton != null &&
            coinBalanceText != null &&
            feedbackText != null &&
            catalogScroll != null &&
            catalogContent != null &&
            catalogGrid != null;

        public void Configure(
            RectTransform viewRoot,
            Button back,
            Button packages,
            Button structureDecks,
            Button profileIcons,
            Text balance,
            Text feedback,
            ScrollRect scroll,
            RectTransform content,
            GridLayoutGroup grid)
        {
            root = viewRoot;
            backButton = back;
            packagesButton = packages;
            structureDecksButton = structureDecks;
            profileIconsButton = profileIcons;
            coinBalanceText = balance;
            feedbackText = feedback;
            catalogScroll = scroll;
            catalogContent = content;
            catalogGrid = grid;

            if (catalogGrid != null)
            {
                productCellSize = catalogGrid.cellSize;
                productSpacing = catalogGrid.spacing;
                productColumns = Mathf.Max(1, catalogGrid.constraintCount);
            }
        }

        public void Bind(
            UnityAction back,
            UnityAction packages,
            UnityAction structureDecks,
            UnityAction profileIcons)
        {
            Unbind();
            BindButton(backButton, ref _backAction, back);
            BindButton(packagesButton, ref _packagesAction, packages);
            BindButton(
                structureDecksButton,
                ref _structureDecksAction,
                structureDecks);
            BindButton(
                profileIconsButton,
                ref _profileIconsAction,
                profileIcons);
        }

        public void SetVisible(bool visible)
        {
            if (root != null)
                root.gameObject.SetActive(visible);
        }

        public void SetBalance(int balance)
        {
            if (coinBalanceText != null)
                coinBalanceText.text = Mathf.Max(0, balance).ToString("N0");
        }

        public void SetFeedback(string message, Color color)
        {
            if (feedbackText == null)
                return;
            feedbackText.text = message ?? string.Empty;
            feedbackText.color = color;
        }

        public void SetSelectedTab(int selectedTab, Color selected, Color idle)
        {
            SetShopTabState(packagesButton, selectedTab == 0);
            SetButtonAccent(
                structureDecksButton,
                selectedTab == 1 ? ShopAmber : new Color(ShopAmber.r,
                    ShopAmber.g, ShopAmber.b, 0.34f));
            SetShopTabState(structureDecksButton, selectedTab == 1);
            SetShopTabState(profileIconsButton, selectedTab == 2);
        }

        /// <summary>
        /// Atualiza somente a apresentacao da casca permanente. As referencias
        /// e callbacks da loja permanecem intactos.
        /// </summary>
        public void ApplyProfessionalTheme()
        {
            if (_professionalThemeApplied || root == null)
                return;
            _professionalThemeApplied = true;

            MasterDuelTypography.ApplyToHierarchy(root);
            StyleBackground();
            StyleHeader();
            StyleCurrency();
            StyleTabs();
            StyleCatalog();
            StyleFeedback();
        }

        public void ConfigureCatalogLayout(bool icons)
        {
            if (catalogGrid == null)
                return;

            catalogGrid.cellSize = icons ? iconCellSize : productCellSize;
            catalogGrid.spacing = icons ? iconSpacing : productSpacing;
            catalogGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            catalogGrid.constraintCount = icons
                ? Mathf.Max(1, iconColumns)
                : Mathf.Max(1, productColumns);
            if (catalogScroll != null)
                catalogScroll.verticalNormalizedPosition = 1f;
        }

        public void ClearCatalog()
        {
            if (catalogContent == null)
                return;
            for (int index = catalogContent.childCount - 1; index >= 0; index--)
            {
                Transform child = catalogContent.GetChild(index);
                child.SetParent(null, false);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private static void SetButtonAccent(Button button, Color color)
        {
            if (button == null)
                return;
            Outline outline = button.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = color;
        }

        private static void SetShopTabState(Button button, bool selected)
        {
            if (button == null)
                return;
            Color accent = selected
                ? ShopGold
                : new Color(ShopAmber.r, ShopAmber.g, ShopAmber.b, 0.58f);
            SetButtonAccent(button, accent);
            ArcaneShopSurfaceGraphic surface =
                button.GetComponentInChildren<ArcaneShopSurfaceGraphic>(true);
            if (surface != null)
                surface.SetStyle(accent, selected, 1f, 9f);
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
                label.color = selected ? Color.white : ShopMuted;
        }

        private void StyleBackground()
        {
            Image background = root.Find("Background")?.GetComponent<Image>();
            if (background != null)
            {
                Sprite professional = ResolveProfessionalBackground();
                if (professional != null)
                {
                    background.sprite = professional;
                    background.preserveAspect = false;
                }
            }
            Image contrast = root.Find("Background Contrast")
                ?.GetComponent<Image>();
            if (contrast != null)
                contrast.color = new Color(0.008f, 0.009f, 0.015f, 0.58f);

            RectTransform topRail = CreateDecoration(
                root,
                "Shop Gold Header Rail",
                new Vector2(0.03f, 0.902f),
                new Vector2(0.97f, 0.906f),
                ShopAmber);
            topRail.SetAsLastSibling();
            RectTransform bottomRail = CreateDecoration(
                root,
                "Shop Gold Footer Rail",
                new Vector2(0.055f, 0.042f),
                new Vector2(0.945f, 0.045f),
                new Color(ShopAmber.r, ShopAmber.g, ShopAmber.b, 0.52f));
            bottomRail.SetAsLastSibling();
        }

        private Sprite ResolveProfessionalBackground()
        {
            if (_runtimeProfessionalBackground != null)
                return _runtimeProfessionalBackground;
            Sprite imported = Resources.Load<Sprite>(
                "Shop/ShopBackgroundGold-v2");
            if (imported != null)
                return imported;
            Texture2D texture = Resources.Load<Texture2D>(
                "Shop/ShopBackgroundGold-v2");
            if (texture == null || texture.width <= 0 || texture.height <= 0)
                return null;
            _runtimeProfessionalBackground = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _runtimeProfessionalBackground.name =
                "Fundo Dourado Profissional da Loja";
            _runtimeProfessionalBackground.hideFlags = HideFlags.DontSave;
            return _runtimeProfessionalBackground;
        }

        private void StyleHeader()
        {
            RectTransform header = root.Find("Header") as RectTransform;
            if (header == null)
                return;
            SetAnchors(header,
                new Vector2(0.035f, 0.915f),
                new Vector2(0.73f, 0.982f));
            if (backButton != null)
            {
                SetAnchors(backButton.transform as RectTransform,
                    new Vector2(0f, 0.05f),
                    new Vector2(0.11f, 0.95f));
                StylePermanentButton(backButton, ShopAmber, true);
                Text backLabel = backButton.GetComponentInChildren<Text>(true);
                if (backLabel != null)
                {
                    backLabel.text = "‹";
                    backLabel.fontSize = 31;
                    MasterDuelTypography.Apply(
                        backLabel,
                        FontStyle.Bold,
                        backLabel.fontSize);
                }
            }

            Text title = header.Find("Title")?.GetComponent<Text>();
            if (title != null)
            {
                title.text = "LOJA";
                title.fontSize = 34;
                title.color = Color.white;
                SetAnchors(title.rectTransform,
                    new Vector2(0.14f, 0.32f),
                    new Vector2(0.48f, 1f));
                MasterDuelTypography.Apply(title, FontStyle.Bold, 34);
                EnsureTextShadow(title, 2f);
            }
            Text subtitle = CreateDecorationText(
                header,
                "Shop Subtitle",
                "MASTER DUEL 2 PLUS ULTRA  •  MERCADO DE DUELISTAS",
                12,
                ShopGold,
                new Vector2(0.14f, 0f),
                new Vector2(0.94f, 0.39f),
                TextAnchor.MiddleLeft);
            EnsureTextShadow(subtitle, 1f);
        }

        private void StyleCurrency()
        {
            RectTransform panel = root.Find("Currency Panel") as RectTransform;
            if (panel == null)
                return;
            SetAnchors(panel,
                new Vector2(0.775f, 0.914f),
                new Vector2(0.965f, 0.978f));
            StylePermanentSurface(panel.gameObject, ShopGold, true, 8f);
            Image image = panel.GetComponent<Image>();
            if (image != null)
                image.color = Color.clear;

            RectTransform icon = panel.Find("Currency Icon") as RectTransform;
            SetAnchors(icon,
                new Vector2(0.07f, 0.16f),
                new Vector2(0.25f, 0.84f));
            if (coinBalanceText != null)
            {
                coinBalanceText.fontSize = 27;
                coinBalanceText.color = Color.white;
                SetAnchors(coinBalanceText.rectTransform,
                    new Vector2(0.46f, 0.10f),
                    new Vector2(0.91f, 0.90f));
                coinBalanceText.alignment = TextAnchor.MiddleRight;
                MasterDuelTypography.Apply(
                    coinBalanceText,
                    FontStyle.Bold,
                    27);
                EnsureTextShadow(coinBalanceText, 1.5f);
            }
            CreateDecorationText(
                panel,
                "Balance Kicker",
                "SALDO",
                10,
                ShopMuted,
                new Vector2(0.25f, 0.08f),
                new Vector2(0.48f, 0.92f),
                TextAnchor.MiddleLeft);
        }

        private void StyleTabs()
        {
            RectTransform tabs = root.Find("Category Tabs") as RectTransform;
            if (tabs == null)
                return;
            SetAnchors(tabs,
                new Vector2(0.055f, 0.838f),
                new Vector2(0.945f, 0.895f));
            const float gap = 0.012f;
            float width = (1f - gap * 2f) / 3f;
            SetAnchors(packagesButton?.transform as RectTransform,
                Vector2.zero,
                new Vector2(width, 1f));
            SetAnchors(structureDecksButton?.transform as RectTransform,
                new Vector2(width + gap, 0f),
                new Vector2(width * 2f + gap, 1f));
            SetAnchors(profileIconsButton?.transform as RectTransform,
                new Vector2(width * 2f + gap * 2f, 0f),
                Vector2.one);

            StylePermanentButton(packagesButton, ShopGold, true);
            StylePermanentButton(structureDecksButton, ShopAmber, false);
            StylePermanentButton(profileIconsButton, ShopAmber, false);
        }

        private void StyleCatalog()
        {
            RectTransform frame = root.Find("Catalog Scroll View")
                as RectTransform;
            if (frame == null)
                return;
            SetAnchors(frame,
                new Vector2(0.055f, 0.055f),
                new Vector2(0.945f, 0.785f));
            StylePermanentSurface(frame.gameObject, ShopAmber, false, 13f);
            Image image = frame.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0.002f, 0.006f, 0.011f, 0.34f);
            RectTransform backplate = CreateDecoration(
                frame,
                "Catalog Obsidian Backplate",
                Vector2.zero,
                Vector2.one,
                new Color(0.003f, 0.006f, 0.010f, 0.88f));
            backplate.SetSiblingIndex(Mathf.Min(1, frame.childCount - 1));

            RectTransform viewport = frame.Find("Viewport") as RectTransform;
            if (viewport != null)
            {
                viewport.offsetMin = new Vector2(18f, 16f);
                viewport.offsetMax = new Vector2(-42f, -16f);
            }
            if (catalogGrid != null)
            {
                catalogGrid.padding = new RectOffset(16, 16, 14, 18);
                catalogGrid.spacing = productSpacing = new Vector2(18f, 18f);
                iconSpacing = new Vector2(16f, 18f);
                // Os emblemas precisam de mais altura que os cards de pacote:
                // isso preserva o hexagono, o nome e a acao em tres faixas
                // independentes, inclusive na proporcao 16:9 do Android.
                iconCellSize = new Vector2(
                    iconCellSize.x,
                    Mathf.Max(iconCellSize.y, 270f));
                catalogGrid.childAlignment = TextAnchor.UpperCenter;
            }

            Image track = frame.Find("Scrollbar")?.GetComponent<Image>();
            if (track != null)
                track.color = new Color(0.045f, 0.036f, 0.025f, 0.90f);
            Image handle = frame.Find("Scrollbar/Sliding Area/Handle")
                ?.GetComponent<Image>();
            if (handle != null)
                handle.color = ShopAmber;
        }

        private void StyleFeedback()
        {
            if (feedbackText == null)
                return;
            SetAnchors(feedbackText.rectTransform,
                new Vector2(0.065f, 0.790f),
                new Vector2(0.935f, 0.832f));
            feedbackText.fontSize = 13;
            feedbackText.alignment = TextAnchor.MiddleLeft;
            feedbackText.color = ShopMuted;
            MasterDuelTypography.Apply(feedbackText, FontStyle.Bold, 13);
        }

        private static void StylePermanentButton(
            Button button,
            Color accent,
            bool raised)
        {
            if (button == null)
                return;
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = Color.clear;
            ArcaneShopSurfaceGraphic surface = StylePermanentSurface(
                button.gameObject,
                accent,
                raised,
                8f);
            button.targetGraphic = surface;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.18f);
            colors.pressedColor = Color.Lerp(Color.white, accent, 0.42f);
            colors.selectedColor = Color.Lerp(Color.white, accent, 0.22f);
            colors.fadeDuration = 0.10f;
            button.colors = colors;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = raised ? Color.white : ShopMuted;
                MasterDuelTypography.Apply(
                    label,
                    FontStyle.Bold,
                    label.fontSize);
                EnsureTextShadow(label, 1.2f);
            }
        }

        private static ArcaneShopSurfaceGraphic StylePermanentSurface(
            GameObject target,
            Color accent,
            bool raised,
            float chamfer)
        {
            Transform existing = target.transform.Find("Professional Surface");
            RectTransform rect;
            ArcaneShopSurfaceGraphic surface;
            if (existing != null)
            {
                rect = existing as RectTransform;
                surface = existing.GetComponent<ArcaneShopSurfaceGraphic>();
            }
            else
            {
                var item = new GameObject(
                    "Professional Surface",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(ArcaneShopSurfaceGraphic));
                rect = item.GetComponent<RectTransform>();
                rect.SetParent(target.transform, false);
                rect.SetAsFirstSibling();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                surface = item.GetComponent<ArcaneShopSurfaceGraphic>();
            }
            surface.raycastTarget = false;
            surface.SetStyle(accent, raised, 1f, chamfer);
            return surface;
        }

        private static RectTransform CreateDecoration(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform found)
                return found;
            var item = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetAnchors(rect, min, max);
            Image image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text CreateDecorationText(
            Transform parent,
            string name,
            string value,
            int size,
            Color color,
            Vector2 min,
            Vector2 max,
            TextAnchor alignment)
        {
            Transform existing = parent.Find(name);
            Text text;
            if (existing != null)
            {
                text = existing.GetComponent<Text>();
            }
            else
            {
                var item = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Text));
                item.transform.SetParent(parent, false);
                text = item.GetComponent<Text>();
            }
            SetAnchors(text.rectTransform, min, max);
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(8, size - 3);
            text.resizeTextMaxSize = size;
            MasterDuelTypography.Apply(text, FontStyle.Bold, size);
            return text;
        }

        private static void EnsureTextShadow(Text text, float distance)
        {
            if (text == null)
                return;
            Shadow shadow = text.GetComponent<Shadow>() ??
                text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.78f);
            shadow.effectDistance = new Vector2(distance, -distance);
            shadow.useGraphicAlpha = true;
        }

        private static void SetAnchors(
            RectTransform rect,
            Vector2 min,
            Vector2 max)
        {
            if (rect == null)
                return;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void BindButton(
            Button button,
            ref UnityAction stored,
            UnityAction action)
        {
            if (button == null || action == null)
                return;
            stored = action;
            button.onClick.AddListener(stored);
        }

        private void Unbind()
        {
            RemoveButton(backButton, ref _backAction);
            RemoveButton(packagesButton, ref _packagesAction);
            RemoveButton(structureDecksButton, ref _structureDecksAction);
            RemoveButton(profileIconsButton, ref _profileIconsAction);
        }

        private static void RemoveButton(Button button, ref UnityAction action)
        {
            if (button != null && action != null)
                button.onClick.RemoveListener(action);
            action = null;
        }

        private void OnDestroy()
        {
            Unbind();
            if (_runtimeProfessionalBackground != null &&
                (_runtimeProfessionalBackground.hideFlags &
                 HideFlags.DontSave) != 0)
            {
                Destroy(_runtimeProfessionalBackground);
            }
            _runtimeProfessionalBackground = null;
        }
    }
}
