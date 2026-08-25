using System;
using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Cards;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using ArcaneArena.StoryRoguelite;
using ArcaneDuel.Game;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap : MonoBehaviour,
        ICardSelectionContext
    {
        private const int MainDeckMinimum = 40;
        private const int MainDeckMaximum = 60;
        private const int MainDeckGridColumns = 11;
        private const int ExtraDeckMaximum = 15;
        private const int CopyLimit = 3;
        private const string MainMenuSceneName = "MainMenu";
        private const string LoginSceneName = "Login";
        private const string DeckEditorSceneName = "DeckEditor";
        private const string DuelArenaSceneName = "DuelArena";
        public const int CurrentEditorPreviewVersion = 11;

        private static readonly Color Background = Hex("#040812");
        private static readonly Color Panel = Hex("#091426");
        private static readonly Color PanelLight = Hex("#112842");
        private static readonly Color Cyan = Hex("#34DDF4");
        private static readonly Color Lime = Hex("#C8FF19");
        private static readonly Color Blue = Hex("#3587FF");
        private static readonly Color Gold = Hex("#F2C766");
        private static readonly Color Muted = Hex("#91A6BA");
        private static readonly Color Danger = Hex("#FF556E");
        private static readonly Color Ink = Hex("#061019");

        [Header("Arte substituível dos porta-decks")]
        [SerializeField] private Sprite defaultDeckCaseSprite;
        [SerializeField] private Sprite[] deckCaseVariants =
            Array.Empty<Sprite>();
        [Header("Dados compartilhados entre cenas")]
        [SerializeField] private CardCatalog cardCatalog;
        [SerializeField, HideInInspector]
        private int editorPreviewVersion;
        [SerializeField, HideInInspector]
        private int editorPreviewScreen = 2;

        private enum PendingDuelMode
        {
            None,
            LocalTest,
            Bot,
            StoryRoguelite
        }

        private sealed class FrontendLayoutOverride
        {
            public bool ActiveSelf;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector2 Pivot;
            public Vector3 LocalScale;
            public Vector3 LocalEulerAngles;
            public Sprite PresentationSprite;
            public Color? ImageColor;
            public bool PreserveAspect;
            public bool? ImageEnabled;
            public Image.Type? ImageType;
            public int? FontSize;
            public Color? TextColor;
            public TextAnchor? TextAlignment;
            public FontStyle? FontStyle;
            public bool? TextEnabled;
            public Vector2? GridCellSize;
            public Vector2? GridSpacing;
            public RectOffset GridPadding;
            public int? GridConstraintCount;
            public TextAnchor? GridChildAlignment;
        }

        private static Font _font;
        private static PendingDuelMode _pendingDuelMode;
        private static DuelDeckLoadout _pendingBotLoadout;
        private static DuelDeckLoadout _pendingPlayerLoadout;
        private static bool _pendingRankedBotDuel;
        private static RankedMatchSnapshot _activeRankedBotMatch;
        private static BotProfile _activeRankedBotProfile;
        private static bool _activeRankedBotResultCommitted;
        private static string _activeDuelStatisticsId = string.Empty;
        private static bool _activeDuelStatisticsRanked;
        public static string ActiveDuelStatisticsId =>
            _activeDuelStatisticsId ?? string.Empty;
        public static bool ActiveDuelStatisticsRanked =>
            _activeDuelStatisticsRanked;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private RectTransform _screenRoot;
        private RectTransform _dragGhost;
        private RectTransform _mainDropZone;
        private RectTransform _extraDropZone;
        private RectTransform _catalogDropZone;
        private RectTransform _mainDeckContent;
        private RectTransform _extraDeckContent;
        private RectTransform _catalogContent;
        private Text _mainDeckCountText;
        private Text _mainDeckLimitText;
        private Text _extraDeckCountText;
        private Text _editorStatus;
        private Text _duelRoomStatus;
        private InputField _catalogSearchInput;
        private Image _deckEditorDetailArtwork;
        private Image _deckEditorCardHeader;
        private Image _deckEditorEffectHeader;
        private GameObject _deckEditorZoomOverlay;
        private Image _deckEditorZoomArtwork;
        private ArcaneArena.CardZoomViewer _deckEditorZoomViewer;
        private Text _deckEditorDetailName;
        private Text _deckEditorDetailType;
        private Text _deckEditorDetailEffect;
        private CardCatalog _catalog;
        private CardArenaBootstrap _duelArena;
        private MasterDuelArena3D _duelField;
        private DuelTestPerspectiveController _perspective;
        private DeckRepository _repository;
        private DeckRecord _selectedDeck;
        private DeckRecord _editingDeck;
        private bool _duelPresentationVisible;
        private bool _editorRefreshQueued;
        private bool _accountBootstrapPending;
        private string _catalogSearch = string.Empty;
        private string _shopFeedback = string.Empty;
        private bool _shopFeedbackIsError;
        private CardCategory _catalogFilter = CardCategory.Unknown;
        private bool _catalogSortDescending;
        private string _deckEditorSelectedCardId = string.Empty;
        private readonly Dictionary<CardCategory, Image>
            _catalogFilterButtons = new();
        private readonly Dictionary<string, FrontendLayoutOverride>
            _editorLayoutOverrides =
                new(StringComparer.Ordinal);
        private readonly HashSet<string>
            _editorLayoutPaths =
                new(StringComparer.Ordinal);
        private const string EditorCardNameRole =
            "ROLE:CARD_NAME";
        private const string EditorCatalogResultsRole =
            "ROLE:CATALOG_RESULTS";
        public static GameFrontendBootstrap Instance { get; private set; }
        public bool NeedsEditorPreviewRebuild =>
            editorPreviewVersion != CurrentEditorPreviewVersion;
        public bool IsInDuel => IsActiveScene(DuelArenaSceneName);
        public bool IsTextInputFocused
        {
            get
            {
                GameObject selected = EventSystem.current?.currentSelectedGameObject;
                return selected != null &&
                    selected.GetComponentInParent<InputField>() != null;
            }
        }

        public bool TryGetSelectedCardId(out string cardId)
        {
            cardId = _deckEditorSelectedCardId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(cardId) ||
                DeckRepository.ResolveCard(_catalog, cardId) == null)
            {
                cardId = string.Empty;
                return false;
            }
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureFrontendExists()
        {
            if (string.Equals(
                    SceneManager.GetActiveScene().name,
                    LoginSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (FindAnyObjectByType<GameFrontendBootstrap>(
                    FindObjectsInactive.Include) != null)
                return;

            var root = new GameObject("Interface Principal");
            root.AddComponent<GameFrontendBootstrap>();
        }

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveProjectReferences();
            CaptureEditorLayoutOverrides();
            bool usesAuthoredMainMenu = TryAttachAuthoredMainMenu();
            if (!usesAuthoredMainMenu)
            {
                ClearGeneratedFrontend();
                ResolveProjectReferences();
                BuildCanvas();
            }
            _repository = new DeckRepository();
            _repository.Load(_catalog);
            PlayerCloudSaveRuntime.Attach(_repository);
            _accountBootstrapPending = !IsDuelSceneName(
                SceneManager.GetActiveScene().name);
            InitializePlayerIdAccess();
            InitializeCoinRewardAuthorization();
            if (_accountBootstrapPending)
                ShowAccountBootstrapScreen();
            else
                InitializeScenePresentation();
            if (!IsActiveScene(DuelArenaSceneName) &&
                !HasCommandArgument("-arcaneSkipTitle"))
            {
                string capturePath =
                    CommandArgumentValue("-arcaneCapture");
                if (!string.IsNullOrWhiteSpace(capturePath))
                {
                    StartCoroutine(
                        CaptureFrontendAndExit(
                            capturePath,
                            CommandArgumentValue(
                                "-arcaneCaptureState")));
                }
            }
        }

        public void BuildEditorPreview()
        {
            if (Application.isPlaying)
                return;

            ClearGeneratedFrontend();
            ResolveProjectReferences();
            _repository = new DeckRepository();
            _repository.Load(_catalog, false);
            BuildCanvas();

            var sceneName = SceneManager.GetActiveScene().name;
            if (string.Equals(
                    sceneName,
                    DeckEditorSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                var previewDeck =
                    _repository.SelectedDeck ??
                    (_repository.State.decks.Count > 0
                        ? _repository.State.decks[0]
                        : null);
                if (previewDeck == null ||
                    editorPreviewScreen <= 0)
                {
                    ShowDeckGallery();
                }
                else if (editorPreviewScreen == 1)
                {
                    ShowDeckDetails(previewDeck);
                }
                else
                {
                    ShowDeckEditor(previewDeck);
                }
            }
            else
            {
                ShowMainMenu();
            }

            editorPreviewVersion = CurrentEditorPreviewVersion;
        }

        private void CaptureEditorLayoutOverrides()
        {
            _editorLayoutOverrides.Clear();
            _editorLayoutPaths.Clear();
            // Uma prévia serializada de versão anterior não deve substituir
            // a composição atual do editor em runtime. Ao reconstruir a cena,
            // a nova versão volta a ser capturada e permanece editável.
            if (NeedsEditorPreviewRebuild)
                return;

            var fullEditorPreview =
                FindDescendantByName(
                    transform,
                    "03 - Editor Completo");
            var previewRoot =
                FindDescendantByName(
                    fullEditorPreview ?? transform,
                    "Tela Atual");
            if (previewRoot == null)
                return;

            CaptureEditorLayoutOverrides(
                previewRoot,
                "Tela Atual#0");

            var cardName =
                FindDescendantByName(
                    fullEditorPreview ?? transform,
                    "NOMECARD");
            if (cardName != null)
            {
                CaptureEditorLayoutOverrides(
                    cardName,
                    EditorCardNameRole);
            }

            var catalogResults =
                FindDescendantTextContaining(
                    fullEditorPreview ?? transform,
                    "CLIQUE: DETALHES");
            if (catalogResults != null)
            {
                CaptureEditorLayoutOverrides(
                    catalogResults.transform,
                    EditorCatalogResultsRole);
            }
        }

        private void CaptureEditorLayoutOverrides(
            Transform current,
            string path)
        {
            _editorLayoutPaths.Add(path);
            if (current is RectTransform rect)
            {
                var snapshot =
                    new FrontendLayoutOverride
                    {
                        ActiveSelf =
                            current.gameObject.activeSelf,
                        AnchorMin = rect.anchorMin,
                        AnchorMax = rect.anchorMax,
                        AnchoredPosition =
                            rect.anchoredPosition,
                        SizeDelta = rect.sizeDelta,
                        Pivot = rect.pivot,
                        LocalScale =
                            rect.localScale,
                        LocalEulerAngles =
                            rect.localEulerAngles
                    };
                var image =
                    current.GetComponent<Image>();
                if (image != null)
                {
                    snapshot.ImageColor = image.color;
                    snapshot.PreserveAspect =
                        image.preserveAspect;
                    snapshot.ImageEnabled =
                        image.enabled;
                    snapshot.ImageType =
                        image.type;
                    if (image.sprite != null &&
                        (_catalog == null ||
                         _catalog.FindBySprite(
                             image.sprite) == null))
                    {
                        snapshot.PresentationSprite =
                            image.sprite;
                    }
                }
                var text =
                    current.GetComponent<Text>();
                if (text != null)
                {
                    snapshot.FontSize =
                        text.fontSize;
                    snapshot.TextColor =
                        text.color;
                    snapshot.TextAlignment =
                        text.alignment;
                    snapshot.FontStyle =
                        text.fontStyle;
                    snapshot.TextEnabled =
                        text.enabled;
                }
                var grid =
                    current.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    snapshot.GridCellSize =
                        grid.cellSize;
                    snapshot.GridSpacing =
                        grid.spacing;
                    snapshot.GridPadding =
                        new RectOffset(
                            grid.padding.left,
                            grid.padding.right,
                            grid.padding.top,
                            grid.padding.bottom);
                    snapshot.GridConstraintCount =
                        grid.constraintCount;
                    snapshot.GridChildAlignment =
                        grid.childAlignment;
                }
                _editorLayoutOverrides[path] =
                    snapshot;
            }

            for (var i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                CaptureEditorLayoutOverrides(
                    child,
                    BuildStableLayoutPath(path, child));
            }
        }

        private void ApplyEditorLayoutOverrides()
        {
            if (_editorLayoutOverrides.Count == 0 ||
                _screenRoot == null)
            {
                return;
            }

            if (_canvas != null)
            {
                var scaler =
                    _canvas.GetComponent<CanvasScaler>();
                UniversalUiLayout.ConfigureCanvasScaler(scaler);
            }

            ApplyEditorLayoutOverrides(
                _screenRoot,
                "Tela Atual#0",
                false);

            if (_deckEditorDetailName != null)
            {
                ApplyEditorLayoutOverrides(
                    _deckEditorDetailName.transform,
                    EditorCardNameRole,
                    true);
            }
            if (_editorStatus != null)
            {
                ApplyEditorLayoutOverrides(
                    _editorStatus.transform,
                    EditorCatalogResultsRole,
                    true);
            }

            var selectedEntry =
                DeckRepository.ResolveCard(
                    _catalog,
                    _deckEditorSelectedCardId);
            ApplyDeckEditorCardTheme(selectedEntry);
        }

        private void ApplyEditorLayoutOverrides(
            Transform current,
            string path,
            bool insideDynamicContent)
        {
            if (current is RectTransform rect &&
                _editorLayoutOverrides.TryGetValue(
                    path,
                    out var snapshot))
            {
                rect.anchorMin = snapshot.AnchorMin;
                rect.anchorMax = snapshot.AnchorMax;
                rect.anchoredPosition =
                    snapshot.AnchoredPosition;
                rect.sizeDelta = snapshot.SizeDelta;
                rect.pivot = snapshot.Pivot;
                rect.localScale = snapshot.LocalScale;
                rect.localEulerAngles =
                    snapshot.LocalEulerAngles;
                current.gameObject.SetActive(
                    snapshot.ActiveSelf);

                var image =
                    current.GetComponent<Image>();
                if (image != null)
                {
                    if (snapshot.ImageColor.HasValue)
                        image.color =
                            snapshot.ImageColor.Value;
                    image.preserveAspect =
                        snapshot.PreserveAspect;
                    if (snapshot.ImageEnabled.HasValue)
                        image.enabled =
                            snapshot.ImageEnabled.Value;
                    if (snapshot.ImageType.HasValue)
                        image.type =
                            snapshot.ImageType.Value;
                    if (snapshot.PresentationSprite != null)
                        image.sprite =
                            snapshot.PresentationSprite;
                }
                var text =
                    current.GetComponent<Text>();
                if (text != null)
                {
                    if (snapshot.FontSize.HasValue)
                        text.fontSize =
                            snapshot.FontSize.Value;
                    if (snapshot.TextColor.HasValue)
                        text.color =
                            snapshot.TextColor.Value;
                    if (snapshot.TextAlignment.HasValue)
                        text.alignment =
                            snapshot.TextAlignment.Value;
                    if (snapshot.FontStyle.HasValue)
                        text.fontStyle =
                            snapshot.FontStyle.Value;
                    if (snapshot.TextEnabled.HasValue)
                        text.enabled =
                            snapshot.TextEnabled.Value;
                }
                var grid =
                    current.GetComponent<GridLayoutGroup>();
                if (grid != null)
                {
                    if (snapshot.GridCellSize.HasValue)
                        grid.cellSize =
                            snapshot.GridCellSize.Value;
                    if (snapshot.GridSpacing.HasValue)
                        grid.spacing =
                            snapshot.GridSpacing.Value;
                    if (snapshot.GridPadding != null)
                        grid.padding =
                            new RectOffset(
                                snapshot.GridPadding.left,
                                snapshot.GridPadding.right,
                                snapshot.GridPadding.top,
                                snapshot.GridPadding.bottom);
                    if (snapshot.GridConstraintCount.HasValue)
                        grid.constraintCount =
                            snapshot.GridConstraintCount.Value;
                    if (snapshot.GridChildAlignment.HasValue)
                        grid.childAlignment =
                            snapshot.GridChildAlignment.Value;
                }
            }

            var childrenAreDynamic =
                insideDynamicContent ||
                IsDynamicCardContainer(current);
            for (var i = current.childCount - 1; i >= 0; i--)
            {
                var child = current.GetChild(i);
                var childPath =
                    BuildStableLayoutPath(path, child);
                if (!childrenAreDynamic &&
                    !_editorLayoutPaths.Contains(childPath) &&
                    !IsEditorRoleTransform(child))
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                    continue;
                }
                ApplyEditorLayoutOverrides(
                    child,
                    childPath,
                    childrenAreDynamic);
            }
        }

        private static string BuildStableLayoutPath(
            string parentPath,
            Transform child)
        {
            var occurrence = 0;
            for (var i = 0; i < child.GetSiblingIndex(); i++)
            {
                if (string.Equals(
                        child.parent.GetChild(i).name,
                        child.name,
                        StringComparison.Ordinal))
                {
                    occurrence++;
                }
            }
            return $"{parentPath}/{child.name}#{occurrence}";
        }

        private static bool IsDynamicCardContainer(
            Transform current)
        {
            return string.Equals(
                       current.name,
                       "Conteúdo Fixo",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       current.name,
                       "Conteúdo",
                   StringComparison.Ordinal);
        }

        private bool IsEditorRoleTransform(
            Transform current)
        {
            return (_deckEditorDetailName != null &&
                    current ==
                    _deckEditorDetailName.transform) ||
                   (_editorStatus != null &&
                    current ==
                    _editorStatus.transform) ||
                   IsTransformInside(
                       current,
                       _catalogAdvancedFilterButton != null
                           ? _catalogAdvancedFilterButton.transform
                           : null) ||
                   IsTransformInside(
                       current,
                       _deckEditorRelatedCardsButton != null
                           ? _deckEditorRelatedCardsButton.transform
                           : null);
        }

        private static bool IsTransformInside(
            Transform current,
            Transform root)
        {
            return current != null && root != null &&
                   (current == root || current.IsChildOf(root));
        }

        private static Transform FindDescendantByName(
            Transform parent,
            string objectName)
        {
            if (parent == null)
                return null;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(
                        child.name,
                        objectName,
                        StringComparison.Ordinal))
                {
                    return child;
                }
                var nested =
                    FindDescendantByName(
                        child,
                        objectName);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private static Text FindDescendantTextContaining(
            Transform parent,
            string textFragment)
        {
            if (parent == null)
                return null;
            var text = parent.GetComponent<Text>();
            if (text != null &&
                !string.IsNullOrEmpty(text.text) &&
                text.text.Contains(textFragment))
            {
                return text;
            }
            for (var i = 0; i < parent.childCount; i++)
            {
                var nested =
                    FindDescendantTextContaining(
                        parent.GetChild(i),
                        textFragment);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private void ClearGeneratedFrontend()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            _canvas = null;
            _canvasRect = null;
            _screenRoot = null;
            _shopSceneView = null;
            _dragGhost = null;
            _mainDropZone = null;
            _extraDropZone = null;
            _catalogContent = null;
            _catalogFilterButtons.Clear();
        }

        private void Update()
        {
            if (Keyboard.current == null ||
                !Keyboard.current.escapeKey.wasPressedThisFrame)
                return;

            if (_packOpeningSequenceActive)
                return;

            if (_deckEditorZoomOverlay != null &&
                _deckEditorZoomOverlay.activeSelf)
            {
                CloseDeckEditorZoom();
                return;
            }

            if (_starterClaimModal != null)
            {
                Destroy(_starterClaimModal);
                _starterClaimModal = null;
                return;
            }

            if (_deckDeleteModal != null)
            {
                Destroy(_deckDeleteModal);
                _deckDeleteModal = null;
                return;
            }

            if (_shopBackAction != null)
            {
                Action backAction = _shopBackAction;
                _shopBackAction = null;
                backAction.Invoke();
                return;
            }

            if (_duelPresentationVisible)
                ToggleDuelMenu();
        }

        private void InitializeScenePresentation()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (!IsDuelSceneName(sceneName) &&
                Array.Exists(
                    Environment.GetCommandLineArgs(),
                    argument => string.Equals(
                        argument,
                        "-arcaneSkipTitle",
                        StringComparison.OrdinalIgnoreCase)))
            {
                _pendingDuelMode = PendingDuelMode.LocalTest;
                SceneManager.LoadScene(DuelArenaSceneName);
                return;
            }

            if (string.Equals(
                    sceneName,
                    DeckEditorSceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                ShowDeckGallery();
                return;
            }

            if (IsDuelSceneName(sceneName))
            {
                StartCoroutine(StartRequestedDuelAfterArenaReset());
                return;
            }

            if (_accountBootstrapPending)
            {
                ShowAccountBootstrapScreen();
                return;
            }

            if (PlayerAccountRuntime.ConsumeRestoreRequest())
            {
                ShowAccountCredentials(true);
                return;
            }

            if (_repository != null && !_repository.HasPlayerProfile)
            {
                ShowPlayerProfileSetup();
                return;
            }

            if (_repository != null && _repository.NeedsStarterDeckSelection)
            {
                ShowStarterDeckSelection();
                return;
            }

            if (StoryRogueliteRuntime.ReturnToStoryRequested)
            {
                StoryRogueliteRuntime.ConsumeReturnRequest();
                ShowStoryRoguelite();
                return;
            }

            ShowMainMenu();
        }

        private void ResolveProjectReferences()
        {
            _duelArena = FindPreferredDuelArena(_duelArena);
            _duelField = FindAnyObjectByType<MasterDuelArena3D>(
                FindObjectsInactive.Include);
            _perspective = FindAnyObjectByType<DuelTestPerspectiveController>(
                FindObjectsInactive.Include);
            _catalog = cardCatalog != null
                ? cardCatalog
                : _duelArena != null
                    ? _duelArena.CardCatalog
                    : null;

            if (_catalog == null)
            {
                foreach (var loaded in Resources.FindObjectsOfTypeAll<CardCatalog>())
                {
                    if (loaded == null)
                        continue;
                    _catalog = loaded;
                    break;
                }
            }
        }

        private void BuildCanvas()
        {
            _font = MasterDuelTypography.Resolve(FontStyle.Normal, 17);
            var canvasObject = new GameObject(
                "Frontend Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            _canvasRect = canvasObject.GetComponent<RectTransform>();

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            UniversalUiLayout.ConfigureCanvasScaler(scaler);

            var responsiveFrame =
                UniversalUiLayout.CreateFrame(
                    canvasObject.transform,
                    false);

            var root = new GameObject(
                "Tela Atual",
                typeof(RectTransform));
            root.transform.SetParent(responsiveFrame, false);
            _screenRoot = root.GetComponent<RectTransform>();
            Stretch(_screenRoot);
            EnsureEventSystem();
        }

        private void ShowPanelMainMenuLegacy()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("NEXO DE DUELO");

            var selectedDeck = _repository?.SelectedDeck;
            var duelRejection = string.Empty;
            var canStartDuel =
                _repository != null &&
                _repository.TryCreateSelectedLoadout(
                    out _,
                    out duelRejection);
            var navigation = CreatePanel(
                _screenRoot,
                "Navegação Principal",
                new Vector2(0.025f, 0.12f),
                new Vector2(0.235f, 0.84f),
                new Color(0.01f, 0.025f, 0.06f, 0.94f));
            AddOutline(
                navigation.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.58f),
                new Vector2(2f, -2f));
            CreateText(
                navigation.transform,
                "ARCANE\nARENA",
                47,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.70f),
                new Vector2(0.92f, 0.95f),
                TextAnchor.MiddleLeft);
            CreateText(
                navigation.transform,
                "NEXO DE DUELO",
                16,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.09f, 0.64f),
                new Vector2(0.92f, 0.73f),
                TextAnchor.MiddleLeft);
            CreateText(
                navigation.transform,
                "DUELISTA",
                12,
                FontStyle.Bold,
                Gold,
                new Vector2(0.09f, 0.585f),
                new Vector2(0.92f, 0.64f),
                TextAnchor.MiddleLeft);
            CreateText(
                navigation.transform,
                _repository.PlayerDisplayName.ToUpperInvariant(),
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.09f, 0.535f),
                new Vector2(0.92f, 0.595f),
                TextAnchor.MiddleLeft);
            CreateMenuButton(
                navigation.transform,
                "DUELAR",
                new Vector2(0.08f, 0.425f),
                new Vector2(0.92f, 0.515f),
                Cyan,
                ShowDuelRoom);
            CreateMenuButton(
                navigation.transform,
                "DECKS",
                new Vector2(0.08f, 0.315f),
                new Vector2(0.92f, 0.405f),
                Lime,
                OpenDeckEditorScene);
            CreateMenuButton(
                navigation.transform,
                "LOJA",
                new Vector2(0.08f, 0.205f),
                new Vector2(0.92f, 0.295f),
                Blue,
                ShowDeckShop);
            CreateButton(
                navigation.transform,
                "OPÇÕES",
                new Vector2(0.08f, 0.105f),
                new Vector2(0.92f, 0.165f),
                Gold,
                ShowAnimationOptions);
            CreateButton(
                navigation.transform,
                "PERFIL",
                new Vector2(0.08f, 0.04f),
                new Vector2(0.92f, 0.09f),
                Cyan,
                () => ShowPlayerProfileSetup(true));

            var hero = CreatePanel(
                _screenRoot,
                "Próximo Duelo",
                new Vector2(0.255f, 0.12f),
                new Vector2(0.975f, 0.84f),
                new Color(0.015f, 0.055f, 0.105f, 0.96f));
            AddOutline(
                hero.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.68f),
                new Vector2(3f, -3f));
            var heroAccent = CreatePanel(
                hero.transform,
                "Pulso do Duelo",
                new Vector2(0.035f, 0.805f),
                new Vector2(0.965f, 0.818f),
                new Color(Lime.r, Lime.g, Lime.b, 0.94f));
            heroAccent.raycastTarget = false;
            CreateText(
                hero.transform,
                "PRÓXIMO DUELO",
                20,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.06f, 0.83f),
                new Vector2(0.94f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateText(
                hero.transform,
                selectedDeck?.displayName?.ToUpperInvariant() ??
                    "SELECIONE UM DECK",
                32,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.06f, 0.70f),
                new Vector2(0.94f, 0.82f),
                TextAnchor.MiddleLeft);
            CreateText(
                hero.transform,
                selectedDeck == null
                    ? "Abra seus decks para preparar a primeira partida."
                    : $"DECK ATIVO  •  {selectedDeck.mainDeckCardIds?.Count ?? 0} PRINCIPAL  •  " +
                      $"{selectedDeck.extraDeckCardIds?.Count ?? 0} ADICIONAL",
                16,
                FontStyle.Bold,
                selectedDeck == null ? Gold : Muted,
                new Vector2(0.06f, 0.63f),
                new Vector2(0.94f, 0.71f),
                TextAnchor.MiddleLeft);
            if (selectedDeck != null)
            {
                var deckShowcase = CreatePanel(
                    hero.transform,
                    "Vitrine do Deck",
                    new Vector2(0.16f, 0.21f),
                    new Vector2(0.84f, 0.59f),
                    new Color(0.005f, 0.02f, 0.045f, 0.76f));
                AddOutline(
                    deckShowcase.gameObject,
                    new Color(Cyan.r, Cyan.g, Cyan.b, 0.24f),
                    new Vector2(1f, -1f));
                CreateDuelDeckPreview(
                    deckShowcase.transform,
                    selectedDeck,
                    new Vector2(0.14f, 0.07f),
                    new Vector2(0.86f, 0.92f));
            }
            else
            {
                CreateText(
                    hero.transform,
                    "SEU PRÓXIMO DECK VAI GANHAR ESPAÇO AQUI",
                    19,
                    FontStyle.Bold,
                    new Color(Muted.r, Muted.g, Muted.b, 0.78f),
                    new Vector2(0.10f, 0.30f),
                    new Vector2(0.90f, 0.54f),
                    TextAnchor.MiddleCenter);
            }
            CreateButton(
                hero.transform,
                canStartDuel ? "ENFRENTAR BOT" : "PREPARAR DECK",
                new Vector2(0.32f, 0.08f),
                new Vector2(0.68f, 0.18f),
                canStartDuel ? Lime : Gold,
                () =>
                {
                    if (canStartDuel)
                    {
                        _pendingRankedBotDuel = false;
                        StartRandomBotDuel();
                        return;
                    }

                    ShowDuelRoom();
                    if (_duelRoomStatus != null)
                    {
                        _duelRoomStatus.text =
                            $"DECK AINDA NÃO ESTÁ PRONTO\n{duelRejection}";
                        _duelRoomStatus.color = Gold;
                    }
                });

            CreateText(
                _screenRoot,
                $"DUELISTA: {_repository.PlayerDisplayName.ToUpperInvariant()}  •  DADOS SALVOS NESTE DISPOSITIVO",
                14,
                FontStyle.Bold,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f),
                new Vector2(0.04f, 0.035f),
                new Vector2(0.96f, 0.085f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                $"v{ArcaneDuel.Game.ProjectIdentity.ProjectVersion} • CORE {ArcaneDuel.Game.ProjectIdentity.CoreApiVersion}",
                16,
                FontStyle.Bold,
                new Color(1f, 1f, 1f, 0.82f),
                new Vector2(0.90f, 0.025f),
                new Vector2(0.975f, 0.085f),
                TextAnchor.LowerRight);
        }

        private void ShowLegacyMainMenu()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("CENTRAL DE DUELOS");

            var navigation = CreatePanel(
                _screenRoot,
                "Navegação Principal",
                new Vector2(0.035f, 0.12f),
                new Vector2(0.34f, 0.84f),
                new Color(0.01f, 0.025f, 0.06f, 0.94f));
            AddOutline(navigation.gameObject, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f), new Vector2(2f, -2f));

            CreateText(
                navigation.transform,
                "ARCANE\nARENA",
                58,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.70f),
                new Vector2(0.92f, 0.95f),
                TextAnchor.MiddleLeft);
            CreateText(
                navigation.transform,
                "CARD DUEL ONLINE",
                18,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.09f, 0.64f),
                new Vector2(0.92f, 0.73f),
                TextAnchor.MiddleLeft);

            CreateText(
                navigation.transform,
                $"DUELISTA LOCAL  •  {_repository.PlayerDisplayName.ToUpperInvariant()}",
                15,
                FontStyle.Bold,
                Gold,
                new Vector2(0.09f, 0.58f),
                new Vector2(0.92f, 0.65f),
                TextAnchor.MiddleLeft);

            CreateMenuButton(
                navigation.transform,
                "DUELAR",
                new Vector2(0.08f, 0.46f),
                new Vector2(0.92f, 0.55f),
                Cyan,
                ShowDuelRoom);
            CreateMenuButton(
                navigation.transform,
                "DECKS",
                new Vector2(0.08f, 0.35f),
                new Vector2(0.92f, 0.44f),
                Lime,
                OpenDeckEditorScene);
            CreateMenuButton(
                navigation.transform,
                "LOJA",
                new Vector2(0.08f, 0.24f),
                new Vector2(0.92f, 0.33f),
                Blue,
                ShowDeckShop);
            CreateMenuButton(
                navigation.transform,
                "OPÇÕES",
                new Vector2(0.08f, 0.13f),
                new Vector2(0.92f, 0.22f),
                Gold,
                ShowAnimationOptions);
            CreateMenuButton(
                navigation.transform,
                "PERFIL",
                new Vector2(0.08f, 0.025f),
                new Vector2(0.92f, 0.105f),
                Cyan,
                () => ShowPlayerProfileSetup(true));

            var feature = CreatePanel(
                _screenRoot,
                "Destaque",
                new Vector2(0.38f, 0.12f),
                new Vector2(0.965f, 0.84f),
                new Color(0.025f, 0.06f, 0.11f, 0.92f));
            AddOutline(feature.gameObject, new Color(Blue.r, Blue.g, Blue.b, 0.5f), new Vector2(2f, -2f));

            CreateText(
                feature.transform,
                "MONTE. TESTE. DUELE.",
                38,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.06f, 0.77f),
                new Vector2(0.94f, 0.93f),
                TextAnchor.MiddleLeft);
            CreateText(
                feature.transform,
                "Seu catálogo já está conectado ao construtor de decks.",
                19,
                FontStyle.Normal,
                Muted,
                new Vector2(0.06f, 0.68f),
                new Vector2(0.94f, 0.78f),
                TextAnchor.MiddleLeft);

            var featured = ReadyCatalogEntries();
            for (var i = 0; i < Mathf.Min(3, featured.Count); i++)
            {
                var x = 0.16f + i * 0.23f;
                CreateCardArtwork(
                    feature.transform,
                    featured[i].Artwork,
                    new Vector2(x, 0.14f + Mathf.Abs(i - 1) * 0.025f),
                    new Vector2(x + 0.22f, 0.64f + Mathf.Abs(i - 1) * 0.025f),
                    (i - 1) * -7f,
                    true);
            }

            CreateText(
                _screenRoot,
                "PROTÓTIPO MULTIPLAYER-FIRST  •  DECKS SALVOS COM IDs OFICIAIS",
                14,
                FontStyle.Bold,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f),
                new Vector2(0.04f, 0.035f),
                new Vector2(0.96f, 0.085f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                $"v{ArcaneDuel.Game.ProjectIdentity.ProjectVersion} · CORE {ArcaneDuel.Game.ProjectIdentity.CoreApiVersion}",
                16,
                FontStyle.Bold,
                new Color(1f, 1f, 1f, 0.82f),
                new Vector2(0.90f, 0.025f),
                new Vector2(0.975f, 0.085f),
                TextAnchor.LowerRight);
        }

        private void ShowPlayerNameEditor(
            bool canReturn = false,
            Action backAction = null)
        {
            BuildModernPlayerNameEditor(
                canReturn,
                backAction ?? ShowMainMenu);
        }

        private void ShowAnimationOptions()
        {
            BuildModernAnimationOptionsScreen();
        }

        private void ShowDuelResponseOptions()
        {
            BuildModernDuelResponseOptionsScreen();
        }

        private void BuildGraphicsQualityRow(Transform parent, float yMin)
        {
            var row = CreatePanel(
                parent,
                "Qualidade gráfica",
                new Vector2(0.055f, yMin),
                new Vector2(0.945f, yMin + 0.115f),
                new Color(0.025f, 0.09f, 0.135f, 0.94f));
            AddOutline(
                row.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f),
                new Vector2(2f, -2f));
            CreateText(
                row.transform,
                "GRÁFICOS\n" + ArcaneGraphicsPreferences.DisplayName(
                    ArcaneGraphicsPreferences.Quality),
                16,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.02f, 0.12f),
                new Vector2(0.25f, 0.88f),
                TextAnchor.MiddleLeft);

            ArcaneGraphicsQuality[] levels =
            {
                ArcaneGraphicsQuality.VeryLow,
                ArcaneGraphicsQuality.Low,
                ArcaneGraphicsQuality.Medium,
                ArcaneGraphicsQuality.High,
                ArcaneGraphicsQuality.VeryHigh
            };
            string[] labels =
            {
                "M. BAIXO",
                "BAIXO",
                "MÉDIO",
                "ALTO",
                "M. ALTO"
            };
            for (int index = 0; index < levels.Length; index++)
            {
                ArcaneGraphicsQuality level = levels[index];
                float xMin = 0.27f + index * 0.139f;
                bool selected = ArcaneGraphicsPreferences.Quality == level;
                CreateButton(
                    row.transform,
                    labels[index],
                    new Vector2(xMin, 0.18f),
                    new Vector2(xMin + 0.125f, 0.82f),
                    selected ? Lime : Cyan,
                    () =>
                    {
                        ArcaneGraphicsPreferences.SetQuality(level);
                        ShowAnimationOptions();
                    });
            }
        }

        private void BuildAudioVolumeRow(Transform parent, float yMin)
        {
            var row = CreatePanel(
                parent,
                "Volumes de audio",
                new Vector2(0.055f, yMin),
                new Vector2(0.945f, yMin + 0.13f),
                new Color(0.025f, 0.09f, 0.135f, 0.94f));
            AddOutline(
                row.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f),
                new Vector2(2f, -2f));
            Text effectsValue = CreateText(
                row.transform,
                $"EFEITOS · {ArcaneAudioPreferences.Volume * 5f:0.0} / 5",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.54f),
                new Vector2(0.43f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateVolumeSlider(
                row.transform,
                "Volume dos Efeitos",
                ArcaneAudioPreferences.Volume * 5f,
                new Vector2(0.025f, 0.13f),
                new Vector2(0.44f, 0.48f),
                Cyan,
                value =>
                {
                    ArcaneAudioPreferences.Volume = value / 5f;
                    effectsValue.text = $"EFEITOS · {value:0.0} / 5";
                    RefreshMasterAudioState();
                });

            Text musicValue = CreateText(
                row.transform,
                $"MÚSICA · {ArcaneMusicPreferences.Volume * 5f:0.0} / 5",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.52f, 0.54f),
                new Vector2(0.93f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateVolumeSlider(
                row.transform,
                "Volume das Musicas",
                ArcaneMusicPreferences.Volume * 5f,
                new Vector2(0.52f, 0.13f),
                new Vector2(0.935f, 0.48f),
                Lime,
                value =>
                {
                    ArcaneMusicPreferences.Volume = value / 5f;
                    musicValue.text = $"MÚSICA · {value:0.0} / 5";
                    RefreshMasterAudioState();
                });
        }

        private static Slider CreateVolumeSlider(
            Transform parent,
            string name,
            float value,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action<float> onChanged)
        {
            Image track = CreatePanel(
                parent,
                name,
                min,
                max,
                new Color(0.035f, 0.075f, 0.11f, 1f));
            AddOutline(
                track.gameObject,
                new Color(accent.r, accent.g, accent.b, 0.52f),
                new Vector2(1.5f, -1.5f));

            GameObject fillAreaObject = new GameObject(
                "Area do Preenchimento",
                typeof(RectTransform));
            fillAreaObject.transform.SetParent(track.transform, false);
            RectTransform fillArea =
                fillAreaObject.GetComponent<RectTransform>();
            Stretch(fillArea);
            fillArea.offsetMin = new Vector2(12f, 6f);
            fillArea.offsetMax = new Vector2(-12f, -6f);
            Image fill = CreatePanel(
                fillArea,
                "Preenchimento",
                Vector2.zero,
                Vector2.one,
                new Color(accent.r, accent.g, accent.b, 0.92f));

            GameObject handleAreaObject = new GameObject(
                "Area da Alca",
                typeof(RectTransform));
            handleAreaObject.transform.SetParent(track.transform, false);
            RectTransform handleArea =
                handleAreaObject.GetComponent<RectTransform>();
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(12f, 2f);
            handleArea.offsetMax = new Vector2(-12f, -2f);
            Image handle = CreatePanel(
                handleArea,
                "Alca",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                Color.white);
            handle.rectTransform.sizeDelta = new Vector2(22f, 0f);
            AddOutline(handle.gameObject, accent, new Vector2(1.5f, -1.5f));

            Slider slider = track.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 5f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, 0f, 5f));
            slider.onValueChanged.AddListener(
                changedValue => onChanged?.Invoke(changedValue));
            return slider;
        }

        private static void RefreshMasterAudioState()
        {
            ArcaneAudioPreferences.Enabled =
                ArcaneAudioPreferences.Volume > 0.0001f ||
                ArcaneMusicPreferences.Volume > 0.0001f;
        }

        private void BuildAnimationOptionRow(
            Transform parent,
            string label,
            bool enabled,
            float currentSpeed,
            float yMin,
            Action toggle,
            Action<float> setSpeed)
        {
            const float rowHeight = 0.13f;
            var row = CreatePanel(
                parent,
                label,
                new Vector2(0.055f, yMin),
                new Vector2(0.945f, yMin + rowHeight),
                new Color(0.025f, 0.09f, 0.135f, 0.94f));
            AddOutline(
                row.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f),
                new Vector2(2f, -2f));
            CreateText(
                row.transform,
                label,
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.52f),
                new Vector2(0.35f, 0.93f),
                TextAnchor.MiddleLeft);
            CreateButton(
                row.transform,
                enabled ? "LIGADA" : "DESLIGADA",
                new Vector2(0.025f, 0.10f),
                new Vector2(0.35f, 0.48f),
                enabled ? Lime : Danger,
                toggle);

            CreateText(
                row.transform,
                $"VELOCIDADE  •  {currentSpeed:0.##}x",
                17,
                FontStyle.Bold,
                Gold,
                new Vector2(0.39f, 0.54f),
                new Vector2(0.97f, 0.93f),
                TextAnchor.MiddleCenter);

            var speeds = new[] { 0.75f, 1f, 1.5f, 2f };
            for (var index = 0; index < speeds.Length; index++)
            {
                var speed = speeds[index];
                var xMin = 0.39f + index * 0.145f;
                var selected =
                    Mathf.Approximately(
                        currentSpeed,
                        speed);
                CreateButton(
                    row.transform,
                    $"{speed:0.##}x",
                    new Vector2(xMin, 0.10f),
                    new Vector2(xMin + 0.125f, 0.48f),
                    selected ? Lime : Cyan,
                    () => setSpeed(speed));
            }
        }

        private void ShowDuelRoom()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("SALA DE DUELO");
            BuildHeader(
                "DUELAR",
                () => RunMainMenuTransition(ShowMainMenu));

            var panel = CreatePanel(
                _screenRoot,
                "Sala",
                new Vector2(0.18f, 0.18f),
                new Vector2(0.82f, 0.82f),
                new Color(0.015f, 0.04f, 0.075f, 0.96f));
            AddOutline(panel.gameObject, new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f), new Vector2(3f, -3f));

            CreateText(
                panel.transform,
                "MODOS DE DUELO",
                42,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.76f),
                new Vector2(0.92f, 0.94f),
                TextAnchor.MiddleCenter);
            var selectedDeck = _repository.SelectedDeck;
            _duelRoomStatus = CreateText(
                panel.transform,
                selectedDeck != null
                    ? $"DECK ATIVO  •  {selectedDeck.displayName}\n" +
                      "Enfrente um bot aleatório ou prepare uma sala multiplayer."
                    : "Escolha um deck válido antes de iniciar o duelo.",
                23,
                FontStyle.Normal,
                selectedDeck != null ? Lime : Gold,
                new Vector2(0.10f, 0.59f),
                new Vector2(0.90f, 0.75f),
                TextAnchor.MiddleCenter);

            CreateButton(
                panel.transform,
                "ENFRENTAR BOT",
                new Vector2(0.14f, 0.44f),
                new Vector2(0.86f, 0.56f),
                Lime,
                ShowCasualBotDeckSelection);
            CreateButton(
                panel.transform,
                "CRIAR SALA PRIVADA",
                new Vector2(0.14f, 0.29f),
                new Vector2(0.86f, 0.41f),
                Cyan,
                () =>
                    ArcaneArenaMultiplayerController.ShowPanel());
            CreateButton(
                panel.transform,
                "ENTRAR COM CÓDIGO",
                new Vector2(0.14f, 0.14f),
                new Vector2(0.86f, 0.26f),
                Blue,
                () =>
                    ArcaneArenaMultiplayerController.ShowPanel(true));
            CreateButton(
                panel.transform,
                "TREINO LOCAL P1 / P2",
                new Vector2(0.28f, 0.025f),
                new Vector2(0.72f, 0.115f),
                Gold,
                StartLocalDuel);
        }

        private void ShowCasualBotDeckSelection()
        {
            _pendingRankedBotDuel = false;
            ShowBotDeckSelection();
        }

        private void ShowRankedBotDeckSelection()
        {
            if (!CanStartWithSelectedDeck())
                return;

            ulong selector = BitConverter.ToUInt64(
                Guid.NewGuid().ToByteArray(), 0);
            int matchmakingSeed = unchecked(
                (int)(selector ^ (selector >> 32)));
            var botRepository = new BotStateRepository();
            BotProfile profile = botRepository.SelectRankedOpponent(
                _repository.CaptureRankSnapshot().rankedPoints,
                matchmakingSeed,
                BotRuntimeSelection.RecentRankedBotIds);
            DeckRecord botDeck = null;
            string deckRejection = string.Empty;
            if (profile != null)
            {
                TryChooseLegalOpponentDeck(
                    _repository.SelectedDeck?.deckId,
                    selector ^ StableTextHash(profile.botId),
                    out botDeck,
                    out deckRejection);
            }
            if (profile == null || botDeck == null)
            {
                Debug.LogError(
                    "[Ranked bot] Não foi possível formar um confronto legal. " +
                    deckRejection);
                _pendingRankedBotDuel = false;
                ShowDuelHub();
                if (_duelRoomStatus != null)
                {
                    _duelRoomStatus.text =
                        "RIVAL IA INDISPONÍVEL\n" +
                        (string.IsNullOrWhiteSpace(deckRejection)
                            ? "Nenhum deck automático válido foi encontrado."
                            : deckRejection);
                    _duelRoomStatus.color = Danger;
                }
                return;
            }

            _pendingRankedBotDuel = true;
            BotRuntimeSelection.RememberRankedOpponent(profile.botId);
            StartBotDuel(botDeck, profile, matchmakingSeed);
        }

        private void ShowBotDeckSelection()
        {
            if (!CanStartWithSelectedDeck())
                return;

            SetDuelPresentation(false);
            ClearScreen();
            Color modeAccent = _pendingRankedBotDuel
                ? DuelRankedAccent
                : DuelOfflineAccent;
            BuildDuelModeBackground(
                _pendingRankedBotDuel
                    ? "RANQUEADO"
                    : "SOLO / OFFLINE",
                modeAccent);
            BuildDuelModeHeader(
                _pendingRankedBotDuel
                    ? "ESCOLHA O RIVAL RANQUEADO"
                    : "ESCOLHA O DECK DO BOT",
                _pendingRankedBotDuel
                    ? "RANQUEADO  •  O RESULTADO ALTERA PE E ELO"
                    : "DUELAR OFFLINE  •  LISTAS TEMÁTICAS COMPLETAS",
                modeAccent,
                () => RunMainMenuTransition(ShowDuelHub));

            Image instruction = CreateDuelModeSurface(
                _screenRoot,
                "Orientação da seleção de rival",
                new Vector2(0.055f, 0.795f),
                new Vector2(0.945f, 0.866f),
                modeAccent,
                true,
                0.78f);
            CreateText(
                instruction.transform,
                _pendingRankedBotDuel
                    ? "SELECIONE UMA IA COMPATÍVEL OU SORTEIE UM RIVAL · O DECK ATIVO SERÁ VALIDADO"
                    : "ESCOLHA UM DECK TEMÁTICO COMPLETO OU SORTEIE UM OPONENTE · MAIN E EXTRA PERMANECEM JUNTOS",
                16,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.12f),
                new Vector2(0.965f, 0.88f),
                TextAnchor.MiddleCenter);

            Image galleryFrame = CreateDuelModeSurface(
                _screenRoot,
                "Galeria de rivais",
                new Vector2(0.040f, 0.055f),
                new Vector2(0.960f, 0.780f),
                modeAccent,
                false,
                0.72f);
            var content = CreateScrollGrid(
                galleryFrame.transform,
                "Decks temáticos do bot",
                new Vector2(0.010f, 0.020f),
                new Vector2(0.990f, 0.985f),
                new Vector2(330f, 265f),
                new Vector2(26f, 24f),
                4);
            TintDuelModeScrollGrid(content, modeAccent);

            IReadOnlyList<DeckRecord> opponentDecks =
                DeckShopCatalog.CreateOpponentRoster();
            CreateRandomBotDeckChoiceTile(
                content,
                opponentDecks.Count);
            for (int index = 0; index < opponentDecks.Count; index++)
            {
                BotProfile profile = DynamicBotCatalog.All[
                    index % DynamicBotCatalog.All.Count];
                CreateBotDeckChoiceTile(content, opponentDecks[index], profile);
            }
        }

        private void CreateRandomBotDeckChoiceTile(
            Transform parent,
            int availableDecks)
        {
            Color accent = _pendingRankedBotDuel
                ? DuelRankedAccent
                : DuelOfflineAccent;
            Image tile = CreateDuelModeSurface(
                parent,
                "Deck temático aleatório do bot",
                Vector2.zero,
                Vector2.one,
                accent,
                true,
                0.94f);
            CreateText(
                tile.transform,
                "RIVAL TEMÁTICO\nALEATÓRIO",
                24,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.51f),
                new Vector2(0.92f, 0.84f),
                TextAnchor.MiddleCenter);
            CreateText(
                tile.transform,
                $"Sorteia 1 de {availableDecks} listas completas.\nMain e Extra Deck permanecem juntos.",
                14,
                FontStyle.Normal,
                Muted,
                new Vector2(0.08f, 0.22f),
                new Vector2(0.92f, 0.49f),
                TextAnchor.MiddleCenter);
            CreateText(
                tile.transform,
                _pendingRankedBotDuel
                    ? "IA DINÂMICA  •  PARTIDA RANQUEADA"
                    : "IA TÁTICA  •  SORTEIO INTEGRAL DE ARQUÉTIPO",
                11,
                FontStyle.Bold,
                accent,
                new Vector2(0.05f, 0.055f),
                new Vector2(0.95f, 0.18f),
                TextAnchor.MiddleCenter);
            AddButtonBehaviour(
                tile,
                StartRandomBotDuel);
        }

        private void CreateBotDeckChoiceTile(
            Transform parent,
            DeckRecord deck,
            BotProfile profile)
        {
            if (deck == null)
                return;

            var valid = DeckRepository.TryValidateForDuel(
                deck,
                _catalog,
                out var rejection);
            Color modeAccent = _pendingRankedBotDuel
                ? DuelRankedAccent
                : DuelOfflineAccent;
            var tile = CreateDuelModeSurface(
                parent,
                $"Deck do bot {deck.deckId}",
                Vector2.zero,
                Vector2.one,
                valid
                    ? modeAccent
                    : Danger,
                true,
                valid ? 0.90f : 0.76f);

            CreateDeckCaseVisual(
                tile.transform,
                deck.caseTheme,
                new Vector2(0.28f, 0.31f),
                new Vector2(0.72f, 0.84f));
            CreateFeaturedCards(
                tile.transform,
                deck,
                new Vector2(0.15f, 0.25f),
                new Vector2(0.85f, 0.88f));
            CreateText(
                tile.transform,
                $"{profile.displayName}\n{deck.displayName}",
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.05f, 0.09f),
                new Vector2(0.95f, 0.25f),
                TextAnchor.MiddleCenter);
            CreateText(
                tile.transform,
                valid
                    ? $"{DynamicBotCatalog.SkillName(profile.skill)} · " +
                      $"{profile.initialRankPoints} PE\n" +
                      $"{deck.mainDeckCardIds.Count} PRINCIPAL  •  {deck.extraDeckCardIds.Count} EXTRA"
                    : rejection,
                valid ? 11 : 10,
                FontStyle.Bold,
                valid ? modeAccent : Danger,
                new Vector2(0.05f, 0.01f),
                new Vector2(0.95f, 0.10f),
                TextAnchor.MiddleCenter);

            if (valid)
            {
                AddButtonBehaviour(
                    tile,
                    () => StartBotDuel(deck, profile));
            }
        }

        private void ShowDeckGallery()
        {
            MainMenuMusicController.SetDeckEditorMode(true);
            SetDuelPresentation(false);
            _selectedDeck = null;
            _editingDeck = null;
            ClearScreen();
            BuildDeckWorkshopGallery();
        }

        private void ShowDeckShop()
        {
            ShowEconomyShop();
        }

        private void ShowLegacyDeckShop()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("LOJA DE DECKS");
            BuildHeader(
                "LOJA DE DECKS",
                () => RunMainMenuTransition(ShowMainMenu));

            CreateText(
                _screenRoot,
                "ESCOLHA UM DECK  •  TODOS SÃO GRATUITOS",
                17,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.08f, 0.835f),
                new Vector2(0.72f, 0.885f),
                TextAnchor.MiddleLeft);
            CreateText(
                _screenRoot,
                string.IsNullOrWhiteSpace(_shopFeedback)
                    ? "Ao usar, o deck entra em Meus Decks e fica selecionado para o próximo duelo."
                    : _shopFeedback,
                15,
                FontStyle.Bold,
                _shopFeedbackIsError ? Danger : Muted,
                new Vector2(0.08f, 0.79f),
                new Vector2(0.94f, 0.84f),
                TextAnchor.MiddleLeft);

            var content = CreateScrollGrid(
                _screenRoot,
                "Produtos da Loja",
                new Vector2(0.055f, 0.075f),
                new Vector2(0.945f, 0.78f),
                new Vector2(500f, 650f),
                new Vector2(30f, 24f),
                3);

            for (var index = 0;
                 index < DeckShopCatalog.Products.Count;
                 index++)
            {
                CreateDeckShopProductTile(
                    content,
                    DeckShopCatalog.Products[index],
                    index);
            }
        }

        private void CreateDeckShopProductTile(
            Transform parent,
            DeckShopProduct product,
            int index)
        {
            var accents = new[] { Cyan, Gold, Danger };
            var accent = accents[
                Mathf.Clamp(index, 0, accents.Length - 1)];
            var tile = CreatePanel(
                parent,
                $"Produto {product.ProductId}",
                Vector2.zero,
                Vector2.one,
                new Color(0.008f, 0.025f, 0.05f, 0.99f));
            AddOutline(
                tile.gameObject,
                new Color(accent.r, accent.g, accent.b, 0.82f),
                new Vector2(3f, -3f));

            var freeBadge = CreatePanel(
                tile.transform,
                "Produto gratuito",
                new Vector2(0.67f, 0.91f),
                new Vector2(0.95f, 0.98f),
                new Color(Lime.r, Lime.g, Lime.b, 0.96f));
            CreateText(
                freeBadge.transform,
                "GRÁTIS",
                14,
                FontStyle.Bold,
                Ink,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);

            CreateText(
                tile.transform,
                product.ArchetypeLabel,
                15,
                FontStyle.Bold,
                accent,
                new Vector2(0.06f, 0.91f),
                new Vector2(0.64f, 0.98f),
                TextAnchor.MiddleLeft);
            CreateText(
                tile.transform,
                product.DisplayName,
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.06f, 0.80f),
                new Vector2(0.94f, 0.91f),
                TextAnchor.MiddleLeft);

            var cover = DeckRepository.ResolveCard(
                _catalog,
                product.CoverCardId);
            CreateCardArtwork(
                tile.transform,
                cover != null ? cover.Artwork : null,
                new Vector2(0.29f, 0.35f),
                new Vector2(0.71f, 0.79f),
                0f,
                true);

            CreateText(
                tile.transform,
                product.Description,
                15,
                FontStyle.Normal,
                Muted,
                new Vector2(0.07f, 0.23f),
                new Vector2(0.93f, 0.34f),
                TextAnchor.UpperCenter);
            CreateText(
                tile.transform,
                $"{product.MainDeckCardIds.Count} PRINCIPAL  •  {product.ExtraDeckCardIds.Count} EXTRA",
                13,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.06f, 0.17f),
                new Vector2(0.94f, 0.23f),
                TextAnchor.MiddleCenter);

            var unlocked =
                _repository.IsDeckProductUnlocked(product.ProductId);
            var selected =
                string.Equals(
                    _repository.State.selectedDeckId,
                    product.DeckId,
                    StringComparison.Ordinal);
            CreateButton(
                tile.transform,
                selected
                    ? "✓  ATIVO NO DUELO"
                    : unlocked
                        ? "USAR ESTE DECK"
                        : "ADICIONAR E USAR",
                new Vector2(0.08f, 0.045f),
                new Vector2(0.92f, 0.15f),
                selected ? Lime : accent,
                () => UseDeckShopProduct(product));
        }

        private void UseDeckShopProduct(DeckShopProduct product)
        {
            if (_repository.TryUseFreeDeckProduct(
                    product.ProductId,
                    out var deck,
                    out var rejection))
            {
                _shopFeedback =
                    $"{deck.displayName} foi adicionado e está ativo para o próximo duelo.";
                _shopFeedbackIsError = false;
            }
            else
            {
                _shopFeedback =
                    $"Não foi possível usar {product.DisplayName}: {rejection}";
                _shopFeedbackIsError = true;
            }

            ShowDeckShop();
        }

        private void CreateNewDeckTile(Transform parent)
        {
            var tile = CreatePanel(
                parent,
                "Criar Deck",
                Vector2.zero,
                Vector2.one,
                new Color(0.015f, 0.03f, 0.05f, 0.98f));
            AddOutline(tile.gameObject, new Color(Lime.r, Lime.g, Lime.b, 0.9f), new Vector2(2f, -2f));
            CreateText(
                tile.transform,
                "+",
                100,
                FontStyle.Normal,
                Lime,
                new Vector2(0.1f, 0.26f),
                new Vector2(0.9f, 0.82f),
                TextAnchor.MiddleCenter);
            CreateText(
                tile.transform,
                "CRIAR NOVO DECK",
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.06f),
                new Vector2(0.92f, 0.25f),
                TextAnchor.MiddleCenter);
            AddButtonBehaviour(
                tile,
                () =>
                {
                    var deck = _repository.CreateDeck(
                        $"Novo Deck {_repository.State.decks.Count + 1}",
                        _repository.State.decks.Count % CaseColors.Length);
                    ShowDeckEditor(deck);
                });
        }

        private void CreateDeckTile(Transform parent, DeckRecord deck)
        {
            var tile = CreatePanel(
                parent,
                $"Deck {deck.deckId}",
                Vector2.zero,
                Vector2.one,
                new Color(0.01f, 0.025f, 0.045f, 0.98f));
            var selected =
                deck.deckId == _repository.State.selectedDeckId;
            AddOutline(
                tile.gameObject,
                selected
                    ? new Color(Lime.r, Lime.g, Lime.b, 0.95f)
                    : new Color(Cyan.r, Cyan.g, Cyan.b, 0.42f),
                selected ? new Vector2(4f, -4f) : new Vector2(2f, -2f));

            CreateDeckCaseVisual(
                tile.transform,
                deck.caseTheme,
                new Vector2(0.28f, 0.27f),
                new Vector2(0.72f, 0.83f));
            if (selected)
            {
                var activeBadge = CreatePanel(
                    tile.transform,
                    "Deck ativo no duelo",
                    new Vector2(0.12f, 0.86f),
                    new Vector2(0.78f, 0.975f),
                    new Color(
                        Lime.r,
                        Lime.g,
                        Lime.b,
                        0.96f));
                CreateText(
                    activeBadge.transform,
                    "✓  ATIVO NO DUELO",
                    13,
                    FontStyle.Bold,
                    Ink,
                    Vector2.zero,
                    Vector2.one,
                    TextAnchor.MiddleCenter);
            }

            var showcase = new GameObject(
                "Três Cartas Principais",
                typeof(RectTransform));
            showcase.transform.SetParent(tile.transform, false);
            Stretch(showcase.GetComponent<RectTransform>());
            CreateFeaturedCards(
                showcase.transform,
                deck,
                new Vector2(0.15f, 0.23f),
                new Vector2(0.85f, 0.88f));
            showcase.SetActive(false);

            CreateText(
                tile.transform,
                deck.displayName,
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.05f, 0.07f),
                new Vector2(0.95f, 0.23f),
                TextAnchor.MiddleCenter);
            CreateText(
                tile.transform,
                $"{deck.mainDeckCardIds.Count} PRINCIPAL  •  {deck.extraDeckCardIds.Count} EXTRA",
                12,
                FontStyle.Bold,
                Muted,
                new Vector2(0.05f, 0.01f),
                new Vector2(0.95f, 0.09f),
                TextAnchor.MiddleCenter);

            var trigger = tile.gameObject.AddComponent<EventTrigger>();
            Image deleteControl = CreateDeckDeleteControl(
                tile.transform,
                deck);
            bool mobileDeleteGesture = Application.isMobilePlatform;
            deleteControl.gameObject.SetActive(false);
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ =>
            {
                if (mobileDeleteGesture)
                    return;
                showcase.SetActive(true);
                deleteControl.gameObject.SetActive(true);
                deleteControl.transform.SetAsLastSibling();
            });
            AddTrigger(trigger, EventTriggerType.PointerExit, _ =>
            {
                if (mobileDeleteGesture)
                    return;
                showcase.SetActive(false);
                deleteControl.gameObject.SetActive(false);
            });
            AddButtonBehaviour(
                tile,
                () => ShowDeckDetails(deck));
            ConfigureMobileDeckDeleteLongPress(tile, deleteControl);
        }

        private void ShowDeckDetails(DeckRecord deck)
        {
            if (deck == null)
                return;

            MainMenuMusicController.SetDeckEditorMode(true);
            SetDuelPresentation(false);
            _selectedDeck = deck;
            ClearScreen();
            BuildDeckWorkshopDetails(deck);
        }

        private void ShowDeckEditor(DeckRecord deck)
        {
            if (deck == null)
                return;

            MainMenuMusicController.SetDeckEditorMode(true);
            SetDuelPresentation(false);
            _editingDeck = deck;
            ClearScreen();
            BuildSharedBackground("EDITOR DE DECK");
            BuildHeader(deck.displayName, () => ShowDeckDetails(deck));
            var deckIsSelected = _repository.IsSelected(deck);
            var deckIsDuelLegal =
                DeckRepository.TryValidateForDuel(
                    deck,
                    _catalog,
                    out _);
            CreateButton(
                _screenRoot,
                deckIsSelected
                    ? deckIsDuelLegal
                        ? "✓ DECK ATIVO"
                        : "⚠ DECK ATIVO"
                    : "USAR NO DUELO",
                new Vector2(0.70f, 0.905f),
                new Vector2(0.83f, 0.972f),
                deckIsSelected
                    ? deckIsDuelLegal ? Lime : Gold
                    : Cyan,
                () =>
                {
                    if (_repository.TrySelectDeck(
                            deck.deckId,
                            out var rejection))
                    {
                        ShowDeckEditor(deck);
                        SetEditorStatus(
                            "Deck selecionado para o próximo duelo.",
                            Lime);
                        return;
                    }

                    SetEditorStatus(rejection, Danger);
                });
            CreateButton(
                _screenRoot,
                "SALVAR",
                new Vector2(0.84f, 0.905f),
                new Vector2(0.955f, 0.972f),
                Lime,
                () =>
                {
                    deck.RefreshFeaturedCards();
                    _repository.Save();
                    if (_editorStatus != null)
                    {
                        if (_repository.IsSelected(deck) &&
                            !DeckRepository.TryValidateForDuel(
                                deck,
                                _catalog,
                                out var rejection))
                        {
                            _editorStatus.text =
                                $"Deck salvo, mas o duelo ficará bloqueado: {rejection}";
                            _editorStatus.color = Danger;
                        }
                        else
                        {
                            _editorStatus.text =
                                "Deck salvo localmente.";
                            _editorStatus.color = Lime;
                        }
                    }
                });

            BuildCraftWalletBar();
            Image detailsPanel = BuildDeckEditorDetailsPanel();

            var deckPanel = CreatePanel(
                _screenRoot,
                "Composição do Deck",
                new Vector2(0.272f, 0.035f),
                new Vector2(0.69f, 0.825f),
                new Color(0.01f, 0.03f, 0.055f, 0.97f));
            Image mainDeckHeader = CreatePanel(
                deckPanel.transform,
                "Cabeçalho do Deck Principal",
                new Vector2(0.02f, 0.91f),
                new Vector2(0.98f, 0.985f),
                new Color(0.018f, 0.105f, 0.085f, 0.96f));
            AddOutline(
                mainDeckHeader.gameObject,
                new Color(DeckEmerald.r, DeckEmerald.g, DeckEmerald.b, 0.55f),
                new Vector2(1f, -1f));
            _mainDeckCountText = CreateText(
                mainDeckHeader.transform,
                $"DECK PRINCIPAL   {deck.mainDeckCardIds.Count}",
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.04f),
                new Vector2(0.67f, 0.96f),
                TextAnchor.MiddleLeft);
            _mainDeckLimitText = CreateText(
                mainDeckHeader.transform,
                $"{MainDeckMinimum}–{MainDeckMaximum} CARTAS" +
                (deckIsSelected
                    ? deckIsDuelLegal ? "  •  ATIVO" : "  •  INVÁLIDO"
                    : string.Empty),
                12,
                FontStyle.Bold,
                deckIsSelected && !deckIsDuelLegal
                    ? Gold
                    : deck.mainDeckCardIds.Count >= MainDeckMinimum
                        ? Lime
                        : Gold,
                new Vector2(0.62f, 0.04f),
                new Vector2(0.975f, 0.96f),
                TextAnchor.MiddleRight);

            var mainCellSize = CalculateResponsiveDeckCellSize(
                MainDeckMaximum,
                MainDeckGridColumns,
                740f,
                493f,
                new Vector2(72f, 105f),
                new Vector2(4f, 4f));
            _mainDeckContent = CreateFixedGrid(
                deckPanel.transform,
                "Cartas do Deck Principal",
                new Vector2(0.02f, 0.345f),
                new Vector2(0.98f, 0.90f),
                mainCellSize,
                new Vector2(4f, 4f),
                MainDeckGridColumns,
                out _mainDropZone);
            PopulateDeckSection(
                _mainDeckContent,
                deck.mainDeckCardIds,
                false);

            Image extraDeckHeader = CreatePanel(
                deckPanel.transform,
                "Cabeçalho do Deck Adicional",
                new Vector2(0.02f, 0.275f),
                new Vector2(0.98f, 0.34f),
                new Color(0.015f, 0.075f, 0.092f, 0.96f));
            AddOutline(
                extraDeckHeader.gameObject,
                new Color(DeckMint.r, DeckMint.g, DeckMint.b, 0.48f),
                new Vector2(1f, -1f));
            _extraDeckCountText = CreateText(
                extraDeckHeader.transform,
                $"DECK ADICIONAL   {deck.extraDeckCardIds.Count} / {ExtraDeckMaximum}",
                17,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.025f, 0.04f),
                new Vector2(0.97f, 0.96f),
                TextAnchor.MiddleLeft);
            var extraCellSize = CalculateResponsiveDeckCellSize(
                deck.extraDeckCardIds.Count,
                10,
                740f,
                212f,
                new Vector2(64f, 93f),
                new Vector2(4f, 4f));
            _extraDeckContent = CreateFixedGrid(
                deckPanel.transform,
                "Cartas do Deck Adicional",
                new Vector2(0.02f, 0.035f),
                new Vector2(0.98f, 0.27f),
                extraCellSize,
                new Vector2(4f, 4f),
                10,
                out _extraDropZone);
            PopulateDeckSection(
                _extraDeckContent,
                deck.extraDeckCardIds,
                true);

            var collectionPanel = CreatePanel(
                _screenRoot,
                "Lista de Cartas",
                new Vector2(0.702f, 0.035f),
                new Vector2(0.985f, 0.825f),
                new Color(0.01f, 0.03f, 0.055f, 0.98f));
            AddOutline(collectionPanel.gameObject, new Color(Lime.r, Lime.g, Lime.b, 0.65f), new Vector2(2f, -2f));

            _catalogAdvancedFilterButton = CreateCatalogControlButton(
                collectionPanel.transform,
                "FILTROS",
                new Vector2(0.03f, 0.925f),
                new Vector2(0.34f, 0.99f),
                Lime,
                ShowAdvancedCatalogFilters);

            CreateCatalogControlButton(
                collectionPanel.transform,
                "A–Z",
                new Vector2(0.37f, 0.925f),
                new Vector2(0.58f, 0.99f),
                Cyan,
                ToggleCatalogSort);
            CreateCatalogControlButton(
                collectionPanel.transform,
                "LIMPAR",
                new Vector2(0.61f, 0.925f),
                new Vector2(0.97f, 0.99f),
                Gold,
                ResetCatalogFilters);

            _catalogSearchInput = CreateSearchField(
                collectionPanel.transform,
                "BUSCAR POR NOME, TIPO OU ID...",
                new Vector2(0.03f, 0.842f),
                new Vector2(0.97f, 0.914f));
            _catalogSearchInput.text = _catalogSearch;
            _catalogSearchInput.onValueChanged.AddListener(value =>
            {
                _catalogSearch = value ?? string.Empty;
                RebuildCatalog();
            });

            _catalogFilterButtons.Clear();
            _catalogFilterButtons[CardCategory.Unknown] =
                CreateCatalogFilterButton(
                    collectionPanel.transform,
                    "TODAS",
                    CardCategory.Unknown,
                    new Vector2(0.03f, 0.765f),
                    new Vector2(0.255f, 0.828f));
            _catalogFilterButtons[CardCategory.Monster] =
                CreateCatalogFilterButton(
                    collectionPanel.transform,
                    "MONSTROS",
                    CardCategory.Monster,
                    new Vector2(0.275f, 0.765f),
                    new Vector2(0.50f, 0.828f));
            _catalogFilterButtons[CardCategory.Spell] =
                CreateCatalogFilterButton(
                    collectionPanel.transform,
                    "MAGIAS",
                    CardCategory.Spell,
                    new Vector2(0.52f, 0.765f),
                    new Vector2(0.745f, 0.828f));
            _catalogFilterButtons[CardCategory.Trap] =
                CreateCatalogFilterButton(
                    collectionPanel.transform,
                    "ARMAD.",
                    CardCategory.Trap,
                    new Vector2(0.765f, 0.765f),
                    new Vector2(0.97f, 0.828f));

            _catalogContent = CreateScrollGrid(
                collectionPanel.transform,
                "Catálogo",
                new Vector2(0.03f, 0.065f),
                new Vector2(0.97f, 0.75f),
                new Vector2(76f, 111f),
                new Vector2(6f, 8f),
                7,
                out _catalogDropZone);
            _catalogContent.localScale = Vector3.one;
            ApplyDeckEditorCatalogScrollbarStyle(_catalogDropZone);
            ConfigureVirtualCatalog();

            _editorStatus = CreateText(
                collectionPanel.transform,
                string.Empty,
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.04f, 0.012f),
                new Vector2(0.96f, 0.058f),
                TextAnchor.MiddleCenter);
            UpdateCatalogFilterVisuals();
            RebuildCatalog();
            ShowInitialDeckEditorCard(deck);
            ApplyEditorLayoutOverrides();
            ApplyDeckWorkshopEditorVisuals(
                deck,
                detailsPanel,
                deckPanel,
                collectionPanel,
                deckIsSelected,
                deckIsDuelLegal);
            FinalizeDeckEditorRequestedLayout();
        }

        private Image BuildDeckEditorDetailsPanel()
        {
            var panel = CreatePanel(
                _screenRoot,
                "Detalhes da Carta",
                new Vector2(0.015f, 0.035f),
                new Vector2(0.26f, 0.825f),
                new Color(0.008f, 0.026f, 0.045f, 0.98f));
            AddOutline(
                panel.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f),
                new Vector2(2f, -2f));

            _deckEditorCardHeader = CreatePanel(
                panel.transform,
                "MOLDURACIMA",
                new Vector2(0.045f, 0.915f),
                new Vector2(0.955f, 0.985f),
                Color.white);
            _deckEditorCardHeader.raycastTarget = false;

            _deckEditorDetailName = CreateText(
                panel.transform,
                "SELECIONE UMA CARTA",
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.045f, 0.915f),
                new Vector2(0.82f, 0.985f),
                TextAnchor.MiddleCenter);
            _deckEditorDetailName.gameObject.name =
                "NOMECARD";

            var artworkFrame = CreatePanel(
                panel.transform,
                "Moldura da Arte",
                new Vector2(0.055f, 0.56f),
                new Vector2(0.58f, 0.875f),
                Color.clear);
            artworkFrame.raycastTarget = false;
            _deckEditorDetailArtwork = CreatePanel(
                artworkFrame.transform,
                "Carta Selecionada",
                new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.975f),
                Color.clear);
            ApplyCapturedRectTransform(
                _deckEditorDetailArtwork.rectTransform,
                new Vector2(0.025f, 0.025f),
                new Vector2(0.975f, 0.975f),
                -43.97125f,
                -15.47515f,
                -34.57125f,
                -54.27515f);
            _deckEditorDetailArtwork.preserveAspect = true;
            _deckEditorDetailArtwork.raycastTarget = true;
            AddOutline(
                _deckEditorDetailArtwork.gameObject,
                new Color(Gold.r, Gold.g, Gold.b, 0.7f),
                new Vector2(2f, -2f));
            var artworkButton =
                _deckEditorDetailArtwork.gameObject.AddComponent<Button>();
            artworkButton.targetGraphic = _deckEditorDetailArtwork;
            artworkButton.transition = Selectable.Transition.ColorTint;
            artworkButton.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                OpenDeckEditorZoom();
            });

            BuildDeckEditorCombatInformation(panel.transform);
            BuildDeckEditorRelatedCardsButton(panel.transform);

            var effectHeader = CreatePanel(
                panel.transform,
                "Cabeçalho do Efeito",
                new Vector2(0.035f, 0.435f),
                new Vector2(0.965f, 0.48f),
                new Color(0.08f, 0.19f, 0.28f, 0.98f));
            _deckEditorEffectHeader = effectHeader;
            CreateText(
                effectHeader.transform,
                "DESCRIÇÃO / EFEITO",
                14,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.04f, 0.03f),
                new Vector2(0.96f, 0.97f),
                TextAnchor.MiddleLeft);

            _deckEditorDetailEffect = CreateScrollableText(
                panel.transform,
                "Texto da Carta",
                new Vector2(0.035f, 0.115f),
                new Vector2(0.965f, 0.425f),
                21,
                0.1158185f,
                true);
            BuildDeckEditorCraftActions(panel.transform);
            BuildDeckEditorZoomViewer();
            return panel;
        }

        private void BuildDeckEditorZoomViewer()
        {
            var overlay = CreatePanel(
                _screenRoot,
                "Visualizador Ampliado do Editor",
                Vector2.zero,
                Vector2.one,
                new Color(0.002f, 0.008f, 0.018f, 0.988f));
            _deckEditorZoomOverlay = overlay.gameObject;

            var artworkObject = new GameObject(
                "Carta em Tela Cheia",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            artworkObject.transform.SetParent(overlay.transform, false);
            var artworkRect =
                artworkObject.GetComponent<RectTransform>();
            artworkRect.anchorMin = new Vector2(0.035f, 0.07f);
            artworkRect.anchorMax = new Vector2(0.965f, 0.94f);
            artworkRect.offsetMin = Vector2.zero;
            artworkRect.offsetMax = Vector2.zero;
            artworkRect.pivot = new Vector2(0.5f, 0.5f);

            _deckEditorZoomArtwork = artworkObject.GetComponent<Image>();
            _deckEditorZoomArtwork.preserveAspect = true;
            _deckEditorZoomArtwork.raycastTarget = true;
            AddOutline(
                artworkObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f),
                new Vector2(3f, -3f));

            CreateButton(
                overlay.transform,
                "FECHAR",
                new Vector2(0.875f, 0.925f),
                new Vector2(0.975f, 0.982f),
                Danger,
                CloseDeckEditorZoom);
            CreateText(
                overlay.transform,
                "SCROLL OU PINCA PARA AMPLIAR  |  ARRASTE PARA MOVER  |  DUPLO CLIQUE PARA RESETAR",
                14,
                FontStyle.Bold,
                new Color(0.68f, 0.9f, 0.96f, 0.9f),
                new Vector2(0.12f, 0.012f),
                new Vector2(0.88f, 0.06f),
                TextAnchor.MiddleCenter);

            _deckEditorZoomViewer =
                overlay.gameObject.AddComponent<ArcaneArena.CardZoomViewer>();
            _deckEditorZoomViewer.Setup(artworkRect);
            _deckEditorZoomOverlay.SetActive(false);
        }

        private void OpenDeckEditorZoom()
        {
            if (_deckEditorDetailArtwork == null ||
                _deckEditorDetailArtwork.sprite == null ||
                _deckEditorZoomOverlay == null ||
                _deckEditorZoomArtwork == null ||
                _deckEditorZoomViewer == null)
            {
                return;
            }

            _deckEditorZoomArtwork.sprite =
                _deckEditorDetailArtwork.sprite;
            _deckEditorZoomOverlay.SetActive(true);
            _deckEditorZoomOverlay.transform.SetAsLastSibling();
            _deckEditorZoomViewer.ResetView();
        }

        private void CloseDeckEditorZoom()
        {
            if (_deckEditorZoomOverlay != null)
                _deckEditorZoomOverlay.SetActive(false);
        }

        private void ShowInitialDeckEditorCard(DeckRecord deck)
        {
            if (!string.IsNullOrWhiteSpace(_deckEditorSelectedCardId) &&
                DeckRepository.ResolveCard(
                    _catalog,
                    _deckEditorSelectedCardId) != null)
            {
                ShowDeckEditorCardDetails(_deckEditorSelectedCardId);
                return;
            }

            string firstId = null;
            if (deck.mainDeckCardIds != null &&
                deck.mainDeckCardIds.Count > 0)
            {
                firstId = deck.mainDeckCardIds[0];
            }
            else if (deck.extraDeckCardIds != null &&
                     deck.extraDeckCardIds.Count > 0)
            {
                firstId = deck.extraDeckCardIds[0];
            }
            else
            {
                var entries = ReadyCatalogEntries();
                if (entries.Count > 0)
                    firstId = DeckRepository.StableCardId(entries[0]);
            }

            if (!string.IsNullOrWhiteSpace(firstId))
                ShowDeckEditorCardDetails(firstId);
        }

        public void ShowDeckEditorCardDetails(string cardId)
        {
            var entry = DeckRepository.ResolveCard(_catalog, cardId);
            if (entry == null)
                return;

            _deckEditorSelectedCardId = cardId;
            if (_deckEditorDetailArtwork != null)
            {
                _deckEditorDetailArtwork.sprite = entry.Artwork;
                _deckEditorDetailArtwork.color =
                    entry.Artwork != null ? Color.white : Color.clear;
                RefreshBanlistBadge(
                    _deckEditorDetailArtwork.transform,
                    cardId,
                    true);
            }
            if (_deckEditorDetailName != null)
                _deckEditorDetailName.text = entry.DisplayName;
            ApplyDeckEditorCardTheme(entry);

            RefreshDeckEditorCombatInformation(entry);
            if (_deckEditorDetailEffect != null)
            {
                _deckEditorDetailEffect.text = CardPresentationText.EffectPtBr(
                    entry,
                    "Descrição ainda não cadastrada para esta carta.");
                var scroll =
                    _deckEditorDetailEffect.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                    scroll.verticalNormalizedPosition = 1f;
            }
            RefreshDeckEditorCraftDetails(entry, cardId);
        }

        private static string FormatCardStat(int value)
        {
            return value >= 0 ? value.ToString() : "—";
        }

        private void PopulateDeckSection(
            Transform parent,
            List<string> cardIds,
            bool extraDeck)
        {
            if (cardIds == null)
                return;

            for (var i = 0; i < cardIds.Count; i++)
            {
                var cardId = cardIds[i];
                var entry = DeckRepository.ResolveCard(_catalog, cardId);
                if (entry == null || entry.Artwork == null)
                    continue;

                Image slot = CreatePanel(
                    parent,
                    $"Célula da carta {cardId}",
                    Vector2.zero,
                    Vector2.one,
                    Color.clear);
                slot.raycastTarget = false;
                var card = CreateCardArtwork(
                    slot.transform,
                    entry.Artwork,
                    Vector2.zero,
                    Vector2.one,
                    0f,
                    false);
                AddBanlistBadge(card.transform, cardId);
                AddCardRarityBadge(slot.transform, entry);
                var removeIndex = i;
                card.gameObject.AddComponent<DeckEditorDeckCardDrag>()
                    .Setup(
                        this,
                        cardId,
                        entry.Artwork,
                        extraDeck,
                        removeIndex);
            }
        }

        private void ApplyDeckEditorCardTheme(
            CardCatalogEntry entry)
        {
            if (entry == null)
                return;

            var tint = Color.white;
            if (entry.Category == CardCategory.Spell)
            {
                tint = Hex("#39D4EE");
            }
            else if (entry.Category == CardCategory.Trap)
            {
                tint = Hex("#D064A5");
            }
            else if (entry.Category == CardCategory.Monster)
            {
                switch (entry.MonsterFrame)
                {
                    case MonsterFrameKind.Normal:
                        tint = Hex("#E1B85B");
                        break;
                    case MonsterFrameKind.Effect:
                        tint = Hex("#D88943");
                        break;
                    case MonsterFrameKind.Ritual:
                        tint = Hex("#65AFC9");
                        break;
                    case MonsterFrameKind.Fusion:
                        tint = Hex("#9B72BE");
                        break;
                    case MonsterFrameKind.Synchro:
                        tint = Hex("#E6EDF0");
                        break;
                    case MonsterFrameKind.Xyz:
                        tint = Hex("#646672");
                        break;
                    case MonsterFrameKind.Link:
                        tint = Hex("#4C87C6");
                        break;
                    case MonsterFrameKind.Pendulum:
                        tint = Hex("#61B78F");
                        break;
                    case MonsterFrameKind.Token:
                        tint = Hex("#A8AEB4");
                        break;
                    default:
                        tint = Hex("#D88943");
                        break;
                }
            }

            Color professionalTint = Color.Lerp(
                new Color(0.008f, 0.035f, 0.032f, 1f),
                tint,
                0.58f);
            if (_deckEditorCardHeader != null)
                _deckEditorCardHeader.color = professionalTint;
            if (_deckEditorEffectHeader != null)
                _deckEditorEffectHeader.color = professionalTint;
        }

        private void RebuildCatalog()
        {
            RebuildVirtualCatalog();
        }

        private void SetCatalogFilter(CardCategory category)
        {
            _catalogFilter = category;
            UpdateCatalogFilterVisuals();
            RebuildCatalog();
        }

        private void ToggleCatalogSort()
        {
            _catalogSortDescending = !_catalogSortDescending;
            RebuildCatalog();
        }

        private void ResetCatalogFilters()
        {
            _catalogSearch = string.Empty;
            _catalogFilter = CardCategory.Unknown;
            _catalogSortDescending = false;
            ClearAdvancedCatalogFilterState();
            if (_catalogSearchInput != null)
                _catalogSearchInput.SetTextWithoutNotify(string.Empty);
            UpdateCatalogFilterVisuals();
            RebuildCatalog();
        }

        private void UpdateCatalogFilterVisuals()
        {
            foreach (var pair in _catalogFilterButtons)
            {
                if (pair.Value == null)
                    continue;
                Color accent = pair.Key == CardCategory.Monster
                    ? Gold
                    : pair.Key == CardCategory.Spell
                        ? Cyan
                        : pair.Key == CardCategory.Trap
                            ? new Color(0.95f, 0.28f, 0.7f)
                            : Lime;
                StyleCatalogControlButton(
                    pair.Value,
                    accent,
                    pair.Key == _catalogFilter);
            }
        }

        private Image CreateCatalogFilterButton(
            Transform parent,
            string label,
            CardCategory category,
            Vector2 min,
            Vector2 max)
        {
            return CreateCatalogControlButton(
                parent,
                label,
                min,
                max,
                category == CardCategory.Monster
                    ? Gold
                    : category == CardCategory.Spell
                        ? Cyan
                        : category == CardCategory.Trap
                            ? new Color(0.95f, 0.28f, 0.7f)
                            : Lime,
                () => SetCatalogFilter(category));
        }

        public void BeginCatalogCardDrag(
            string cardId,
            Sprite sprite,
            Vector2 screenPosition)
        {
            if (_dragGhost != null)
                Destroy(_dragGhost.gameObject);

            var ghost = new GameObject(
                "Carta Arrastada",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            ghost.transform.SetParent(_canvas.transform, false);
            _dragGhost = ghost.GetComponent<RectTransform>();
            _dragGhost.sizeDelta = new Vector2(96f, 140f);
            _dragGhost.pivot = new Vector2(0.5f, 0.5f);
            var image = ghost.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            ghost.GetComponent<CanvasGroup>().alpha = 0.93f;
            AddOutline(ghost, Cyan, new Vector2(4f, -4f));
            MoveCatalogCardDrag(screenPosition);
        }

        public void BeginDeckCardDrag(
            Sprite sprite,
            Vector2 screenPosition)
        {
            BeginCatalogCardDrag(string.Empty, sprite, screenPosition);
        }

        public void MoveCatalogCardDrag(Vector2 screenPosition)
        {
            if (_dragGhost == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPosition,
                    null,
                    out var localPoint))
            {
                _dragGhost.anchoredPosition = localPoint;
            }
        }

        public void EndCatalogCardDrag(
            string cardId,
            Vector2 screenPosition)
        {
            if (_dragGhost != null)
                Destroy(_dragGhost.gameObject);
            _dragGhost = null;

            if (_mainDropZone != null &&
                RectTransformUtility.RectangleContainsScreenPoint(
                    _mainDropZone,
                    screenPosition))
            {
                Vector2 destination = RectScreenCenter(_mainDropZone);
                var entry = DeckRepository.ResolveCard(_catalog, cardId);
                if (TryAddCardToDeck(cardId, false))
                {
                    PlayDeckEditorCardTransfer(
                        entry != null ? entry.Artwork : null,
                        screenPosition,
                        destination,
                        false);
                }
            }
            else if (_extraDropZone != null &&
                     RectTransformUtility.RectangleContainsScreenPoint(
                         _extraDropZone,
                         screenPosition))
            {
                Vector2 destination = RectScreenCenter(_extraDropZone);
                var entry = DeckRepository.ResolveCard(_catalog, cardId);
                if (TryAddCardToDeck(cardId, true))
                {
                    PlayDeckEditorCardTransfer(
                        entry != null ? entry.Artwork : null,
                        screenPosition,
                        destination,
                        false);
                }
            }
            else if (_editorStatus != null)
            {
                _editorStatus.text =
                    "Solte a carta dentro do Deck Principal ou Adicional.";
                _editorStatus.color = Gold;
            }
        }

        public void QuickAddCatalogCard(
            string cardId,
            RectTransform sourceRect)
        {
            var entry = DeckRepository.ResolveCard(_catalog, cardId);
            bool extraDeck = DeckRepository.BelongsToExtraDeck(entry);
            RectTransform destination = extraDeck
                ? _extraDropZone
                : _mainDropZone;
            Vector2 start = RectScreenCenter(sourceRect);
            Vector2 end = RectScreenCenter(destination);
            if (TryAddCardToDeck(cardId, extraDeck))
            {
                PlayDeckEditorCardTransfer(
                    entry != null ? entry.Artwork : null,
                    start,
                    end,
                    false);
            }
        }

        public void NotifyLockedCatalogCard(string cardId)
        {
            var entry = DeckRepository.ResolveCard(
                _catalog,
                cardId);
            SetEditorStatus(
                $"{(entry != null ? entry.DisplayName : "Esta carta")} não foi obtida. Selecione-a e use GERAR para criá-la.",
                Danger);
        }

        private bool TryAddCardToDeck(string cardId, bool targetExtraDeck)
        {
            if (_editingDeck == null)
                return false;

            var entry = DeckRepository.ResolveCard(_catalog, cardId);
            if (entry == null)
            {
                SetEditorStatus("Carta não encontrada no catálogo.", Danger);
                return false;
            }

            var belongsToExtra = DeckRepository.BelongsToExtraDeck(entry);
            if (belongsToExtra != targetExtraDeck)
            {
                SetEditorStatus(
                    belongsToExtra
                        ? "Esta carta pertence ao Deck Adicional."
                        : "Esta carta pertence ao Deck Principal.",
                    Gold);
                return false;
            }

            var totalCopies =
                CountCopies(_editingDeck.mainDeckCardIds, cardId) +
                CountCopies(_editingDeck.extraDeckCardIds, cardId);
            var ownedCopies = DeckEditorOwnedCopies(entry, cardId);
            if (totalCopies >= ownedCopies)
            {
                SetEditorStatus(
                    ownedCopies == 0
                        ? $"{entry.DisplayName} ainda não foi adquirida na Loja de Decks."
                        : $"Você possui {ownedCopies} cópia(s) de {entry.DisplayName}.",
                    Danger);
                return false;
            }
            if (totalCopies >= CopyLimit)
            {
                SetEditorStatus(
                    $"Limite de {CopyLimit} cópias atingido.",
                    Danger);
                return false;
            }

            var target = targetExtraDeck
                ? _editingDeck.extraDeckCardIds
                : _editingDeck.mainDeckCardIds;
            var maximum = targetExtraDeck
                ? ExtraDeckMaximum
                : MainDeckMaximum;
            if (target.Count >= maximum)
            {
                SetEditorStatus(
                    targetExtraDeck
                        ? "O Deck Adicional já possui 15 cartas."
                        : "O Deck Principal já possui 60 cartas.",
                    Danger);
                return false;
            }

            var lastMatchingCopy =
                target.FindLastIndex(existingId =>
                    string.Equals(
                        existingId,
                        cardId,
                        StringComparison.OrdinalIgnoreCase));
            if (lastMatchingCopy >= 0)
            {
                target.Insert(
                    lastMatchingCopy + 1,
                    cardId);
            }
            else
            {
                target.Add(cardId);
            }
            _editingDeck.RefreshFeaturedCards();
            _repository.Save();
            SetEditorStatus(
                $"{entry.DisplayName} foi adicionada ao " +
                (targetExtraDeck ? "Deck Adicional." : "Deck Principal."),
                Lime);
            QueueEditorRefresh();
            return true;
        }

        public void EndDeckCardDrag(
            bool extraDeck,
            int index,
            Sprite sprite,
            Vector2 screenPosition)
        {
            if (_dragGhost != null)
                Destroy(_dragGhost.gameObject);
            _dragGhost = null;

            if (_catalogDropZone == null ||
                !RectTransformUtility.RectangleContainsScreenPoint(
                    _catalogDropZone,
                    screenPosition))
            {
                SetEditorStatus(
                    "Arraste a carta até o catálogo para removê-la do deck.",
                    Gold);
                return;
            }

            Vector2 destination = RectScreenCenter(_catalogDropZone);
            if (RemoveCardFromDeck(extraDeck, index))
            {
                PlayDeckEditorCardTransfer(
                    sprite,
                    screenPosition,
                    destination,
                    true);
            }
        }

        public void QuickRemoveDeckCard(
            bool extraDeck,
            int index,
            Sprite sprite,
            RectTransform sourceRect)
        {
            Vector2 start = RectScreenCenter(sourceRect);
            Vector2 end = RectScreenCenter(_catalogDropZone);
            if (RemoveCardFromDeck(extraDeck, index))
            {
                PlayDeckEditorCardTransfer(
                    sprite,
                    start,
                    end,
                    true);
            }
        }

        private bool RemoveCardFromDeck(bool extraDeck, int index)
        {
            if (_editingDeck == null)
                return false;

            var source = extraDeck
                ? _editingDeck.extraDeckCardIds
                : _editingDeck.mainDeckCardIds;
            if (index < 0 || index >= source.Count)
                return false;

            string removedCardId = source[index];
            CardCatalogEntry removedEntry = DeckRepository.ResolveCard(
                _catalog,
                removedCardId);
            source.RemoveAt(index);
            _editingDeck.RefreshFeaturedCards();
            _repository.Save();
            SetEditorStatus(
                $"{(removedEntry != null ? removedEntry.DisplayName : "Carta")} foi removida do deck.",
                Lime);
            QueueEditorRefresh();
            return true;
        }

        private static Vector2 RectScreenCenter(RectTransform rect)
        {
            if (rect == null)
                return Vector2.zero;
            return RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center));
        }

        private void PlayDeckEditorCardTransfer(
            Sprite sprite,
            Vector2 startScreen,
            Vector2 endScreen,
            bool removing)
        {
            if (sprite == null || _canvasRect == null)
                return;
            StartCoroutine(
                AnimateDeckEditorCardTransfer(
                    sprite,
                    startScreen,
                    endScreen,
                    removing));
        }

        private IEnumerator AnimateDeckEditorCardTransfer(
            Sprite sprite,
            Vector2 startScreen,
            Vector2 endScreen,
            bool removing)
        {
            var transfer = new GameObject(
                removing ? "Carta Removida" : "Carta Adicionada",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            transfer.transform.SetParent(_canvas.transform, false);
            transfer.transform.SetAsLastSibling();
            RectTransform rect = transfer.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(110f, 160f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = transfer.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            CanvasGroup group = transfer.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            AddOutline(
                transfer,
                removing ? Gold : Cyan,
                new Vector2(3f, -3f));

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                startScreen,
                null,
                out Vector2 start);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                endScreen,
                null,
                out Vector2 end);

            const float duration = 0.20f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float linear = Mathf.Clamp01(elapsed / duration);
                float eased = linear * linear * (3f - 2f * linear);
                rect.anchoredPosition = Vector2.LerpUnclamped(
                    start,
                    end,
                    eased);
                float pulse = Mathf.Sin(linear * Mathf.PI) * 0.12f;
                float finalScale = removing ? 0.58f : 0.72f;
                float scale = Mathf.Lerp(1f, finalScale, eased) + pulse;
                rect.localScale = Vector3.one * scale;
                group.alpha = 1f - Mathf.Clamp01((linear - 0.62f) / 0.38f);
                yield return null;
            }

            Destroy(transfer);
        }

        private void QueueEditorRefresh()
        {
            if (_editorRefreshQueued)
                return;
            _editorRefreshQueued = true;
            StartCoroutine(RefreshEditorNextFrame());
        }

        private IEnumerator RefreshEditorNextFrame()
        {
            yield return null;
            _editorRefreshQueued = false;
            RefreshDeckEditorComposition();
        }

        private void RefreshDeckEditorComposition()
        {
            if (_editingDeck == null ||
                _mainDeckContent == null ||
                _extraDeckContent == null)
            {
                return;
            }

            RebuildDeckEditorSection(
                _mainDeckContent,
                _editingDeck.mainDeckCardIds,
                false);
            RebuildDeckEditorSection(
                _extraDeckContent,
                _editingDeck.extraDeckCardIds,
                true);

            if (_mainDeckCountText != null)
            {
                _mainDeckCountText.text =
                    $"DECK PRINCIPAL   {_editingDeck.mainDeckCardIds.Count}";
            }
            if (_mainDeckLimitText != null)
            {
                _mainDeckLimitText.text =
                    $"{MainDeckMinimum}–{MainDeckMaximum} CARTAS";
                _mainDeckLimitText.color =
                    _editingDeck.mainDeckCardIds.Count >= MainDeckMinimum
                        ? Lime
                        : Gold;
            }
            if (_extraDeckCountText != null)
            {
                _extraDeckCountText.text =
                    $"DECK ADICIONAL   {_editingDeck.extraDeckCardIds.Count} / " +
                    ExtraDeckMaximum;
            }
        }

        private void RebuildDeckEditorSection(
            RectTransform content,
            List<string> cardIds,
            bool extraDeck)
        {
            for (int index = content.childCount - 1; index >= 0; index--)
            {
                Transform child = content.GetChild(index);
                child.SetParent(null, false);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = extraDeck
                    ? CalculateResponsiveDeckCellSize(
                        cardIds.Count,
                        10,
                        740f,
                        212f,
                        new Vector2(64f, 93f),
                        new Vector2(4f, 4f))
                    : CalculateResponsiveDeckCellSize(
                        MainDeckMaximum,
                        MainDeckGridColumns,
                        740f,
                        493f,
                        new Vector2(72f, 105f),
                        new Vector2(4f, 4f));
            }

            PopulateDeckSection(content, cardIds, extraDeck);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void SetEditorStatus(string message, Color color)
        {
            if (_editorStatus == null)
                return;
            _editorStatus.text = message;
            _editorStatus.color = color;
        }

        private void StartLocalDuel()
        {
            if (!CanStartWithSelectedDeck())
                return;
            _pendingBotLoadout = null;
            OpenDuelArenaScene(PendingDuelMode.LocalTest);
        }

        private void StartRandomBotDuel()
        {
            ulong selector = BitConverter.ToUInt64(
                Guid.NewGuid().ToByteArray(),
                0);
            DeckRecord botDeck = DeckShopCatalog.ChooseOpponentDeck(
                _repository.SelectedDeck?.deckId,
                selector);
            if (botDeck == null)
            {
                ShowBotDeckSelection();
                return;
            }
            int profileIndex = (int)(selector %
                (ulong)DynamicBotCatalog.All.Count);
            StartBotDuel(botDeck, DynamicBotCatalog.All[profileIndex]);
        }

        private bool TryChooseLegalOpponentDeck(
            string playerDeckId,
            ulong selector,
            out DeckRecord selected,
            out string rejection)
        {
            selected = null;
            rejection = "Nenhum deck temático do bot está apto para duelo.";
            IReadOnlyList<DeckRecord> roster =
                DeckShopCatalog.CreateOpponentRoster();
            var candidates = new List<DeckRecord>(roster.Count);
            for (int index = 0; index < roster.Count; index++)
            {
                DeckRecord deck = roster[index];
                if (deck != null &&
                    !string.Equals(
                        deck.deckId,
                        playerDeckId,
                        StringComparison.Ordinal))
                {
                    candidates.Add(deck);
                }
            }
            if (candidates.Count == 0)
            {
                for (int index = 0; index < roster.Count; index++)
                {
                    if (roster[index] != null)
                        candidates.Add(roster[index]);
                }
            }
            if (candidates.Count == 0) return false;

            int first = (int)(selector % (ulong)candidates.Count);
            for (int offset = 0; offset < candidates.Count; offset++)
            {
                DeckRecord candidate = candidates[
                    (first + offset) % candidates.Count];
                if (DeckRepository.TryValidateForDuel(
                        candidate,
                        _catalog,
                        out string candidateRejection))
                {
                    selected = candidate;
                    rejection = string.Empty;
                    return true;
                }
                rejection = candidateRejection;
            }
            return false;
        }

        private void StartBotDuel(
            DeckRecord botDeck,
            BotProfile botProfile = null,
            int? requestedDecisionSeed = null)
        {
            if (!CanStartWithSelectedDeck())
                return;
            bool rankedRequest = _pendingRankedBotDuel;
            if (!DeckRepository.TryValidateForDuel(
                    botDeck,
                    _catalog,
                    out var rejection))
            {
                _pendingRankedBotDuel = false;
                if (rankedRequest)
                {
                    ShowDuelHub();
                }
                else
                {
                    ShowBotDeckSelection();
                }
                if (_duelRoomStatus != null)
                {
                    _duelRoomStatus.text =
                        $"DECK DO BOT INVÁLIDO\n{rejection}";
                    _duelRoomStatus.color = Danger;
                }
                return;
            }

            // O snapshot contém somente IDs estáveis. O bot e o jogador
            // recebem cópias independentes; nenhuma lista ou ordem de Deck
            // é compartilhada entre os lados.
            botProfile ??= DynamicBotCatalog.Find("BOT_017");
            string botStableId = "bot:" +
                (botProfile?.botId ?? "BOT_017");
            _pendingBotLoadout = DuelDeckLoadout.Create(
                botStableId,
                botDeck,
                botProfile?.displayName ?? "OPONENTE IA");
            _pendingBotLoadout.identity = new DuelIdentitySnapshot
            {
                stablePlayerId = botStableId,
                nickname = botProfile?.displayName ?? "OPONENTE IA",
                equippedIconId = ProfileIconCatalog.ResolveForStableIdentity(
                    botStableId),
                rankTier = RankRules.ResolveTier(
                    botProfile?.initialRankPoints ?? 0),
                rankedPoints = RankRules.ClampPoints(
                    botProfile?.initialRankPoints ?? 0),
                cosmeticsCatalogVersion = ProfileIconCatalog.CatalogVersion
            };
            int decisionSeed = requestedDecisionSeed ?? unchecked(
                (int)BitConverter.ToUInt64(
                    Guid.NewGuid().ToByteArray(), 0));
            BotRuntimeSelection.Select(botProfile.botId, decisionSeed);
            var botRepository = new BotStateRepository();
            botRepository.GetOrCreate(botProfile);
            if (_pendingRankedBotDuel)
            {
                RankPlayerSnapshot playerSnapshot =
                    _repository.CaptureRankSnapshot();
                RankPlayerSnapshot botSnapshot =
                    botRepository.CaptureRankSnapshot(botProfile);
                _activeRankedBotMatch = new RankedMatchSnapshot
                {
                    matchId = "bot-" + Guid.NewGuid().ToString("N"),
                    policy = CompetitivePolicy.Ranked,
                    source = CompetitiveMatchSource.Matchmaking,
                    rulesVersion = RankRules.RulesVersion,
                    rulesHash = RankRules.RulesHash,
                    sealedAtUtcTicks = DateTime.UtcNow.Ticks,
                    seat0 = playerSnapshot,
                    seat1 = botSnapshot
                };
                _activeRankedBotProfile = botProfile;
                _activeRankedBotResultCommitted = false;
                _activeDuelStatisticsId = _activeRankedBotMatch.matchId;
                _activeDuelStatisticsRanked = true;
            }
            else
            {
                _activeRankedBotMatch = null;
                _activeRankedBotProfile = null;
                _activeRankedBotResultCommitted = false;
                _activeDuelStatisticsId =
                    "bot-" + Guid.NewGuid().ToString("N");
                _activeDuelStatisticsRanked = false;
            }
            _pendingRankedBotDuel = false;
            OpenDuelArenaScene(PendingDuelMode.Bot);
        }

        private static ulong StableTextHash(string text)
        {
            unchecked
            {
                ulong value = 14695981039346656037UL;
                string source = text ?? string.Empty;
                for (int index = 0; index < source.Length; index++)
                {
                    value ^= source[index];
                    value *= 1099511628211UL;
                }
                return value;
            }
        }

        public RankChangeReceipt CompleteActiveBotDuel(
            byte winner,
            long damageDealt = 0,
            long damageReceived = 0)
        {
            if (_repository != null &&
                !string.IsNullOrWhiteSpace(_activeDuelStatisticsId))
            {
                _repository.TryRecordAuthoritativeDuelResult(
                    "result:" + _activeDuelStatisticsId,
                    winner == 0,
                    winner > 1,
                    false,
                    _activeDuelStatisticsRanked,
                    damageDealt,
                    damageReceived,
                    out string statisticRejection);
                if (!string.IsNullOrWhiteSpace(statisticRejection))
                    Debug.LogWarning("[Profile statistics] " + statisticRejection);
            }
            if (_activeRankedBotResultCommitted ||
                _activeRankedBotMatch == null ||
                _activeRankedBotProfile == null)
            {
                return null;
            }

            RankedOutcome playerOutcome = winner > 1
                ? RankedOutcome.Draw
                : winner == 0 ? RankedOutcome.Win : RankedOutcome.Loss;
            RankedOutcome botOutcome = winner > 1
                ? RankedOutcome.Draw
                : winner == 1 ? RankedOutcome.Win : RankedOutcome.Loss;
            if (!RankPointService.TryCreateReceipt(
                    _activeRankedBotMatch, 0, playerOutcome,
                    out RankChangeReceipt playerReceipt,
                    out string playerRejection) ||
                !_repository.TryCommitRankReceipt(
                    playerReceipt, out RankChangeReceipt committedPlayerReceipt,
                    out playerRejection))
            {
                Debug.LogError(
                    "[Ranked bot] Resultado local rejeitado: " +
                    playerRejection);
                return null;
            }
            if (!RankPointService.TryCreateReceipt(
                    _activeRankedBotMatch, 1, botOutcome,
                    out RankChangeReceipt botReceipt,
                    out string botRejection) ||
                !new BotStateRepository().TryCommitRankReceipt(
                    _activeRankedBotProfile, botReceipt,
                    out botRejection))
            {
                Debug.LogError(
                    "[Ranked bot] Resultado do bot rejeitado: " +
                    botRejection);
                return committedPlayerReceipt;
            }

            _activeRankedBotResultCommitted = true;
            Debug.Log(
                $"[Ranked bot] Partida {_activeRankedBotMatch.matchId} " +
                $"confirmada. Jogador {playerReceipt.oldPoints} -> " +
                $"{playerReceipt.newPoints} PE; " +
                $"{_activeRankedBotProfile.displayName} " +
                $"{botReceipt.oldPoints} -> {botReceipt.newPoints} PE.");
            return committedPlayerReceipt;
        }

        public bool TryGetSelectedDuelLoadout(
            out DuelDeckLoadout loadout,
            out string rejection)
        {
            loadout = null;
            rejection = string.Empty;
            if (_repository == null)
            {
                ResolveProjectReferences();
                _repository = new DeckRepository();
                _repository.Load(_catalog);
                InitializeCoinRewardAuthorization();
            }

            return _repository.TryCreateSelectedLoadout(
                out loadout,
                out rejection);
        }

        private bool CanStartWithSelectedDeck()
        {
            if (_repository.TryCreateSelectedLoadout(
                    out var loadout,
                    out var rejection))
            {
                if (_duelRoomStatus != null)
                {
                    _duelRoomStatus.text =
                        $"DECK CONFIRMADO  •  {loadout.displayName}";
                    _duelRoomStatus.color = Lime;
                }
                return true;
            }

            if (_duelRoomStatus != null)
            {
                _duelRoomStatus.text =
                    $"DUELO BLOQUEADO\n{rejection}";
                _duelRoomStatus.color = Danger;
            }
            return false;
        }

        private void OpenDeckEditorScene()
        {
            if (IsActiveScene(DeckEditorSceneName) ||
                !Application.CanStreamedLevelBeLoaded(DeckEditorSceneName))
            {
                ShowDeckGallery();
                return;
            }

            OnlineLoadingScreenPresenter presenter = DuelOnlineSession
                .EnsureInstance()
                ?.TransitionPresenter;
            if (presenter == null)
            {
                SceneManager.LoadScene(DeckEditorSceneName);
                return;
            }

            presenter.ShowSceneLoading(
                "CARREGANDO EDITOR DE DECKS",
                "Preparando suas cartas e ferramentas de edicao.",
                LoadingCardMotionStyle.DeckFan,
                DeckEditorSceneName);
        }

        private void ReturnToMainMenuScene()
        {
            RunMainMenuTransition(ReturnToMainMenuSceneImmediate);
        }

        private void ExitDuelPresentationToMenu()
        {
            if (StoryRogueliteRuntime.IsStoryDuel)
                StoryRogueliteRuntime.ForfeitActiveDuel();
            ReturnToMainMenuScene();
        }

        public void ReturnToMenuAfterOfflineDuel()
        {
            if (StoryRogueliteRuntime.IsStoryDuel)
                StoryRogueliteRuntime.RequestReturnToStory();
            ReturnToMainMenuScene();
        }

        private void ReturnToMainMenuSceneImmediate()
        {
            if (IsActiveScene(DeckEditorSceneName))
                _repository?.ClearPendingDeckEditorNewCards();
            if (_duelPresentationVisible &&
                DuelOnlineSession.Instance?.IsOnlineDuelActive == true)
            {
                DuelOnlineSession.Instance.LeaveRoom();
            }
            if (IsActiveScene(MainMenuSceneName) ||
                !Application.CanStreamedLevelBeLoaded(MainMenuSceneName))
            {
                ShowMainMenu();
                return;
            }

            SceneManager.LoadScene(MainMenuSceneName);
        }

        private void OpenDuelArenaScene(PendingDuelMode mode)
        {
            DuelDeckLoadout selectedPlayer = null;
            string rejection =
                "A coleção de decks ainda não foi carregada.";
            if (_repository == null ||
                !_repository.TryCreateSelectedLoadout(
                    out selectedPlayer,
                    out rejection))
            {
                if (_duelRoomStatus != null)
                {
                    _duelRoomStatus.text = $"DUELO BLOQUEADO\n{rejection}";
                    _duelRoomStatus.color = Danger;
                }
                return;
            }

            _pendingPlayerLoadout = selectedPlayer;
            _pendingDuelMode = mode;
            if (mode != PendingDuelMode.Bot)
            {
                _activeDuelStatisticsId =
                    "local-" + Guid.NewGuid().ToString("N");
                _activeDuelStatisticsRanked = false;
            }
            if (IsActiveScene(DuelArenaSceneName) ||
                !Application.CanStreamedLevelBeLoaded(DuelArenaSceneName))
            {
                BeginOfflineDuelPrelude();
                return;
            }

            BeginOfflineDuelPrelude();
        }

        public void RecordConfirmedDuelStatistic(
            string eventId,
            DuelStatisticEventType eventType,
            long amount,
            bool online,
            bool ranked)
        {
            if (_repository == null)
                return;
            if (!_repository.TryRecordAuthoritativeStatisticEvent(
                    eventId,
                    eventType,
                    amount,
                    online,
                    ranked,
                    out string rejection) &&
                !string.IsNullOrWhiteSpace(rejection))
            {
                Debug.LogWarning("[Profile statistics] " + rejection);
            }
        }

        private IEnumerator StartRequestedDuelAfterArenaReset()
        {
            EnterDuelPresentation();

            // CardArenaBootstrap limpa os previews da Scene no primeiro frame.
            // O comando de início só é enviado depois desse reset, preservando
            // a mesma fronteira que será usada pelo host autoritativo.
            const int maximumReadyFrames = 300;
            int readyFrames = 0;
            do
            {
                yield return null;
                ResolveProjectReferences();
                readyFrames++;
            }
            while ((_duelArena == null ||
                    !_duelArena.IsPresentationReady) &&
                   readyFrames < maximumReadyFrames);

            SetDuelPresentation(true);

            if (_duelArena == null || !_duelArena.IsPresentationReady)
            {
                string failure = _duelArena == null
                    ? "A interface da arena não foi encontrada."
                    : string.IsNullOrWhiteSpace(
                        _duelArena.InitializationFailure)
                        ? "O Core do duelo não ficou pronto a tempo."
                        : _duelArena.InitializationFailure;
                Debug.LogError(
                    "[Duel startup] Arena indisponível: " + failure);
                _pendingDuelMode = PendingDuelMode.None;
                _pendingPlayerLoadout = null;
                _pendingBotLoadout = null;
                _pendingStartingPlayer = 0;
                yield break;
            }

            var online = DuelOnlineSession.Instance;
            if (DuelOnlineBridge.OnlineArenaTransitionPending ||
                online != null && online.IsOnlineDuelActive)
            {
                online?.AttachOnlineArena(_duelArena);
                yield break;
            }

            var mode = _pendingDuelMode == PendingDuelMode.None
                ? PendingDuelMode.LocalTest
                : _pendingDuelMode;
            _pendingDuelMode = PendingDuelMode.None;
            DuelDeckLoadout playerLoadout = _pendingPlayerLoadout;
            _pendingPlayerLoadout = null;
            byte startingPlayer = _pendingStartingPlayer;
            _pendingStartingPlayer = 0;
            online?.TransitionPresenter?.Hide();

            if (mode == PendingDuelMode.Bot ||
                mode == PendingDuelMode.StoryRoguelite)
            {
                var botLoadout = _pendingBotLoadout;
                _pendingBotLoadout = null;
                _duelArena?.StartDuelAgainstBot(
                    botLoadout,
                    playerLoadout,
                    startingPlayer);
            }
            else
            {
                _pendingBotLoadout = null;
                _duelArena?.StartLocalTestDuel(
                    playerLoadout,
                    startingPlayer);
            }
        }

        private void EnterDuelPresentation()
        {
            SetDuelPresentation(true);
            ClearScreen();
            CreateArcaneActionButton(
                _screenRoot,
                "MENU",
                new Vector2(0.90f, 0.018f),
                new Vector2(0.985f, 0.078f),
                ArcaneCyan,
                ToggleDuelMenu,
                17);
        }

        private void SetDuelPresentation(bool visible)
        {
            _duelPresentationVisible = visible;
            var arenas = FindObjectsByType<CardArenaBootstrap>(
                FindObjectsInactive.Include);
            _duelArena = FindPreferredDuelArena(_duelArena, arenas);
            var duelFields =
                FindObjectsByType<MasterDuelArena3D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            if (_duelField == null &&
                duelFields.Length > 0)
            {
                _duelField = duelFields[0];
            }
            var perspectives = FindObjectsByType<DuelTestPerspectiveController>(
                FindObjectsInactive.Include);
            if (_perspective == null && perspectives.Length > 0)
                _perspective = perspectives[0];

            foreach (var duelField in duelFields)
            {
                if (duelField != null)
                {
                    duelField.gameObject.SetActive(
                        duelField == _duelField &&
                        (visible ||
                         IsDuelSceneName(
                             SceneManager.GetActiveScene().name)));
                }
            }

            foreach (var arena in arenas)
            {
                if (arena != null)
                    arena.gameObject.SetActive(visible && arena == _duelArena);
            }

            foreach (var perspective in perspectives)
            {
                if (perspective != null)
                    perspective.enabled = visible && perspective == _perspective;
            }

            // Compatibilidade com cenas antigas: nenhum Canvas de duelo pode
            // sobreviver visualmente no menu ou no editor de decks.
            foreach (var canvas in FindObjectsByType<Canvas>(
                         FindObjectsInactive.Include))
            {
                if (canvas != null &&
                    string.Equals(canvas.name, "Arena Canvas", StringComparison.Ordinal))
                {
                    canvas.gameObject.SetActive(
                        visible &&
                        _duelArena != null &&
                        canvas.transform.IsChildOf(_duelArena.transform));
                }
            }
        }

        private static CardArenaBootstrap FindPreferredDuelArena(
            CardArenaBootstrap current,
            CardArenaBootstrap[] knownArenas = null)
        {
            var arenas = knownArenas ??
                         FindObjectsByType<CardArenaBootstrap>(
                             FindObjectsInactive.Include);
            CardArenaBootstrap activePrimary = null;
            CardArenaBootstrap activeFallback = null;
            CardArenaBootstrap inactivePrimary = null;
            CardArenaBootstrap fallback = null;

            foreach (var candidate in arenas)
            {
                if (candidate == null)
                    continue;

                fallback ??= candidate;
                var isActive = candidate.gameObject.activeInHierarchy ||
                               candidate.gameObject.activeSelf;
                if (isActive)
                    activeFallback ??= candidate;
                if (!candidate.IsPrimaryDuelInterface)
                    continue;
                if (isActive)
                    activePrimary ??= candidate;
                else
                    inactivePrimary ??= candidate;
            }

            // A Arena visivel na cena e a fonte autoral da Hierarchy. Isso
            // impede que uma copia legada, inativa, substitua o layout
            // editado quando o frontend abre um duelo local, contra bot ou
            // online.
            if (activePrimary != null)
                return activePrimary;
            if (current != null && current.IsPrimaryDuelInterface)
                return current;
            return activeFallback ?? inactivePrimary ?? current ?? fallback;
        }

        private static bool IsActiveScene(string sceneName)
        {
            return string.Equals(
                SceneManager.GetActiveScene().name,
                sceneName,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDuelSceneName(string sceneName)
        {
            return string.Equals(
                       sceneName,
                       DuelArenaSceneName,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       sceneName,
                       ArcaneDuel.Game.ProjectIdentity.DuelScene,
                       StringComparison.OrdinalIgnoreCase);
        }

        private Image BuildSharedBackground(string section)
        {
            var background = CreatePanel(
                _screenRoot,
                "Fundo",
                Vector2.zero,
                Vector2.one,
                new Color(Background.r, Background.g, Background.b, 0.975f));
            background.transform.SetAsFirstSibling();

            var upper = CreatePanel(
                background.transform,
                "Faixa Superior",
                new Vector2(0f, 0.86f),
                new Vector2(1f, 1f),
                new Color(0.04f, 0.11f, 0.23f, 0.86f));
            var accent = CreatePanel(
                background.transform,
                "Linha Ciano",
                new Vector2(0f, 0.857f),
                new Vector2(1f, 0.862f),
                Cyan);
            accent.raycastTarget = false;

            for (var i = 0; i < 9; i++)
            {
                var line = CreatePanel(
                    background.transform,
                    $"Linha {i + 1}",
                    new Vector2(0f, 0.06f + i * 0.095f),
                    new Vector2(1f, 0.061f + i * 0.095f),
                    new Color(0.12f, 0.38f, 0.62f, 0.16f));
                line.raycastTarget = false;
            }

            CreateText(
                upper.transform,
                section,
                16,
                FontStyle.Bold,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f),
                new Vector2(0.73f, 0.12f),
                new Vector2(0.96f, 0.88f),
                TextAnchor.MiddleRight);
            return background;
        }

        private void BuildHeader(string title, Action backAction)
        {
            CreateButton(
                _screenRoot,
                "‹",
                new Vector2(0.018f, 0.897f),
                new Vector2(0.067f, 0.975f),
                Lime,
                backAction);
            CreateText(
                _screenRoot,
                title,
                34,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.078f, 0.895f),
                new Vector2(0.62f, 0.978f),
                TextAnchor.MiddleLeft);
        }

        private void CreateDuelDeckPreview(
            Transform parent,
            DeckRecord deck,
            Vector2 min,
            Vector2 max)
        {
            var width = max.x - min.x;
            var height = max.y - min.y;
            const float cardWidthFraction = 0.26f;
            const float cardSpacingFraction = 0.30f;

            for (var i = 0; i < 3; i++)
            {
                CardCatalogEntry entry = null;
                if (deck.featuredCardIds != null &&
                    i < deck.featuredCardIds.Count)
                {
                    entry = DeckRepository.ResolveCard(
                        _catalog,
                        deck.featuredCardIds[i]);
                }

                var edgeOffset = Mathf.Abs(i - 1) * height * 0.035f;
                var cardWidth = width * cardWidthFraction;
                var center = min.x + width *
                    (0.5f + (i - 1) * cardSpacingFraction);
                Vector2 cardMin = new Vector2(
                    center - cardWidth * 0.5f,
                    min.y + edgeOffset);
                Vector2 cardMax = new Vector2(
                    center + cardWidth * 0.5f,
                    max.y - edgeOffset);
                Image card = CreateCardArtwork(
                    parent,
                    entry != null ? entry.Artwork : null,
                    cardMin,
                    cardMax,
                    0f,
                    true);
                bool isCapturedDuelHubCard =
                    i == 0 &&
                    Mathf.Abs(cardMin.x - 0.40024f) < 0.0001f &&
                    Mathf.Abs(cardMin.y - 0.55605f) < 0.0001f &&
                    Mathf.Abs(cardMax.x - 0.46056f) < 0.0001f &&
                    Mathf.Abs(cardMax.y - 0.76995f) < 0.0001f;
                if (isCapturedDuelHubCard)
                {
                    ApplyCapturedRectTransform(
                        card.rectTransform,
                        new Vector2(0.40024f, 0.55605f),
                        new Vector2(0.46056f, 0.76995f),
                        -9.699997f,
                        -0.6000061f,
                        9.699997f,
                        0.6000061f,
                        1f,
                        0f);
                }
            }
        }

        private void CreateFeaturedCards(
            Transform parent,
            DeckRecord deck,
            Vector2 min,
            Vector2 max)
        {
            var width = max.x - min.x;
            var height = max.y - min.y;
            for (var i = 0; i < 3; i++)
            {
                CardCatalogEntry entry = null;
                if (deck.featuredCardIds != null &&
                    i < deck.featuredCardIds.Count)
                {
                    entry = DeckRepository.ResolveCard(
                        _catalog,
                        deck.featuredCardIds[i]);
                }

                var cardWidth = width * 0.46f;
                var center = min.x + width * (0.5f + (i - 1) * 0.23f);
                var bottom = min.y + Mathf.Abs(i - 1) * height * 0.04f;
                CreateCardArtwork(
                    parent,
                    entry != null ? entry.Artwork : null,
                    new Vector2(center - cardWidth * 0.5f, bottom),
                    new Vector2(center + cardWidth * 0.5f, max.y),
                    (i - 1) * 9f,
                    true);
            }
        }

        private static readonly Color[] CaseColors =
        {
            Hex("#324ED8"),
            Hex("#B92B54"),
            Hex("#542DB3"),
            Hex("#078F9E"),
            Hex("#D76416"),
            Hex("#445267")
        };

        private void CreateDeckCaseVisual(
            Transform parent,
            int theme,
            Vector2 min,
            Vector2 max)
        {
            var color = CaseColors[Mathf.Abs(theme) % CaseColors.Length];
            var shadow = CreatePanel(
                parent,
                "Sombra do Porta-Deck",
                min + new Vector2(0.025f, -0.02f),
                max + new Vector2(0.025f, -0.02f),
                new Color(0f, 0f, 0f, 0.7f));
            shadow.raycastTarget = false;

            var box = CreatePanel(
                parent,
                "Porta-Deck",
                min,
                max,
                Color.clear);
            var caseSprite = ResolveDeckCaseSprite(theme);
            if (caseSprite != null)
            {
                box.sprite = caseSprite;
                box.preserveAspect = true;
                box.color = Color.white;
                AddOutline(
                    box.gameObject,
                    new Color(0.75f, 0.9f, 1f, 0.85f),
                    new Vector2(3f, -3f));
                return;
            }

            GameObject caseObject = new(
                "Porta-Deck tridimensional",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ArcaneDeckCase3DGraphic));
            caseObject.transform.SetParent(box.transform, false);
            RectTransform caseRect = caseObject.GetComponent<RectTransform>();
            Stretch(caseRect);
            ArcaneDeckCase3DGraphic caseGraphic =
                caseObject.GetComponent<ArcaneDeckCase3DGraphic>();
            caseGraphic.raycastTarget = false;
            caseGraphic.SetStyle(color, DeckEmerald);
            CreateText(
                box.transform,
                "MD2PU\nDECK",
                11,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.31f, 0.12f),
                new Vector2(0.86f, 0.32f),
                TextAnchor.MiddleCenter);
        }

        private Sprite ResolveDeckCaseSprite(int theme)
        {
            if (deckCaseVariants != null &&
                deckCaseVariants.Length > 0)
            {
                var candidate = deckCaseVariants[
                    Mathf.Abs(theme) % deckCaseVariants.Length];
                if (candidate != null)
                    return candidate;
            }

            return defaultDeckCaseSprite;
        }

        private static Image CreateCardArtwork(
            Transform parent,
            Sprite sprite,
            Vector2 min,
            Vector2 max,
            float rotation,
            bool withShadow)
        {
            if (withShadow)
            {
                var shadow = CreatePanel(
                    parent,
                    "Sombra da Carta",
                    min + new Vector2(0.012f, -0.012f),
                    max + new Vector2(0.012f, -0.012f),
                    new Color(0f, 0f, 0f, 0.72f));
                shadow.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, rotation);
                shadow.raycastTarget = false;
            }

            var card = CreatePanel(
                parent,
                sprite != null ? sprite.name : "Espaço de Carta",
                min,
                max,
                sprite != null
                    ? Color.white
                    : new Color(0.04f, 0.08f, 0.12f, 0.9f));
            card.sprite = sprite;
            card.preserveAspect = true;
            card.rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, rotation);
            return card;
        }

        private static RectTransform CreateFixedGrid(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Vector2 cellSize,
            Vector2 spacing,
            int columns,
            out RectTransform dropZone)
        {
            var frame = CreatePanel(
                parent,
                name,
                min,
                max,
                new Color(0.005f, 0.015f, 0.028f, 0.82f));
            dropZone = frame.rectTransform;

            var contentObject = new GameObject(
                "Conteúdo Fixo",
                typeof(RectTransform),
                typeof(GridLayoutGroup));
            contentObject.transform.SetParent(frame.transform, false);
            var content = contentObject.GetComponent<RectTransform>();
            Stretch(content);

            var grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            return content;
        }

        private static Vector2 CalculateResponsiveDeckCellSize(
            int cardCount,
            int columns,
            float availableWidth,
            float availableHeight,
            Vector2 maximumCellSize,
            Vector2 spacing)
        {
            var safeColumns = Mathf.Max(1, columns);
            var rows = Mathf.Max(
                1,
                Mathf.CeilToInt(Mathf.Max(1, cardCount) / (float)safeColumns));
            const float horizontalPadding = 16f;
            const float verticalPadding = 16f;
            var widthLimit =
                (availableWidth -
                 horizontalPadding -
                 spacing.x * (safeColumns - 1)) /
                safeColumns;
            var heightLimit =
                (availableHeight -
                 verticalPadding -
                 spacing.y * (rows - 1)) /
                rows;

            const float cardAspect = 0.6863f;
            var height = Mathf.Min(
                maximumCellSize.y,
                heightLimit,
                widthLimit / cardAspect);
            height = Mathf.Max(54f, height);
            var width = Mathf.Min(maximumCellSize.x, height * cardAspect);
            return new Vector2(width, height);
        }

        private static Text CreateScrollableText(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            int fontSize = 17,
            float handleBottomAnchor = 0f,
            bool stretchHandleWidth = false)
        {
            var viewport = CreatePanel(
                parent,
                name,
                min,
                max,
                new Color(0.004f, 0.012f, 0.024f, 0.96f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject(
                "Conteúdo do Texto",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            var content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout =
                contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 24, 10, 10);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var textObject = new GameObject(
                "Descrição",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(ContentSizeFitter));
            textObject.transform.SetParent(contentObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(0f, 24f);

            var text = textObject.GetComponent<Text>();
            text.font = MasterDuelTypography.Resolve(
                FontStyle.Normal,
                fontSize);
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.lineSpacing = 1.08f;

            var textFitter = textObject.GetComponent<ContentSizeFitter>();
            textFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var contentFitter =
                contentObject.GetComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;

            var scrollbarTrack = CreatePanel(
                viewport.transform,
                "Barra do Texto",
                new Vector2(0.955f, 0.02f),
                new Vector2(0.985f, 0.98f),
                new Color(0.05f, 0.10f, 0.14f, 0.95f));
            var slidingArea = new GameObject(
                "Área Deslizante",
                typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarTrack.transform, false);
            Stretch(slidingArea.GetComponent<RectTransform>());
            var handle = CreatePanel(
                slidingArea.transform,
                "Alça",
                new Vector2(
                    stretchHandleWidth ? 0f : 0.1f,
                    handleBottomAnchor),
                new Vector2(
                    stretchHandleWidth ? 1f : 0.9f,
                    1f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f));
            var scrollbar =
                scrollbarTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
            return text;
        }

        private static RectTransform CreateScrollGrid(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Vector2 cellSize,
            Vector2 spacing,
            int columns)
        {
            return CreateScrollGrid(
                parent,
                name,
                min,
                max,
                cellSize,
                spacing,
                columns,
                out _);
        }

        private static RectTransform CreateScrollGrid(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Vector2 cellSize,
            Vector2 spacing,
            int columns,
            out RectTransform viewportRect)
        {
            var viewport = CreatePanel(
                parent,
                name,
                min,
                max,
                new Color(0.005f, 0.015f, 0.028f, 0.82f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            viewportRect = viewport.rectTransform;

            var contentObject = new GameObject(
                "Conteúdo",
                typeof(RectTransform),
                typeof(GridLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            var content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(12f, 0f);
            content.offsetMax = new Vector2(-30f, 0f);
            var grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = spacing;
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;

            var fitter = contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 68f;

            var scrollbarTrack = CreatePanel(
                viewport.transform,
                "Barra de Rolagem",
                new Vector2(0.965f, 0.015f),
                new Vector2(0.992f, 0.985f),
                new Color(0.04f, 0.09f, 0.13f, 0.96f));
            var slidingArea = new GameObject(
                "Área Deslizante",
                typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarTrack.transform, false);
            Stretch(slidingArea.GetComponent<RectTransform>());

            var handle = CreatePanel(
                slidingArea.transform,
                "Alça",
                new Vector2(0.08f, 0f),
                new Vector2(0.92f, 1f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.92f));
            var scrollbar =
                scrollbarTrack.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.numberOfSteps = 0;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }

        private static void ApplyDeckEditorCatalogScrollbarStyle(
            RectTransform viewport)
        {
            Transform trackTransform = FindDescendantByName(
                viewport,
                "Barra de Rolagem");
            Image track = trackTransform != null
                ? trackTransform.GetComponent<Image>()
                : null;
            if (track != null)
                track.color = Hex("#001820");

            Transform handleTransform = FindDescendantByName(
                trackTransform,
                "Alça");
            Image handle = handleTransform != null
                ? handleTransform.GetComponent<Image>()
                : null;
            if (handle == null)
                return;

            handle.color = Hex("#21DDEF");
            AddOutline(
                handle.gameObject,
                new Color(0.82f, 0.98f, 1f, 0.88f),
                new Vector2(1f, -1f));
            Scrollbar scrollbar = trackTransform.GetComponent<Scrollbar>();
            if (scrollbar == null)
                return;
            ColorBlock colors = scrollbar.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Hex("#A5F6FF");
            colors.pressedColor = Hex("#00AFC7");
            colors.selectedColor = Hex("#6CEBFA");
            colors.fadeDuration = 0.08f;
            scrollbar.colors = colors;
        }

        private static Image CreateCatalogControlButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action action)
        {
            var image = CreatePanel(
                parent,
                $"Controle {label}",
                min,
                max,
                Color.clear);
            SkinDeckEditorSurface(image, accent, true, 0.82f);
            var button = image.gameObject.AddComponent<Button>();
            ArcanePanelSheenGraphic sheen =
                image.GetComponentInChildren<ArcanePanelSheenGraphic>(true);
            button.targetGraphic = sheen != null ? sheen : image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.18f);
            colors.pressedColor = Color.Lerp(Color.white, accent, 0.42f);
            colors.selectedColor = Color.Lerp(Color.white, accent, 0.24f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                action?.Invoke();
            });
            Text text = CreateText(
                image.transform,
                label,
                13,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.03f, 0.04f),
                new Vector2(0.97f, 0.96f),
                TextAnchor.MiddleCenter);
            text.gameObject.name = "Legenda do controle";
            Image energy = CreatePanel(
                image.transform,
                "Energia do controle",
                new Vector2(0.18f, 0.025f),
                new Vector2(0.82f, 0.055f),
                new Color(accent.r, accent.g, accent.b, 0.72f));
            energy.raycastTarget = false;
            Image core = CreatePanel(
                image.transform,
                "Núcleo do controle",
                new Vector2(0.035f, 0.38f),
                new Vector2(0.055f, 0.62f),
                new Color(accent.r, accent.g, accent.b, 0.94f));
            core.raycastTarget = false;
            core.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            StyleCatalogControlButton(image, accent, false);
            return image;
        }

        private static void StyleCatalogControlButton(
            Image image,
            Color accent,
            bool selected)
        {
            if (image == null)
                return;
            image.color = Color.clear;
            ArcanePanelSheenGraphic sheen =
                image.GetComponentInChildren<ArcanePanelSheenGraphic>(true);
            if (sheen == null)
            {
                SkinDeckEditorSurface(
                    image,
                    accent,
                    selected,
                    selected ? 0.96f : 0.78f);
                sheen = image.GetComponentInChildren<
                    ArcanePanelSheenGraphic>(true);
            }
            sheen?.SetStyle(
                accent,
                selected,
                selected ? 0.96f : 0.78f);
            Button button = image.GetComponent<Button>();
            if (button != null && sheen != null)
                button.targetGraphic = sheen;
            Text label = image.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = selected
                    ? new Color(0.97f, 0.92f, 0.78f, 1f)
                    : Color.white;
            }
            Transform energy = image.transform.Find("Energia do controle");
            Image energyImage = energy != null
                ? energy.GetComponent<Image>()
                : null;
            if (energyImage != null)
            {
                energyImage.color = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    selected ? 0.96f : 0.50f);
            }
            Transform core = image.transform.Find("Núcleo do controle");
            Image coreImage = core != null ? core.GetComponent<Image>() : null;
            if (coreImage != null)
            {
                coreImage.color = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    selected ? 1f : 0.72f);
            }
        }

        private static InputField CreateSearchField(
            Transform parent,
            string placeholder,
            Vector2 min,
            Vector2 max)
        {
            var background = CreatePanel(
                parent,
                "Busca de Cartas",
                min,
                max,
                Color.clear);
            SkinDeckEditorSurface(background, DeckMint, false, 0.86f);

            CreateText(
                background.transform,
                "⌕",
                30,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.02f, 0.03f),
                new Vector2(0.13f, 0.97f),
                TextAnchor.MiddleCenter);
            var inputText = CreateText(
                background.transform,
                string.Empty,
                17,
                FontStyle.Normal,
                Color.white,
                new Vector2(0.14f, 0.08f),
                new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleLeft);
            var placeholderText = CreateText(
                background.transform,
                placeholder,
                15,
                FontStyle.Bold,
                new Color(0.72f, 0.76f, 0.78f, 0.82f),
                new Vector2(0.14f, 0.08f),
                new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleLeft);

            var input = background.gameObject.AddComponent<InputField>();
            ArcanePanelSheenGraphic sheen =
                background.GetComponentInChildren<ArcanePanelSheenGraphic>(true);
            input.targetGraphic = sheen != null ? sheen : background;
            input.textComponent = inputText;
            input.placeholder = placeholderText;
            input.lineType = InputField.LineType.SingleLine;
            input.characterLimit = 80;
            input.selectionColor =
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f);
            return input;
        }

        private static InputField CreateProfileNameField(
            Transform parent,
            string placeholder,
            Vector2 min,
            Vector2 max)
        {
            var background = CreatePanel(
                parent,
                "Nome de Duelista",
                min,
                max,
                new Color(0.025f, 0.09f, 0.135f, 0.98f));
            AddOutline(
                background.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.88f),
                new Vector2(2f, -2f));

            var inputText = CreateText(
                background.transform,
                string.Empty,
                24,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.06f, 0.08f),
                new Vector2(0.94f, 0.92f),
                TextAnchor.MiddleLeft);
            var placeholderText = CreateText(
                background.transform,
                placeholder,
                20,
                FontStyle.Normal,
                new Color(Muted.r, Muted.g, Muted.b, 0.78f),
                new Vector2(0.06f, 0.08f),
                new Vector2(0.94f, 0.92f),
                TextAnchor.MiddleLeft);

            var input = background.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = inputText;
            input.placeholder = placeholderText;
            input.lineType = InputField.LineType.SingleLine;
            input.selectionColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f);
            return input;
        }

        private static Image CreateMenuButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action action)
        {
            var button = CreateButton(
                parent,
                label,
                min,
                max,
                accent,
                action);
            var marker = CreatePanel(
                button.transform,
                "Marcador",
                new Vector2(0f, 0.08f),
                new Vector2(0.018f, 0.92f),
                accent);
            marker.raycastTarget = false;
            return button;
        }

        private static Image CreateButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Color accent,
            Action action)
        {
            var image = CreatePanel(
                parent,
                $"Botão {label}",
                min,
                max,
                new Color(0.015f, 0.045f, 0.075f, 0.98f));
            AddOutline(
                image.gameObject,
                new Color(accent.r, accent.g, accent.b, 0.88f),
                new Vector2(2f, -2f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.42f);
            colors.pressedColor = accent;
            colors.selectedColor = Color.Lerp(Color.white, accent, 0.28f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                action?.Invoke();
            });

            CreateText(
                image.transform,
                label,
                22,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.04f, 0.05f),
                new Vector2(0.96f, 0.95f),
                TextAnchor.MiddleCenter);
            return image;
        }

        private static void AddButtonBehaviour(Image image, Action action)
        {
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.72f, 0.9f, 1f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                action?.Invoke();
            });
        }

        private static Image CreatePanel(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color color)
        {
            var item = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = item.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string value,
            int size,
            FontStyle style,
            Color color,
            Vector2 min,
            Vector2 max,
            TextAnchor alignment)
        {
            var item = new GameObject(
                value,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = item.GetComponent<Text>();
            text.text = value;
            text.font = MasterDuelTypography.Resolve(style, size);
            text.fontSize = size;
            text.fontStyle = style == FontStyle.Italic ||
                             style == FontStyle.BoldAndItalic
                ? FontStyle.Italic
                : FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = size;
            text.raycastTarget = false;
            ArcaneUiTextScaleRuntime.Register(text, size);
            return text;
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

        private static void AddTrigger(
            EventTrigger trigger,
            EventTriggerType type,
            Action<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry
            {
                eventID = type
            };
            entry.callback.AddListener(data => action?.Invoke(data));
            trigger.triggers.Add(entry);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Persiste no gerador os valores exibidos pelo Inspector do Unity.
        /// Right e Top usam o sinal inverso de offsetMax no RectTransform.
        /// </summary>
        private static void ApplyCapturedRectTransform(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float left,
            float top,
            float right,
            float bottom,
            float uniformScale = 1f,
            float rotationZ = 0f)
        {
            if (rect == null)
                return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = new Vector3(
                uniformScale,
                uniformScale,
                uniformScale);
            rect.localEulerAngles = new Vector3(0f, 0f, rotationZ);
        }

        private void ClearScreen()
        {
            CancelPackOpeningPresentation();
            StopMainMenuConnectionMonitor();
            _duelMenuOverlay = null;
            _duelMenuContent = null;
            ReleaseVirtualCatalogView();
            _deckEditorSelectedCardId = string.Empty;
            if (_mainMenuSceneView != null)
                _mainMenuSceneView.SetMainMenuVisible(false);
            if (_shopSceneView != null)
            {
                _shopSceneView.SetVisible(false);
                _shopSceneView.ClearCatalog();
            }
            _mainDropZone = null;
            _extraDropZone = null;
            _catalogDropZone = null;
            _mainDeckContent = null;
            _extraDeckContent = null;
            _catalogContent = null;
            _mainDeckCountText = null;
            _mainDeckLimitText = null;
            _extraDeckCountText = null;
            _editorStatus = null;
            _catalogSearchInput = null;
            _deckEditorDetailArtwork = null;
            _deckEditorCardHeader = null;
            _deckEditorEffectHeader = null;
            _deckEditorZoomOverlay = null;
            _deckEditorZoomArtwork = null;
            _deckEditorZoomViewer = null;
            _deckEditorDetailName = null;
            _deckEditorDetailType = null;
            _deckEditorDetailEffect = null;
            _starterClaimModal = null;
            _deckDeleteModal = null;
            _catalogFilterButtons.Clear();
            if (_dragGhost != null)
                Destroy(_dragGhost.gameObject);
            _dragGhost = null;

            for (var i = _screenRoot.childCount - 1; i >= 0; i--)
            {
                var previous =
                    _screenRoot.GetChild(i);
                previous.SetParent(null, false);
                previous.gameObject.SetActive(false);
                Destroy(previous.gameObject);
            }
        }

        private List<CardCatalogEntry> ReadyCatalogEntries()
        {
            var entries = new List<CardCatalogEntry>();
            if (_catalog == null)
                return entries;

            foreach (var entry in _catalog.Entries)
            {
                if (entry != null &&
                    entry.IsReadyForGameplay &&
                    entry.IsCollectible &&
                    entry.HasArtwork)
                {
                    entries.Add(entry);
                }
            }

            entries.Sort((left, right) =>
                string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.CurrentCultureIgnoreCase));
            return entries;
        }

        private int DeckEditorOwnedCopies(
            CardCatalogEntry entry,
            string cardId)
        {
            return entry != null && _repository != null
                ? _repository.OwnedCardQuantity(cardId)
                : 0;
        }

        private static int CountCopies(
            List<string> source,
            string cardId)
        {
            if (source == null)
                return 0;

            var count = 0;
            foreach (var candidate in source)
            {
                if (string.Equals(
                        candidate,
                        cardId,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private IEnumerator CaptureFrontendAndExit(
            string path,
            string captureState)
        {
            yield return new WaitForSecondsRealtime(1.0f);
            if (string.Equals(
                    captureState,
                    "duel-selection",
                    StringComparison.OrdinalIgnoreCase))
            {
                ShowDuelRoom();
            }
            else if (string.Equals(
                         captureState,
                         "duel-hub",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowDuelHub();
            }
            else if (string.Equals(
                         captureState,
                         "decks",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowDeckGallery();
            }
            else if (string.Equals(
                         captureState,
                         "shop",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowDeckShop();
            }
            else if (string.Equals(
                         captureState,
                         "options",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowAnimationOptions();
            }
            yield return new WaitForSecondsRealtime(0.4f);
            ScreenCapture.CaptureScreenshot(path, 1);
            yield return new WaitForSecondsRealtime(1.2f);
            Application.Quit(0);
        }

        private static bool HasCommandArgument(string name)
        {
            return Array.Exists(
                Environment.GetCommandLineArgs(),
                argument => string.Equals(
                    argument,
                    name,
                    StringComparison.OrdinalIgnoreCase));
        }

        private static string CommandArgumentValue(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (string.Equals(
                        arguments[index],
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }
            return string.Empty;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out var color);
            return color;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }

        private void OnDestroy()
        {
            ReleasePlayerIdAccess();
            if (Application.isPlaying && IsActiveScene(DeckEditorSceneName))
                _repository?.ClearPendingDeckEditorNewCards();
            CancelPackOpeningPresentation();
            ReleasePackOpeningAnimationResources();
            ReleaseShopMysteryCardSprite();
            ReleaseShopVisualSprites();
            ReleaseMainMenuHudOverlayMaterial();
            if (Instance == this)
                Instance = null;
        }
    }
}
