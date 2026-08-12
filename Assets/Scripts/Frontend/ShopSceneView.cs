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
            SetButtonAccent(packagesButton, selectedTab == 0 ? selected : idle);
            SetButtonAccent(
                structureDecksButton,
                selectedTab == 1 ? selected : idle);
            SetButtonAccent(
                profileIconsButton,
                selectedTab == 2 ? selected : idle);
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
        }
    }
}
