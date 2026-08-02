using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ArcaneArena.Cards;
using ArcaneArena.Frontend;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena
{
    /// <summary>
    /// Presentation adapter for the authored legacy arena.
    /// It contains no card rules. Every action is a response to a prompt
    /// produced by ygopro-core through DuelArenaController.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class CardArenaBootstrap : MonoBehaviour
    {
        private const uint FaceUpAttack = 0x1;
        private const uint FaceDownAttack = 0x2;
        private const uint FaceUpDefense = 0x4;
        private const uint FaceDownDefense = 0x8;
        private const float HandCardHeight = 258f;
        private const float HandVisibleViewportRatio = 0.14f;
        private const float HandMinimumVisibleHeight = 124f;
        private const float HandMaximumVisibleHeight = 154f;
        private const float HandLowerViewportOffset = 82f;
        private const float DesktopPassiveRefreshInterval = 0.08f;
        private const float MobilePassiveRefreshInterval = 0.20f;

        [SerializeField] private List<Sprite> cardSprites = new();
        [SerializeField] private Sprite cardBackSprite;
        [SerializeField] private CardCatalog cardCatalog;
        [SerializeField] private bool primaryDuelInterface = true;
        [SerializeField] private bool preserveAuthoredDuelInterface = true;
        [SerializeField] private Sprite detailLevelIconTemplate;
        [SerializeField] private Sprite detailAttackIconTemplate;
        [SerializeField] private Sprite detailDefenseIconTemplate;
        [SerializeField] private Sprite detailCardLayoutTemplate;
        [SerializeField] private Sprite detailCardHeaderTemplate;
        [SerializeField] private Sprite detailEffectHeaderTemplate;
        [SerializeField] private Sprite choiceSelectionTemplate;
        [SerializeField] private Sprite choiceSelectionArrowTemplate;
        [SerializeField] private List<Sprite> detailAttributeIconTemplates = new();
        [SerializeField] private List<Sprite> detailTypeIconTemplates = new();

        private static readonly Color Cyan = Hex("#52E8E0");
        private static readonly Color SummonBlue = Hex("#52C3FF");
        private static readonly Color EffectGlow = Hex("#A0FF25");
        private static readonly Color Gold = Hex("#F6C766");
        private static readonly Color Lime = Hex("#C8FF19");
        private static readonly Color Muted = Hex("#87A8B7");
        private static readonly Color Red = Hex("#FF5E73");
        private static Font font;

        private readonly List<CardView> handViews = new();
        private readonly Dictionary<uint, Sprite> runtimeSprites = new();
        private readonly Dictionary<uint, Texture2D> runtimeTextures = new();
        private readonly HashSet<int> selectedPromptIndexes = new();
        private readonly Dictionary<string, CardInstanceKey> renderedZones =
            new(StringComparer.Ordinal);

        private DuelArenaController core;
        private DuelPresentationState state;
        private CardDatabase database;
        private CardVisualCatalog visualCatalog;
        private Canvas arenaCanvas;
        private RectTransform frame;
        private RectTransform handRoot;
        private DuelHandLayoutAnchor handLayoutAnchor;
        private CanvasGroup handInteractionGroup;
        private Vector2 handRestPosition;
        private Vector3 handRestScale = Vector3.one;
        private Vector2 lastHandViewportSize = new Vector2(-1f, -1f);
        private bool handPlacementMode;
        private CardView selectedCard;
        private CardView hoveredCard;
        private GameObject actionPanel;
        private GameObject activateAction;
        private GameObject summonAction;
        private GameObject setAction;
        private GameObject detailPanel;
        private Image detailArtwork;
        private Text detailName;
        private Text detailType;
        private Text detailStats;
        private Text detailEffect;
        private Image detailAttributeIcon;
        private Image detailLevelIcon;
        private Text detailLevel;
        private Image detailTypeIcon;
        private Image detailAttackIcon;
        private Image detailDefenseIcon;
        private Text detailAttack;
        private Text detailDefense;
        private Text detailCombatType;
        private Image detailCardHeaderImage;
        private Image detailEffectHeaderImage;
        private Outline detailCardOutline;
        private Text localLife;
        private Text opponentLife;
        private Text localPlayerName;
        private string localPlayerDisplayName = "DUELISTA LOCAL";
        private Text status;
        private Button phaseButton;
        private Text phaseLabel;
        private GameObject choiceModal;
        private Text choiceTitle;
        private RectTransform choiceContent;
        private Button choiceConfirm;
        private GameObject zoneBrowser;
        private GameObject zoneBrowserTray;
        private ScrollRect zoneBrowserScroll;
        private RectTransform zoneBrowserContent;
        private Text zoneBrowserTitle;
        private uint inspectedCode;
        private DuelZone3D inspectedZone;
        private DuelPrompt observedPrompt;
        private DuelPrompt scheduledAutomaticPrompt;
        private Coroutine automaticPromptRoutine;
        private ulong observedHandSignature;
        private ulong observedFieldSignature;
        private DuelZone3D draggingAttacker;
        private LineRenderer attackLine;
        private bool presentationReady;
        private bool criticalInteractionLocked;
        private float nextPassiveRefreshTime;

        private bool InteractionLocked =>
            criticalInteractionLocked ||
            phasePresentationLocked ||
            cardPresentationDecisionLocked ||
            (core != null && core.PresentationDecisionLocked);

        public bool IsPrimaryDuelInterface => primaryDuelInterface;
        public CardCatalog CardCatalog => cardCatalog;
        public bool NeedsEditorRebuild => false;

        private void Awake()
        {
            if (!Application.isPlaying || !primaryDuelInterface)
            {
                if (Application.isPlaying && !primaryDuelInterface)
                    gameObject.SetActive(false);
                return;
            }
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            core = GetComponent<DuelArenaController>();
            if (core == null)
                core = gameObject.AddComponent<DuelArenaController>();
            StartCoroutine(InitializeAfterSceneAwake());
        }

        private IEnumerator InitializeAfterSceneAwake()
        {
            yield return null;

            DisableLegacyDuplicate();
            BindAuthoredHierarchy();
            ClearAuthoredPreviewCards();
            EnsureEventSystem();

            core = GetComponent<DuelArenaController>();
            if (core == null) core = gameObject.AddComponent<DuelArenaController>();
            core.CoreEventPresented += OnCoreEvent;
            core.CoreFailure += OnCoreFailure;
            core.PresentationStateChanged += OnPresentationStateChanged;

            yield return null;
            state = core.PresentationState;
            database = core.Database;
            visualCatalog = CardVisualCatalog.LoadDefault();
            presentationReady = state != null && database != null;
            RefreshEverything(true);
            SetStatus(
                "Escolha uma carta iluminada da mão, do campo ou do Deck Adicional.",
                Cyan);
        }

        private void OnDestroy()
        {
            presentationReady = false;
            criticalInteractionLocked = false;
            phasePresentationLocked = false;
            if (core != null)
            {
                core.CoreEventPresented -= OnCoreEvent;
                core.CoreFailure -= OnCoreFailure;
                core.PresentationStateChanged -= OnPresentationStateChanged;
            }
            DisposeArenaPresentation();
            ResetCardSoundPresentation();
            if (automaticPromptRoutine != null)
                StopCoroutine(automaticPromptRoutine);
            foreach (Texture2D texture in runtimeTextures.Values)
            {
                if (texture != null) Destroy(texture);
            }
            runtimeTextures.Clear();
            runtimeSprites.Clear();
        }

        private void OnCoreFailure(string failure)
        {
            string detail = string.IsNullOrWhiteSpace(failure)
                ? "Falha inesperada nos dados do duelo."
                : failure;
            SetStatus(
                $"DUELO INTERROMPIDO · {detail} · Retorne ao menu para reiniciar.",
                Red);
            UpdateDecisionRibbon(
                "O DUELO FOI INTERROMPIDO · RETORNE AO MENU",
                Red);
            PushDuelFeed("Falha de dados do duelo", Red);
        }

        private void Update()
        {
            UpdateDuelExperienceAnimation();
            UpdateDrawRevealFastForward();
            RecoverStalledTurnFlowPresentation();
            UpdateCardPresentationAcceleration();
            UpdateOnlineInteractionWaitStatus();
            ApplyResponsiveHandLayout(false);
            if (core == null || state == null) return;
            EnsureRequiredResponseTrayVisible();
            if (Time.unscaledTime >= nextPassiveRefreshTime)
                RefreshEverything(false);
        }

        private void LateUpdate()
        {
            if (actionPanel != null && actionPanel.activeSelf &&
                selectedCard != null)
            {
                RectTransform rect = actionPanel.GetComponent<RectTransform>();
                rect.anchoredPosition =
                    new Vector2(selectedCard.Rect.anchoredPosition.x, 0f);
                actionPanel.transform.SetAsLastSibling();
            }
            UpdateFieldActionMenuPosition();
        }

        public void StartLocalTestDuel(
            DuelDeckLoadout playerLoadout = null)
        {
            Debug.Log(
                $"[Arcane legacy bridge] StartLocalTestDuel requested: " +
                $"{playerLoadout?.displayName ?? "deck ativo persistido"}.");
            StartSelectedDuel(null, false, playerLoadout);
            SetStatus("Duelo local iniciado pelo ygopro-core.", Cyan);
        }

        public void StartDuelAgainstBot(
            DuelDeckLoadout loadout = null,
            DuelDeckLoadout playerLoadout = null)
        {
            Debug.Log(
                $"[Arcane legacy bridge] StartDuelAgainstBot requested: " +
                $"player={playerLoadout?.displayName ?? "deck ativo persistido"}; " +
                $"bot={loadout?.displayName ?? "deck espelhado"}.");
            StartSelectedDuel(loadout, true, playerLoadout);
            SetStatus(
                "Duelo contra IA tática iniciado pelo ygopro-core.",
                Lime);
        }

        public void Build()
        {
            // The authored hierarchy is intentionally preserved verbatim.
        }

        public void BuildSceneCardPreviews()
        {
            // Preview generation belongs to the original editor tooling only.
        }

        private void StartSelectedDuel(
            DuelDeckLoadout requestedOpponent,
            bool versusBot,
            DuelDeckLoadout requestedPlayer)
        {
            DuelDeckLoadout player = requestedPlayer;
            string rejection = string.Empty;
            if (player == null)
            {
                GameFrontendBootstrap frontend =
                    GameFrontendBootstrap.Instance;
                if (frontend == null ||
                    !frontend.TryGetSelectedDuelLoadout(
                        out player,
                        out rejection))
                {
                    Debug.LogWarning(
                        $"[Arcane legacy bridge] Selected loadout rejected: " +
                        $"{rejection}");
                    SetStatus(
                        string.IsNullOrWhiteSpace(rejection)
                            ? "O deck selecionado não pôde ser carregado."
                            : rejection,
                        Gold);
                    return;
                }
            }

            DuelDeckLoadout opponent =
                requestedOpponent ?? player;
            localPlayerDisplayName =
                string.IsNullOrWhiteSpace(player.playerDisplayName)
                    ? "DUELISTA LOCAL"
                    : player.playerDisplayName;
            UpdateLocalPlayerName();
            uint[] playerMain = ParseCodes(player.mainDeckCardIds);
            uint[] playerExtra = ParseCodes(player.extraDeckCardIds);
            uint[] opponentMain = ParseCodes(opponent.mainDeckCardIds);
            uint[] opponentExtra = ParseCodes(opponent.extraDeckCardIds);
            if (playerMain.Length < 40 || opponentMain.Length < 40)
            {
                Debug.LogWarning(
                    $"[Arcane legacy bridge] Resolved deck is incomplete: " +
                    $"player={playerMain.Length}/{player.mainDeckCardIds.Count}, " +
                    $"opponent={opponentMain.Length}/{opponent.mainDeckCardIds.Count}.");
                SetStatus(
                    "O catálogo não resolveu 40 cartas válidas para um dos duelistas.",
                    Gold);
                return;
            }

            try
            {
                Debug.Log(
                    $"[Arcane legacy bridge] Restarting core with " +
                    $"player='{player.displayName}' id='{player.deckId}' " +
                    $"main={playerMain.Length} cards=[{string.Join(",", playerMain.Take(8))}] " +
                    $"extra={playerExtra.Length}; " +
                    $"opponent='{opponent.displayName}' main={opponentMain.Length} " +
                    $"extra={opponentExtra.Length}.");
                core.RestartExternalDuel(
                    playerMain,
                    playerExtra,
                    opponentMain,
                    opponentExtra);
                state = core.PresentationState;
                observedPrompt = null;
                ResetPromptPresentationIdentity();
                observedHandSignature = 0UL;
                observedFieldSignature = 0UL;
                RefreshEverything(true);
                SetStatus(
                    versusBot
                        ? "DUELO CONTRA IA TÁTICA · REGRAS PELO YGOPRO-CORE"
                        : "TREINO LOCAL P1 / P2 · REGRAS PELO YGOPRO-CORE",
                    Lime);
            }
            catch (Exception exception)
            {
                SetStatus(
                    $"Não foi possível iniciar o duelo: {exception.GetBaseException().Message}",
                    Gold);
                Debug.LogException(exception);
            }
        }

        private uint[] ParseCodes(IEnumerable<string> values)
        {
            if (values == null)
                return Array.Empty<uint>();

            return values
                .Select(value =>
                    uint.TryParse(value, out uint code)
                        ? code
                        : 0u)
                .Where(code =>
                    code != 0 &&
                    database != null &&
                    database.TryGet(code, out _))
                .ToArray();
        }

        public void PrepareCaptureState(string captureState)
        {
            if (!Application.isPlaying || core == null || state == null)
                return;

            RefreshEverything(true);
            SuppressAnnouncementBanner();
            if (PrepareArenaPresentationCapture(captureState))
                return;
            if (string.Equals(
                    captureState,
                    "phase",
                    StringComparison.OrdinalIgnoreCase))
            {
                OpenPhaseChoices();
                return;
            }

            if (string.Equals(
                    captureState,
                    "inspector",
                    StringComparison.OrdinalIgnoreCase))
            {
                // Stable monster fixture used by visual regression captures.
                ShowInspector(38517737);
                return;
            }

            if (!string.Equals(
                    captureState,
                    "action",
                    StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(
                    captureState,
                    "placement",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            DuelPrompt prompt = core.CurrentPrompt;
            bool capturePlacement = string.Equals(
                captureState,
                "placement",
                StringComparison.OrdinalIgnoreCase);
            CardView legalCard = null;
            DuelChoice placementAction = null;
            foreach (CardView card in handViews.Where(card => card != null))
            {
                List<DuelChoice> choices = ChoicesForCard(
                        prompt,
                        card.InstanceKey)
                    .ToList();
                if (!capturePlacement && choices.Count > 0)
                {
                    legalCard = card;
                    break;
                }
                DuelChoice action = choices.FirstOrDefault(choice =>
                        Contains(choice.Label, "Invocar") &&
                        !Contains(choice.Label, "Baixar")) ??
                    choices.FirstOrDefault(choice =>
                        Contains(choice.Label, "Baixar"));
                if (action != null)
                {
                    legalCard = card;
                    placementAction = action;
                    break;
                }
            }
            CardView cardToInspect =
                legalCard ?? handViews.FirstOrDefault(card => card != null);
            if (cardToInspect != null)
                SelectCard(cardToInspect);

            if (legalCard == null ||
                !string.Equals(
                    captureState,
                    "placement",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (placementAction != null)
            {
                core.SubmitChoice(placementAction);
                RefreshEverything(true);
            }
        }

        private void OnCoreEvent(DuelEvent duelEvent)
        {
            state = core.PresentationState;
            presentationReady = state != null && database != null;
            PrepareTurnFlowPresentation(duelEvent);
            RefreshEverything(true);
            ValidatePresentationConsistency(duelEvent, true);
            HandleArenaPresentationEvent(duelEvent);
            QueueCardSoundPresentation(duelEvent);
        }

        private void OnPresentationStateChanged()
        {
            state = core != null ? core.PresentationState : null;
            presentationReady = state != null && database != null;
            RefreshEverything(true);
        }

        private void RefreshEverything(bool force)
        {
            if (state == null) return;
            nextPassiveRefreshTime = Time.unscaledTime +
                (ArcaneGraphicsPreferences.IsMobileRuntime
                    ? MobilePassiveRefreshInterval
                    : DesktopPassiveRefreshInterval);
            ulong handSignature = BuildHandSignature();
            if (handSignature != observedHandSignature)
            {
                observedHandSignature = handSignature;
                RebuildHand();
            }

            ulong fieldSignature = BuildFieldSignature();
            if (force || fieldSignature != observedFieldSignature)
            {
                observedFieldSignature = fieldSignature;
                ReconcileField();
            }
            if (force)
                RefreshInspectedCombatStats();

            UpdateLifeAndPhase();
            DuelPrompt prompt = core.CurrentPrompt;
            // Snapshots received from the network may recreate the prompt
            // object while preserving its RequestId. Use the semantic prompt
            // identity so a heartbeat cannot reopen the response tray or
            // discard an in-progress multi-card selection.
            observedPrompt = prompt;
            if (!IsPromptPresentationCurrent(prompt))
            {
                selectedPromptIndexes.Clear();
                if (RefreshPrompt(prompt))
                    MarkPromptPresented(prompt);
            }
            RefreshDuelExperienceState();
        }

        private void BindAuthoredHierarchy()
        {
            Transform canvasTransform = transform.Find("Arena Canvas");
            if (canvasTransform == null)
            {
                var canvasObject = new GameObject(
                    "Arena Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                canvasObject.transform.SetParent(transform, false);
                canvasTransform = canvasObject.transform;
            }
            arenaCanvas = canvasTransform.GetComponent<Canvas>();
            arenaCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler =
                canvasTransform.GetComponent<CanvasScaler>() ??
                canvasTransform.gameObject.AddComponent<CanvasScaler>();
            UniversalUiLayout.ConfigureCanvasScaler(scaler);
            frame = canvasTransform.Find(
                UniversalUiLayout.FrameName) as RectTransform;
            if (frame == null)
                frame = UniversalUiLayout.CreateFrame(canvasTransform, true);

            handRoot = FindRect(frame, "POSICAO DA MAO DO JOGADOR") ??
                FindRect(frame, "Mão do Jogador");
            if (handRoot == null)
            {
                handRoot = CreateRect(
                    frame,
                    "POSICAO DA MAO DO JOGADOR",
                    new Vector2(0.5f, 0f),
                    new Vector2(0.5f, 0f),
                    new Vector2(1000f, 330f));
                handRoot.pivot = new Vector2(0.5f, 0f);
                handRoot.anchoredPosition = new Vector2(0f, -15f);
            }
            handRoot.anchorMin = new Vector2(0.5f, 0f);
            handRoot.anchorMax = new Vector2(0.5f, 0f);
            handRoot.pivot = new Vector2(0.5f, 0f);
            handLayoutAnchor =
                handRoot.GetComponent<DuelHandLayoutAnchor>();
            if (handLayoutAnchor == null)
            {
                handLayoutAnchor =
                    handRoot.gameObject.AddComponent<DuelHandLayoutAnchor>();
                handLayoutAnchor.ConfigureOwner(
                    DuelHandLayoutAnchor.HandOwner.LocalPlayer);
            }
            handRestPosition = handRoot.anchoredPosition;
            handRestScale = handRoot.localScale;
            ApplyResponsiveHandLayout(true);
            handInteractionGroup = handRoot.GetComponent<CanvasGroup>();
            if (handInteractionGroup == null)
                handInteractionGroup =
                    handRoot.gameObject.AddComponent<CanvasGroup>();
            handInteractionGroup.alpha = 1f;
            handInteractionGroup.interactable = true;
            handInteractionGroup.blocksRaycasts = true;

            detailPanel = FindObject(frame, "Ficha Lateral da Carta");
            BindDetailPanel();
            actionPanel = FindObject(frame, "Ações da Carta Selecionada");
            BindActionButtons();
            BuildFieldActionMenu();
            BindLifeAndPhase();
            BuildChoiceModal();
            BuildZoneBrowser();
            BuildArenaPresentation();
        }

        private void BindDetailPanel()
        {
            if (detailPanel == null)
            {
                detailPanel = CreatePanel(
                    frame,
                    "Ficha Lateral da Carta",
                    new Vector2(0.012f, 0.025f),
                    new Vector2(0.272f, 0.975f),
                    new Color(0.01f, 0.025f, 0.04f, 0.98f));
            }
            Transform art = FindTransform(detailPanel.transform, "Arte da Carta");
            detailArtwork = art != null ? art.GetComponent<Image>() : null;
            if (detailArtwork == null)
            {
                detailArtwork = CreateImage(
                    detailPanel.transform,
                    "Arte da Carta",
                    new Vector2(0.06f, 0.505f),
                    new Vector2(0.57f, 0.89f),
                    Color.white);
            }

            Transform header =
                FindTransform(detailPanel.transform, "Cabecalho Personalizado") ??
                FindTransform(detailPanel.transform, "Cabeçalho Personalizado");
            detailCardHeaderImage = header != null
                ? header.GetComponent<Image>()
                : null;
            detailName = header != null
                ? header.GetComponentsInChildren<Text>(true)
                    .Where(text => text.text != "×")
                    .OrderByDescending(text => text.fontSize)
                    .FirstOrDefault()
                : null;
            if (detailName == null)
            {
                detailName = CreateText(
                    detailPanel.transform,
                    "Nome da Carta",
                    22,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.06f, 0.91f),
                    new Vector2(0.94f, 0.985f),
                    TextAnchor.MiddleCenter);
            }

            Transform attribute =
                header != null ? FindTransform(header, "Atributo") : null;
            detailAttributeIcon =
                attribute != null ? attribute.GetComponent<Image>() : null;
            Transform close =
                header != null ? FindTransform(header, "×") : null;
            Button closeButton =
                close != null ? close.GetComponent<Button>() : null;
            if (closeButton != null)
                closeButton.onClick.AddListener(CloseCardDetails);

            Transform information =
                FindTransform(detailPanel.transform, "Informacoes") ??
                FindTransform(detailPanel.transform, "Informações");
            if (information != null)
            {
                information.gameObject.SetActive(true);
                detailLevelIcon =
                    FindTransform(information, "levelstar")?.GetComponent<Image>();
                detailTypeIcon =
                    FindTransform(information, "Type")?.GetComponent<Image>();
                detailAttackIcon =
                    FindTransform(information, "AtackIcon")?.GetComponent<Image>();
                detailDefenseIcon =
                    FindTransform(information, "DefesaIcon")?.GetComponent<Image>();
                Text[] informationTexts = information
                    .GetComponentsInChildren<Text>(true)
                    .OrderBy(text => text.transform.GetSiblingIndex())
                    .ToArray();
                detailLevel =
                    informationTexts.Length > 0 ? informationTexts[0] : null;
                detailAttack =
                    informationTexts.Length > 1 ? informationTexts[1] : null;
                detailDefense =
                    informationTexts.Length > 2 ? informationTexts[2] : null;
            }

            Transform typeHeader =
                FindTransform(detailPanel.transform, "Cabecalho do Tipo da Carta") ??
                FindTransform(detailPanel.transform, "Cabeçalho do Tipo da Carta");
            detailEffectHeaderImage =
                typeHeader != null ? typeHeader.GetComponent<Image>() : null;
            detailCombatType = typeHeader != null
                ? typeHeader.GetComponentInChildren<Text>(true)
                : null;
            detailType = detailCombatType;
            detailStats = detailAttack;
            Transform effect =
                FindTransform(detailPanel.transform, "Texto do Efeito");
            detailEffect = effect != null ? effect.GetComponent<Text>() : null;
            detailCardOutline = detailPanel.GetComponent<Outline>();
            detailPanel.SetActive(false);
        }

        private void BindActionButtons()
        {
            if (actionPanel == null)
            {
                actionPanel = CreatePanel(
                    frame,
                    "Ações da Carta Selecionada",
                    new Vector2(0.5f, 0.405f),
                    new Vector2(0.5f, 0.405f),
                    Color.clear);
            }

            activateAction = FindObject(actionPanel.transform, "Ação Principal");
            summonAction = FindObject(actionPanel.transform, "Invocar");
            setAction = FindObject(actionPanel.transform, "Baixar");
            if (activateAction == null)
                activateAction = CreateActionButton("Ação Principal", "ATIVAR");
            if (summonAction == null)
                summonAction = CreateActionButton("Invocar", "INVOCAR");
            if (setAction == null)
                setAction = CreateActionButton("Baixar", "BAIXAR");
            RebindButton(activateAction, SubmitActivateAction);
            RebindButton(summonAction, SubmitSummonAction);
            RebindButton(setAction, SubmitSetAction);
            actionPanel.SetActive(false);
        }

        private GameObject CreateActionButton(string objectName, string label)
        {
            GameObject panel = CreatePanel(
                actionPanel.transform,
                objectName,
                Vector2.zero,
                Vector2.one,
                new Color(0.008f, 0.025f, 0.04f, 0.97f));
            var button = panel.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            CreateText(
                panel.transform,
                label,
                13,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            return panel;
        }

        private void BindLifeAndPhase()
        {
            GameObject localPanel = FindObject(frame, "LP do Player");
            GameObject opponentPanel = FindObject(frame, "LP do Oponente");
            localLife = FindLifeValue(localPanel);
            opponentLife = FindLifeValue(opponentPanel);
            BindLocalPlayerName(localPanel);

            GameObject phasePanel = FindObject(frame, "Controle de Fases");
            if (phasePanel != null)
            {
                Button[] buttons =
                    phasePanel.GetComponentsInChildren<Button>(true);
                phaseButton = buttons.FirstOrDefault(button =>
                                  Contains(button.name, "Avan")) ??
                              buttons.FirstOrDefault() ??
                              phasePanel.GetComponent<Button>() ??
                              phasePanel.AddComponent<Button>();
                if (phaseButton != null)
                {
                    phaseButton.targetGraphic =
                        phaseButton.GetComponent<Graphic>() ??
                        phasePanel.GetComponent<Graphic>();
                    phaseButton.interactable = true;
                    phaseButton.onClick.RemoveAllListeners();
                    phaseButton.onClick.AddListener(OpenPhaseChoices);
                    phaseLabel =
                        phaseButton.GetComponentInChildren<Text>(true);
                }
            }

            Text[] allTexts = frame.GetComponentsInChildren<Text>(true);
            status = allTexts.FirstOrDefault(text =>
                text.text.IndexOf(
                    "Escolha um modo de duelo",
                    StringComparison.OrdinalIgnoreCase) >= 0);
            if (status == null)
            {
                status = CreateText(
                    frame,
                    "Status do Duelo",
                    16,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.31f, 0.018f),
                    new Vector2(0.69f, 0.075f),
                    TextAnchor.MiddleCenter);
            }
        }

        private void BindLocalPlayerName(GameObject localPanel)
        {
            if (localPanel == null)
                return;

            localPlayerName =
                localPanel.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(text =>
                        string.Equals(
                            text.text?.Trim(),
                            "PLAYER",
                            StringComparison.OrdinalIgnoreCase));
            if (localPlayerName == null)
            {
                localPlayerName = CreateText(
                    localPanel.transform,
                    "Nome do Duelista",
                    14,
                    FontStyle.Bold,
                    Gold,
                    new Vector2(0.06f, 0.70f),
                    new Vector2(0.94f, 0.96f),
                    TextAnchor.MiddleLeft);
            }

            UpdateLocalPlayerName();
        }

        private void UpdateLocalPlayerName()
        {
            if (localPlayerName == null)
                return;

            localPlayerName.text =
                $"DUELISTA • {localPlayerDisplayName.ToUpperInvariant()}";
        }

        private void ClearAuthoredPreviewCards()
        {
            foreach (CardView card in
                     GetComponentsInChildren<CardView>(true))
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            foreach (DuelZone3D zone in
                     FindObjectsByType<DuelZone3D>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                zone.EnsureIdentityFromHierarchy(false);
                ClearWorldCard(zone);
                zone.ClearPlacedCard();
                zone.SetDropHighlight(false);
            }
        }

        private void RebuildHand()
        {
            ulong selectedRuntimeId = selectedCard != null
                ? selectedCard.InstanceKey.RuntimeId
                : 0;
            ulong hoveredRuntimeId = hoveredCard != null
                ? hoveredCard.InstanceKey.RuntimeId
                : 0;
            Dictionary<ulong, CardView> existing = handViews
                .Where(card =>
                    card != null &&
                    card.InstanceKey.RuntimeId != 0)
                .GroupBy(card => card.InstanceKey.RuntimeId)
                .ToDictionary(group => group.Key, group => group.First());
            var obsolete = new HashSet<CardView>(
                handViews.Where(card => card != null));
            handViews.Clear();
            selectedCard = null;
            hoveredCard = null;
            actionPanel?.SetActive(false);

            List<uint> cards = state.Players[0].Hand;
            for (int index = 0; index < cards.Count; index++)
            {
                uint code = cards[index];
                CardInstanceState instance =
                    state.InstanceAt(
                        0,
                        (byte)DuelLocation.Hand,
                        (uint)index);
                CardInstanceKey key = instance != null
                    ? instance.Key
                    : new CardInstanceKey(
                        0,
                        code,
                        0,
                        0,
                        (byte)DuelLocation.Hand,
                        (uint)index,
                        0);
                CardView card;
                if (key.RuntimeId != 0 &&
                    existing.TryGetValue(key.RuntimeId, out CardView reused) &&
                    reused != null)
                {
                    card = reused;
                    card.Rebind(key, index);
                    obsolete.Remove(card);
                }
                else
                {
                    card = CreateHandCard(key, index);
                }
                card.transform.SetSiblingIndex(index);
                handViews.Add(card);
                card.SetRestPose(
                    HandPosition(index, cards.Count),
                    HandAngle(index, cards.Count));
                if (key.RuntimeId == selectedRuntimeId &&
                    selectedRuntimeId != 0)
                {
                    selectedCard = card;
                    card.SetSelected(true);
                }
                if (key.RuntimeId == hoveredRuntimeId &&
                    hoveredRuntimeId != 0)
                {
                    hoveredCard = card;
                }
            }
            foreach (CardView card in obsolete)
            {
                card.gameObject.SetActive(false);
                Destroy(card.gameObject);
            }
            RelayoutHand();
            RefreshHandLegalGlows();
            SetHandPlacementMode(
                core?.CurrentPrompt != null &&
                core.CurrentPrompt.Player == 0 &&
                (core.CurrentPrompt.Message == CoreMessage.SelectPlace ||
                 core.CurrentPrompt.Message == CoreMessage.SelectDisableField));
        }

        private CardView CreateHandCard(
            CardInstanceKey instanceKey,
            int index)
        {
            uint code = instanceKey.DefinitionCode;
            var root = new GameObject(
                $"Hand_{code:00000000}_{instanceKey.RuntimeId}_{index}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Outline),
                typeof(CanvasGroup),
                typeof(CardView));
            root.transform.SetParent(handRoot, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = handLayoutAnchor != null
                ? handLayoutAnchor.CardSize
                : new Vector2(178f, 258f);
            rect.pivot = new Vector2(0.5f, 0f);
            Image image = root.GetComponent<Image>();
            image.sprite = SpriteFor(code);
            image.preserveAspect = true;
            image.raycastTarget = true;
            CardView view = root.GetComponent<CardView>();
            view.Setup(this, instanceKey, image.sprite, index);
            if (handLayoutAnchor != null)
            {
                view.ConfigureHandMotion(
                    handLayoutAnchor.SelectedLift,
                    handLayoutAnchor.HoverLift,
                    handLayoutAnchor.SelectedScale,
                    handLayoutAnchor.HoverScale,
                    handLayoutAnchor.AnimationDuration);
            }
            return view;
        }

        public void NotifyHandHoverChanged(CardView card, bool hovered)
        {
            if (hovered) hoveredCard = card;
            else if (hoveredCard == card) hoveredCard = null;
            RelayoutHand();
            if (hovered && card != null) ShowInspector(card.Code);
        }

        public void SelectCard(CardView card)
        {
            if (card == null) return;
            if (InteractionLocked)
            {
                SetStatus(
                    InteractionLockStatus(
                        "Aguarde a conclusao da animacao confirmada pelo Core."),
                    Muted);
                return;
            }
            CloseFieldActionMenu();
            DuelPrompt prompt = core.CurrentPrompt;
            DuelChoice direct = ChoiceForCard(
                prompt,
                card.InstanceKey);
            if (direct != null &&
                IsDirectSelectionPrompt(prompt))
            {
                SubmitSelectionChoice(direct);
                return;
            }

            if (selectedCard != null && selectedCard != card)
                selectedCard.SetSelected(false);
            selectedCard = card;
            selectedCard.SetSelected(true);
            ShowInspector(card.Code);
            RelayoutHand();
            ShowActionsForSelectedCard();
            SetStatus(
                ChoicesForCard(prompt, card.InstanceKey).Count > 0
                    ? $"Carta selecionada - {CardName(card.Code)}"
                    : UnavailableCardReason(prompt),
                ChoicesForCard(prompt, card.InstanceKey).Count > 0
                    ? Cyan
                    : Muted);
        }

        private static string UnavailableCardReason(DuelPrompt prompt)
        {
            if (prompt == null)
                return "O Core esta processando o estado do duelo.";
            if (prompt.Player != 0)
                return "A prioridade pertence ao oponente.";
            if (prompt.Message == CoreMessage.SelectChain)
                return prompt.Forced
                    ? "Resolva primeiro a resposta obrigatoria da corrente."
                    : "Conclua primeiro a janela de resposta da corrente.";
            if (prompt.Message != CoreMessage.SelectIdleCommand &&
                prompt.Message != CoreMessage.SelectBattleCommand)
            {
                return
                    $"Resolva primeiro: {prompt.Title}. " +
                    "Esta carta nao e uma resposta legal nesta etapa.";
            }
            return
                "O Core nao ofereceu uma acao legal para esta carta " +
                "nesta fase e neste estado do campo.";
        }

        public void BeginCardDrag(CardView card)
        {
            if (card == null || InteractionLocked) return;
            SelectCard(card);
            ShowActionsForSelectedCard();
        }

        public void EndCardDrag(Vector2 screenPosition)
        {
            if (InteractionLocked) return;
            DuelPrompt prompt = core.CurrentPrompt;
            if (prompt == null || selectedCard == null) return;
            if (TryRaycastZone(screenPosition, out DuelZone3D zone))
            {
                HandleZoneClick(zone, 1);
            }
        }

        private void RelayoutHand()
        {
            int count = handViews.Count;
            int focusIndex = selectedCard != null
                ? handViews.IndexOf(selectedCard)
                : hoveredCard != null
                    ? handViews.IndexOf(hoveredCard)
                    : -1;
            float separation = handLayoutAnchor != null
                ? handLayoutAnchor.FocusSeparationFor(count)
                : Mathf.Clamp(
                    430f -
                    (count <= 1
                        ? 0f
                        : Mathf.Min(96f * (count - 1), 720f)) * 0.5f,
                    70f,
                    132f);
            for (int index = 0; index < count; index++)
            {
                Vector2 position = HandPosition(index, count);
                if (focusIndex >= 0 && index != focusIndex)
                    position.x += index < focusIndex
                        ? -separation
                        : separation;
                handViews[index].SetHandOrder(index);
                handViews[index].SetRestPose(
                    position,
                    HandAngle(index, count));
            }
            selectedCard?.transform.SetAsLastSibling();
        }

        private Vector2 HandPosition(int index, int count)
        {
            if (handLayoutAnchor != null)
                return handLayoutAnchor.PositionFor(index, count);
            float center = (count - 1) * 0.5f;
            float spacing = count <= 1
                ? 0f
                : Mathf.Min(96f, 720f / (count - 1));
            return new Vector2((index - center) * spacing, 0f);
        }

        private float HandAngle(int index, int count)
        {
            if (handLayoutAnchor != null)
                return handLayoutAnchor.AngleFor(index, count);
            if (count <= 1) return 0f;
            float center = (count - 1) * 0.5f;
            return ((index - center) / Mathf.Max(1f, center)) * -6f;
        }

        private void ShowActionsForSelectedCard()
        {
            if (InteractionLocked)
            {
                actionPanel?.SetActive(false);
                return;
            }
            if (selectedCard == null || actionPanel == null) return;
            DuelPrompt prompt = core.CurrentPrompt;
            List<DuelChoice> choices =
                ChoicesForCard(
                    prompt,
                    selectedCard.InstanceKey);
            bool canActivate = choices.Any(choice =>
                Contains(choice.Label, "Ativar"));
            bool canSummon = choices.Any(IsSummonChoice);
            bool canSet = choices.Any(choice =>
                Contains(choice.Label, "Baixar"));
            activateAction.SetActive(canActivate);
            summonAction.SetActive(canSummon);
            setAction.SetActive(canSet);
            LayoutActionButtons(
                new[] { activateAction, summonAction, setAction }
                    .Where(button => button.activeSelf)
                    .ToList());
            actionPanel.SetActive(canActivate || canSummon || canSet);
            UpdateCardActionPresentation();
        }

        private void SubmitActivateAction()
        {
            SubmitSelectedAction("Ativar");
        }

        private void SubmitSummonAction()
        {
            SubmitSelectedAction("Invocar");
        }

        private void SubmitSetAction()
        {
            SubmitSelectedAction("Baixar");
        }

        private void SubmitSelectedAction(string label)
        {
            if (selectedCard == null) return;
            DuelChoice choice = ChoicesForCard(
                    core.CurrentPrompt,
                    selectedCard.InstanceKey)
                .FirstOrDefault(candidate =>
                    label == "Invocar"
                        ? IsSummonChoice(candidate)
                        : Contains(candidate.Label, label));
            if (choice == null) return;
            actionPanel.SetActive(false);
            ClearHandSelection();
            core.SubmitChoice(choice);
            RefreshEverything(true);
        }

        private void ClearHandSelection()
        {
            if (selectedCard != null)
                selectedCard.SetSelected(false);
            selectedCard = null;
            hoveredCard = null;
            if (actionPanel != null)
                actionPanel.SetActive(false);
            RelayoutHand();
        }

        private static float CalculateHandRestY(float viewportHeight)
        {
            float visibleHeight = Mathf.Clamp(
                viewportHeight * HandVisibleViewportRatio,
                HandMinimumVisibleHeight,
                HandMaximumVisibleHeight);
            return visibleHeight - HandCardHeight - HandLowerViewportOffset;
        }

        private void ApplyResponsiveHandLayout(bool force)
        {
            if (frame == null || handRoot == null)
                return;

            Vector2 viewportSize = frame.rect.size;
            if (viewportSize.x <= 1f || viewportSize.y <= 1f)
                return;
            if (!force &&
                (viewportSize - lastHandViewportSize).sqrMagnitude < 0.25f)
            {
                return;
            }

            lastHandViewportSize = viewportSize;
            if (handLayoutAnchor == null)
            {
                handRestPosition = new Vector2(
                    0f,
                    CalculateHandRestY(viewportSize.y));
            }
            handRoot.anchoredPosition =
                handRestPosition +
                (handPlacementMode
                    ? handLayoutAnchor != null
                        ? handLayoutAnchor.PlacementModeOffset
                        : new Vector2(0f, -136f)
                    : Vector2.zero);
            handRoot.localScale =
                handRestScale * (handPlacementMode
                    ? handLayoutAnchor != null
                        ? handLayoutAnchor.PlacementModeScale
                        : 0.70f
                    : 1f);
        }

        private void SetHandPlacementMode(bool placement)
        {
            handPlacementMode = placement;
            bool disabled = placement || InteractionLocked;
            ApplyResponsiveHandLayout(true);
            if (handRoot != null)
            {
                handRoot.localScale =
                    handRestScale * (placement
                        ? handLayoutAnchor != null
                            ? handLayoutAnchor.PlacementModeScale
                            : 0.70f
                        : 1f);
            }
            if (handInteractionGroup != null)
            {
                handInteractionGroup.interactable = !disabled;
                handInteractionGroup.blocksRaycasts = !disabled;
                handInteractionGroup.alpha = placement
                    ? 0.36f
                    : phasePresentationLocked
                        ? 0.72f
                        : 1f;
            }
            foreach (CardView card in handViews)
                card?.SetInteraction(!disabled);
            if (disabled)
                ClearHandSelection();
        }

        private void ScheduleAutomaticPromptChoice(
            DuelPrompt prompt,
            DuelChoice choice,
            string message)
        {
            if (prompt == null || choice == null ||
                SamePromptIdentity(scheduledAutomaticPrompt, prompt))
            {
                return;
            }

            if (automaticPromptRoutine != null)
                StopCoroutine(automaticPromptRoutine);
            scheduledAutomaticPrompt = prompt;
            SetStatus(message, Muted);
            automaticPromptRoutine = StartCoroutine(
                SubmitAutomaticPromptChoice(prompt, choice));
        }

        private IEnumerator SubmitAutomaticPromptChoice(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            yield return null;
            while (InteractionLocked &&
                   core != null &&
                   SamePromptIdentity(core.CurrentPrompt, prompt))
            {
                yield return null;
            }
            if (core != null &&
                SamePromptIdentity(core.CurrentPrompt, prompt))
            {
                core.SubmitChoice(choice);
                observedPrompt = null;
                RefreshEverything(true);
            }
            scheduledAutomaticPrompt = null;
            automaticPromptRoutine = null;
        }

        private void RefreshHandLegalGlows()
        {
            DuelPrompt prompt = core.CurrentPrompt;
            foreach (CardView card in handViews)
            {
                List<DuelChoice> choices = ChoicesForCard(
                    prompt,
                    card.InstanceKey);
                bool legal = !InteractionLocked && choices.Count > 0;
                bool canNormalSummon =
                    legal && choices.Any(IsNormalSummonChoice);
                bool canSpecialSummon =
                    legal && choices.Any(IsSpecialSummonChoice);
                bool canActivate = legal && choices.Any(choice =>
                    IsEffectActivationChoice(prompt, choice));
                bool effectAction = canSpecialSummon || canActivate;
                card.SetLegalActionGlow(
                    effectAction || !canNormalSummon
                        ? EffectGlow
                        : SummonBlue,
                    legal);
            }
        }

        private static bool IsSummonChoice(DuelChoice choice)
        {
            return choice != null &&
                   Contains(choice.Label, "Invoc") &&
                   !Contains(choice.Label, "Ativar");
        }

        private static bool IsSpecialSummonChoice(DuelChoice choice)
        {
            return IsSummonChoice(choice) &&
                   Contains(choice.Label, "especial");
        }

        private static bool IsNormalSummonChoice(DuelChoice choice)
        {
            return IsSummonChoice(choice) &&
                   !IsSpecialSummonChoice(choice);
        }

        private static bool IsEffectActivationChoice(
            DuelPrompt prompt,
            DuelChoice choice)
        {
            if (choice == null)
                return false;
            if (Contains(choice.Label, "Ativar"))
                return true;
            return prompt != null &&
                   (prompt.Message == CoreMessage.SelectChain ||
                    prompt.Message == CoreMessage.SelectEffectYesNo ||
                    prompt.Message == CoreMessage.SelectYesNo);
        }

        private bool RefreshPrompt(DuelPrompt prompt)
        {
            ClearZoneHighlights();
            HideCompactResponseBar();
            CloseChoiceModal();
            CloseFieldActionMenu();
            CloseZoneBrowser();
            ClosePhaseNavigator();
            if (phasePresentationLocked)
            {
                SetHandPlacementMode(false);
                ClearHandSelection();
                if (phaseButton != null)
                    phaseButton.interactable = false;
                SetStatus(
                    "Aguarde a apresentação da fase atual.",
                    PhaseAccent(presentationPhaseOverride ?? state.Phase));
                return false;
            }
            UpdateDuelExperienceForPrompt(prompt);
            bool placementPrompt =
                prompt != null &&
                prompt.Player == 0 &&
                (prompt.Message == CoreMessage.SelectPlace ||
                 prompt.Message == CoreMessage.SelectDisableField);
            SetHandPlacementMode(placementPrompt);
            RefreshHandLegalGlows();
            ShowActionsForSelectedCard();
            if (prompt == null)
            {
                SetStatus("Aguardando o ygopro-core...", Muted);
                return true;
            }

            if (prompt.Player == 1)
            {
                ClearHandSelection();
                bool online =
                    Multiplayer.DuelOnlineSession.Instance
                        ?.IsOnlineDuelActive == true;
                SetStatus(
                    online
                        ? "TURNO DO OPONENTE · aguardando a decisão do outro jogador."
                        : "TURNO DA IA · o adversário está analisando uma ação válida.",
                    Gold);
                return true;
            }

            if (prompt.Player == 0 &&
                DuelPromptPresentationRules.ShouldAutoPassEmptyChain(prompt))
            {
                ScheduleAutomaticPromptChoice(
                    prompt,
                    prompt.Choices[0],
                    "Nenhuma resposta disponível · continuando automaticamente.");
                return true;
            }

            switch (prompt.Message)
            {
                case CoreMessage.SelectIdleCommand:
                    HighlightPromptZones(prompt);
                    int legalExtraSummons = prompt.Choices.Count(choice =>
                        choice.HasLocation &&
                        choice.Controller == 0 &&
                        (choice.Location & DuelLocation.Extra) != 0);
                    if (legalExtraSummons > 0)
                    {
                        SetStatus(
                            $"DECK ADICIONAL DISPONÍVEL · {legalExtraSummons} invocação(ões) legal(is) · clique no monte iluminado.",
                            EffectGlow);
                    }
                    else
                    {
                        SetStatus(
                            "Selecione uma carta da mão ou do campo para ver somente ações legais.",
                            Cyan);
                    }
                    break;
                case CoreMessage.SelectBattleCommand:
                    HighlightPromptZones(prompt);
                    SetStatus(
                        "Selecione um monstro iluminado para declarar o ataque.",
                        Cyan);
                    break;
                case CoreMessage.SelectPlace:
                case CoreMessage.SelectDisableField:
                    HighlightPromptZones(prompt);
                    SetStatus(
                        prompt.MaximumSelections > 1
                            ? $"Escolha {prompt.MaximumSelections} zonas iluminadas · 0/{prompt.MaximumSelections}."
                            : "Escolha uma zona iluminada · as cartas da mão foram recolhidas para liberar o campo.",
                        Cyan);
                    break;
                case CoreMessage.SelectCard:
                case CoreMessage.SelectTribute:
                case CoreMessage.SelectSum:
                case CoreMessage.SelectUnselectCard:
                    HighlightPromptZones(prompt);
                    if (prompt.Choices.Count > 0)
                        OpenChoiceModal(prompt, prompt.Choices);
                    SetStatus(prompt.Title, Gold);
                    break;
                case CoreMessage.SelectChain:
                case CoreMessage.SelectEffectYesNo:
                case CoreMessage.SelectYesNo:
                    HighlightPromptZones(prompt);
                    if (DuelPromptPresentationRules
                        .ShouldUseCompactResponseBar(prompt))
                    {
                        ShowCompactResponseBar(prompt);
                        SetStatus(
                            "VOCÊ PODE RESPONDER · escolha RESPONDER ou PASSAR.",
                            EffectGlow);
                    }
                    else
                    {
                        OpenChoiceModal(prompt, prompt.Choices);
                        SetStatus(prompt.Title, EffectGlow);
                    }
                    break;
                default:
                    OpenChoiceModal(prompt, prompt.Choices);
                    SetStatus(prompt.Title, Gold);
                    break;
            }
            return true;
        }

        public void HandleZoneClick(DuelZone3D zone, int clickCount)
        {
            if (zone == null || core == null) return;
            if (TryHandleDrawDeckClick(zone))
                return;
            if (InteractionLocked)
            {
                SetStatus(
                    InteractionLockStatus(
                        "Aguarde a conclusao da animacao confirmada pelo Core."),
                    Muted);
                return;
            }
            DuelPrompt prompt = core.CurrentPrompt;
            if (zone.Kind == DuelZoneKind.ExtraDeck &&
                IsLocalZone(zone))
            {
                OpenZoneChoices(zone, prompt);
                return;
            }
            if (prompt == null)
            {
                InspectZone(zone);
                return;
            }

            byte controller = StatePlayerForZone(zone);
            byte location = LocationFor(zone.Kind);
            int sequence = SequenceFor(zone);
            bool contextualCommand =
                prompt.Message == CoreMessage.SelectIdleCommand ||
                prompt.Message == CoreMessage.SelectBattleCommand;

            DuelChoice direct = prompt.Choices.FirstOrDefault(choice =>
                choice.HasLocation &&
                choice.Controller == controller &&
                (choice.Location & location) != 0 &&
                choice.Sequence == sequence);
            if (direct != null && !contextualCommand)
            {
                if (IsMultiPlacePrompt(prompt))
                    StagePlaceChoice(prompt, direct);
                else if (IsDirectSelectionPrompt(prompt))
                    SubmitSelectionChoice(direct);
                else
                    core.SubmitChoice(direct);
                if (!IsMultiPlacePrompt(prompt))
                    RefreshEverything(true);
                return;
            }

            if (contextualCommand)
            {
                uint code = CodeAt(zone);
                List<DuelChoice> cardChoices =
                    ChoicesForCard(
                        prompt,
                        code,
                        controller,
                        location,
                        sequence);
                if (cardChoices.Count > 0)
                {
                    ShowInspector(zone);
                    OpenFieldActionMenu(zone, prompt, cardChoices);
                    return;
                }
            }

            if (IsSpecialZone(zone.Kind))
            {
                OpenZoneChoices(zone, prompt);
                return;
            }
            uint inspected = CodeAt(zone);
            if (inspected != 0) ShowInspector(zone);
        }

        public void HandleZoneHover(DuelZone3D zone, bool hovered)
        {
            if (!hovered || zone == null || !presentationReady)
                return;
            if (!zone.HasValidIdentity &&
                !zone.EnsureIdentityFromHierarchy(false))
            {
                return;
            }
            uint code = CodeAt(zone);
            if (code != 0)
                ShowInspector(zone);
            else if (IsSpecialZone(zone.Kind))
                SetStatus(PileLabel(zone), Muted);
        }

        private void HighlightPromptZones(DuelPrompt prompt)
        {
            if (prompt == null) return;
            var highlighted = new Dictionary<DuelZone3D, bool>();
            foreach (DuelChoice choice in prompt.Choices)
            {
                if (!choice.HasLocation) continue;
                DuelZone3D zone = FindZone(
                    choice.Controller,
                    choice.Location,
                    (int)choice.Sequence);
                if (zone == null)
                    continue;
                bool effectAccent =
                    (IsMultiPlacePrompt(prompt) &&
                     selectedPromptIndexes.Contains(choice.ChoiceIndex)) ||
                    (choice.Location & DuelLocation.Graveyard) != 0 ||
                    (choice.Location & DuelLocation.Extra) != 0 ||
                    IsEffectActivationChoice(prompt, choice);
                if (!highlighted.TryGetValue(zone, out bool existing) ||
                    effectAccent)
                {
                    highlighted[zone] = existing || effectAccent;
                }
            }
            foreach (KeyValuePair<DuelZone3D, bool> item in highlighted)
            {
                item.Key.SetDropHighlight(
                    true,
                    item.Value ? EffectGlow : SummonBlue);
            }
        }

        private void ReconcileField()
        {
            foreach (DuelZone3D zone in AllZones())
            {
                if (zone == null)
                    continue;
                if (!zone.HasValidIdentity &&
                    !zone.EnsureIdentityFromHierarchy(false))
                {
                    continue;
                }
                uint code = CodeAt(zone);
                uint position = PositionAt(zone);
                CardInstanceState instance = InstanceAt(zone);
                bool occupied = code != 0 || instance != null;
                CardInstanceKey key = instance != null
                    ? instance.Key
                    : new CardInstanceKey(
                        occupied ? SyntheticZoneRuntimeId(zone) : 0UL,
                        code,
                        StatePlayerForZone(zone),
                        StatePlayerForZone(zone),
                        LocationFor(zone.Kind),
                        (uint)Mathf.Max(0, SequenceFor(zone)),
                        position);
                renderedZones.TryGetValue(
                    zone.StableId,
                    out CardInstanceKey previous);
                if (key == previous &&
                    HasWorldCardRepresentation(zone, key, code, occupied))
                {
                    ApplyWorldPosition(zone, code, position);
                    continue;
                }

                ClearWorldCard(zone);
                zone.ClearPlacedCard();
                renderedZones[zone.StableId] = key;
                if (!occupied) continue;

                bool faceUp = IsFaceUp(position);
                Sprite sprite = code == 0 && cardBackSprite != null
                    ? cardBackSprite
                    : SpriteFor(code);
                zone.SetPlacedCard(
                    sprite,
                    code == 0 ? "HIDDEN" : code.ToString("00000000"),
                    faceUp);
                if ((position & (FaceUpDefense | FaceDownDefense)) != 0)
                {
                    zone.SetMonsterPosition(
                        faceUp
                            ? DuelMonsterPosition.FaceUpDefense
                            : DuelMonsterPosition.FaceDownDefense);
                }
                CreateWorldCard(zone, key, sprite, position);
            }
        }

        private CardInstanceState InstanceAt(DuelZone3D zone)
        {
            if (zone == null || state == null)
                return null;
            byte controller = StatePlayerForZone(zone);
            byte location = LocationFor(zone.Kind);
            if (zone.Kind == DuelZoneKind.Graveyard)
            {
                List<CardInstanceState> instances =
                    state.Players[controller].GraveyardInstances;
                return instances.Count > 0
                    ? instances[instances.Count - 1]
                    : null;
            }
            if (zone.Kind == DuelZoneKind.Banishment)
            {
                List<CardInstanceState> instances =
                    state.Players[controller].BanishedInstances;
                return instances.Count > 0
                    ? instances[instances.Count - 1]
                    : null;
            }
            return state.InstanceAt(
                controller,
                location,
                (uint)Mathf.Max(0, SequenceFor(zone)));
        }

        private ulong SyntheticZoneRuntimeId(DuelZone3D zone)
        {
            unchecked
            {
                return 0xF000000000000000UL |
                       ((ulong)StatePlayerForZone(zone) << 48) |
                       ((ulong)(byte)zone.Kind << 40) |
                       (uint)Mathf.Max(0, SequenceFor(zone)) + 1UL;
            }
        }

        private static bool HasWorldCardRepresentation(
            DuelZone3D zone,
            CardInstanceKey key,
            uint code,
            bool occupied)
        {
            Transform card = zone?.FindPresentedCard();
            if (!occupied)
                return card == null;
            if (card == null || !card.gameObject.activeInHierarchy)
                return false;
            WorldCardInstanceView view =
                card.GetComponent<WorldCardInstanceView>();
            if (view == null || !view.IsVisuallyReady)
                return false;
            // A identidade adversária com a face para baixo é opaca (código
            // 0), mas o verso ainda precisa existir no campo.
            if (code == 0)
            {
                return view.InstanceKey.DefinitionCode == 0 &&
                       (!key.IsValid ||
                        !view.InstanceKey.IsValid ||
                        view.InstanceKey.RuntimeId == key.RuntimeId);
            }
            return view.InstanceKey.DefinitionCode == code &&
                   (!key.IsValid ||
                    !view.InstanceKey.IsValid ||
                    view.InstanceKey.RuntimeId == key.RuntimeId);
        }

        private string[] ValidatePresentationConsistency(
            DuelEvent cause,
            bool repair)
        {
            if (state == null)
                return new[] { "Presentation state is unavailable." };
            var problems = new List<string>(
                state.ValidateInstanceConsistency());
            foreach (DuelZone3D zone in AllZones())
            {
                if (zone == null || !zone.HasValidIdentity)
                    continue;
                uint code = CodeAt(zone);
                CardInstanceState instance = InstanceAt(zone);
                bool occupied = code != 0 || instance != null;
                if (!occupied)
                    continue;
                CardInstanceKey key = instance != null
                    ? instance.Key
                    : new CardInstanceKey(
                        SyntheticZoneRuntimeId(zone),
                        code,
                        StatePlayerForZone(zone),
                        StatePlayerForZone(zone),
                        LocationFor(zone.Kind),
                        (uint)Mathf.Max(0, SequenceFor(zone)),
                        PositionAt(zone));
                if (!HasWorldCardRepresentation(zone, key, code, occupied))
                {
                    problems.Add(
                        $"{zone.StableId} has authoritative card " +
                        $"{code:00000000} but no matching world view.");
                    if (repair)
                        renderedZones.Remove(zone.StableId);
                }
            }
            if (repair && problems.Any(problem =>
                    problem.IndexOf(
                        "world view",
                        StringComparison.OrdinalIgnoreCase) >= 0))
            {
                ReconcileField();
            }
            if (problems.Count > 0)
            {
                DuelDevelopmentLog.Write(
                    DuelLogCategory.StateSync,
                    $"event={cause?.Message.ToString() ?? "manual"}; " +
                    string.Join(" | ", problems),
                    this);
            }
            return problems.ToArray();
        }

        private void CreateWorldCard(
            DuelZone3D zone,
            CardInstanceKey instanceKey,
            Sprite sprite,
            uint position)
        {
            uint code = instanceKey.DefinitionCode;
            bool faceUp = IsFaceUp(position);
            bool monster = zone.Kind == DuelZoneKind.Monster;
            var canvasObject = new GameObject(
                "Carta Invocada",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster),
                typeof(WorldCardInstanceView));
            canvasObject.transform.SetParent(
                zone.CardPresentationAnchor,
                false);
            canvasObject.transform.localPosition = Vector3.zero;
            canvasObject.transform.localRotation =
                CardRotation(monster, position);
            Vector3 finalScale = Vector3.one * 0.00745f;
            canvasObject.transform.localScale =
                Application.isPlaying
                    ? finalScale * 0.18f
                    : finalScale;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 2;
            canvasObject.GetComponent<RectTransform>().sizeDelta =
                new Vector2(178f, 258f);

            Image back = CreateImage(
                canvasObject.transform,
                "Verso",
                Vector2.zero,
                Vector2.one,
                Color.white);
            back.sprite = cardBackSprite;
            back.preserveAspect = true;
            back.gameObject.SetActive(!faceUp);

            Image front = CreateImage(
                canvasObject.transform,
                "Frente",
                Vector2.zero,
                Vector2.one,
                Color.white);
            front.sprite = sprite;
            front.preserveAspect = true;
            front.gameObject.SetActive(faceUp || cardBackSprite == null);
            canvasObject.GetComponent<WorldCardInstanceView>()
                .Bind(instanceKey, faceUp);

            if (monster && faceUp)
                CreateCombatLabel(zone, code, position);
            if (Application.isPlaying)
                StartCoroutine(
                    AnimateWorldArrival(
                        canvasObject.transform,
                        finalScale));
        }

        private void CreateCombatLabel(
            DuelZone3D zone,
            uint code,
            uint position)
        {
            var root = new GameObject(
                "Indicador de ATK",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CameraFacingCardLabel));
            root.transform.SetParent(zone.CombatLabelAnchor, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localScale = Vector3.one * 0.0055f;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 8;
            root.GetComponent<RectTransform>().sizeDelta =
                new Vector2(300f, 76f);
            Text text = CreateText(
                root.transform,
                CombatLabelFor(zone, code, position),
                46,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private IEnumerator AnimateWorldArrival(
            Transform card,
            Vector3 finalScale)
        {
            float duration =
                DuelAnimationPreferences.MonsterDuration(0.34f);
            float elapsed = 0f;
            Vector3 start = finalScale * 0.18f;
            while (elapsed < duration && card != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                float overshoot = Mathf.Sin(t * Mathf.PI) * 0.16f;
                card.localScale =
                    Vector3.Lerp(start, finalScale, eased) *
                    (1f + overshoot);
                yield return null;
            }
            if (card != null) card.localScale = finalScale;
        }

        private void ApplyWorldPosition(
            DuelZone3D zone,
            uint code,
            uint position)
        {
            Transform card = zone.FindPresentedCard();
            if (card == null) return;
            card.localRotation = CardRotation(
                zone.Kind == DuelZoneKind.Monster,
                position);
            Transform front = card.Find("Frente");
            Transform back = card.Find("Verso");
            bool faceUp = IsFaceUp(position);
            if (front != null)
                front.gameObject.SetActive(faceUp || back == null);
            if (back != null) back.gameObject.SetActive(!faceUp);
            WorldCardInstanceView view =
                card.GetComponent<WorldCardInstanceView>();
            if (view != null)
                view.Bind(view.InstanceKey, faceUp);
            RefreshCombatLabel(zone, code, position, faceUp);
        }

        private void RefreshCombatLabel(
            DuelZone3D zone,
            uint code,
            uint position,
            bool faceUp)
        {
            Transform root = zone.FindCombatLabel();
            if (root == null)
            {
                if (faceUp && IsMonster(code))
                    CreateCombatLabel(zone, code, position);
                return;
            }
            root.gameObject.SetActive(faceUp && IsMonster(code));
            Text text = root.GetComponentInChildren<Text>(true);
            if (text != null)
                text.text = CombatLabelFor(zone, code, position);
        }

        private string CombatLabelFor(
            DuelZone3D zone,
            uint code,
            uint position)
        {
            bool defensePosition =
                (position & (FaceUpDefense | FaceDownDefense)) != 0;
            if (TryGetCombatStats(zone, code, out int attack, out int defense))
            {
                int value = defensePosition ? defense : attack;
                string prefix = defensePosition ? "DEF" : "ATK";
                return $"{prefix} {(value < 0 ? "?" : value.ToString())}";
            }
            return defensePosition ? "DEF —" : "ATK —";
        }

        private bool TryGetCombatStats(
            DuelZone3D zone,
            uint code,
            out int attack,
            out int defense)
        {
            if (zone != null &&
                zone.Kind == DuelZoneKind.Monster &&
                core != null &&
                core.TryGetCurrentCombatStats(
                    StatePlayerForZone(zone),
                    LocationFor(zone.Kind),
                    (uint)Mathf.Max(0, SequenceFor(zone)),
                    out attack,
                    out defense))
            {
                return true;
            }
            if (database != null &&
                database.TryGet(code, out CardRecord card))
            {
                attack = card.Attack;
                defense = card.Defense;
                return true;
            }
            attack = 0;
            defense = 0;
            return false;
        }

        private static Quaternion CardRotation(bool monster, uint position)
        {
            bool defense = monster &&
                           (position &
                            (FaceUpDefense | FaceDownDefense)) != 0;
            return defense
                ? Quaternion.Euler(90f, 0f, 90f)
                : Quaternion.Euler(90f, 0f, 0f);
        }

        private void ClearWorldCard(DuelZone3D zone)
        {
            foreach (Transform child in
                     new[]
                     {
                         zone.FindPresentedCard(),
                         zone.FindCombatLabel()
                     })
            {
                if (child == null) continue;
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        private ulong BuildHandSignature()
        {
            ulong hash = 1469598103934665603UL;
            foreach (CardInstanceState instance in
                     state.Players[0].HandInstances)
            {
                hash = MixSignature(
                    hash,
                    instance?.RuntimeId ?? 0UL);
            }
            return MixSignature(
                hash,
                (ulong)state.Players[0].HandInstances.Count);
        }

        private ulong BuildFieldSignature()
        {
            ulong hash = 1469598103934665603UL;
            for (byte player = 0; player < 2; player++)
            {
                DuelistState duelist = state.Players[player];
                for (int index = 0;
                     index < duelist.MonsterZones.Length;
                     index++)
                {
                    hash = MixSignature(hash, duelist.MonsterZones[index]);
                    hash = MixSignature(
                        hash,
                        duelist.MonsterPositions[index]);
                }
                for (int index = 0;
                     index < duelist.SpellTrapZones.Length;
                     index++)
                {
                    hash = MixSignature(hash, duelist.SpellTrapZones[index]);
                    hash = MixSignature(
                        hash,
                        duelist.SpellTrapPositions[index]);
                }
            }
            return hash;
        }

        private static ulong MixSignature(ulong hash, ulong value)
        {
            return (hash ^ value) * 1099511628211UL;
        }

        private void UpdateLifeAndPhase()
        {
            if (localLife != null)
                localLife.text = state.Players[0].LifePoints.ToString("N0");
            if (opponentLife != null)
                opponentLife.text = state.Players[1].LifePoints.ToString("N0");
            if (phaseLabel != null)
            {
                uint displayedPhase =
                    presentationPhaseOverride ?? state.Phase;
                int phaseTransitions =
                    phasePresentationLocked
                        ? 0
                        : DuelPromptPresentationRules.PhaseChoices(
                            core?.CurrentPrompt).Count;
                phaseLabel.text =
                    $"TURNO {Mathf.Max(1, state.TurnNumber)}\n" +
                    CoreMessageDecoder.PhaseName(displayedPhase).ToUpperInvariant() +
                    (phaseTransitions > 0
                        ? "\nCLIQUE PARA AVANÇAR"
                        : string.Empty);
                if (phaseButton != null)
                    phaseButton.interactable =
                        !phasePresentationLocked &&
                        core?.CurrentPrompt?.Player == 0 &&
                        phaseTransitions > 0;
            }
        }

        private void ShowInspector(uint code)
        {
            ShowInspector(code, null);
        }

        private void ShowInspector(DuelZone3D zone)
        {
            ShowInspector(CodeAt(zone), zone);
        }

        private void ShowInspector(uint code, DuelZone3D zone)
        {
            if (code == 0 || database == null ||
                !database.TryGet(code, out CardRecord card))
            {
                return;
            }
            inspectedCode = code;
            inspectedZone = zone;
            CardCatalogEntry legacy = LegacyEntryFor(code);
            detailPanel.SetActive(true);
            detailPanel.transform.SetAsLastSibling();
            detailArtwork.sprite = SpriteFor(code);
            if (detailName != null)
            {
                detailName.text =
                    legacy != null &&
                    !string.IsNullOrWhiteSpace(legacy.DisplayName)
                        ? legacy.DisplayName
                        : card.Name;
            }
            string typeLine =
                legacy != null ? CombatTypeLine(legacy) : CardTypeLabel(card);
            if (detailCombatType != null)
                detailCombatType.text = typeLine;
            if (detailEffect != null)
            {
                string description =
                    legacy != null &&
                    !string.IsNullOrWhiteSpace(legacy.EffectText)
                        ? legacy.EffectText
                        : card.Description;
                detailEffect.text =
                    string.IsNullOrWhiteSpace(description)
                        ? "Descrição não disponível."
                        : description;
                detailEffect.fontSize = Mathf.Min(detailEffect.fontSize, 18);
                detailEffect.gameObject.SetActive(true);
                detailEffect.enabled = true;
                Color effectColor = detailEffect.color;
                effectColor.a = 1f;
                detailEffect.color = effectColor;
            }
            bool hasCurrentStats = TryGetCombatStats(
                zone,
                code,
                out int currentAttack,
                out int currentDefense);
            ApplyDetailInformation(
                legacy,
                card,
                hasCurrentStats ? currentAttack : null,
                hasCurrentStats ? currentDefense : null);
            ApplyDetailTheme(legacy, card);
            EnsureCardDetailContentVisible();
        }

        private void EnsureCardDetailContentVisible()
        {
            if (detailEffect == null || detailPanel == null)
                return;

            detailEffect.gameObject.SetActive(true);
            detailEffect.enabled = true;
            if (font != null)
                detailEffect.font = font;
            Color effectColor = detailEffect.color;
            effectColor.a = 1f;
            detailEffect.color = effectColor;

            RectTransform viewport =
                detailEffect.transform.parent as RectTransform;
            if (viewport != null)
            {
                viewport.gameObject.SetActive(true);
                Image viewportImage = viewport.GetComponent<Image>();
                if (viewportImage != null)
                    viewportImage.enabled = true;

                Mask legacyMask = viewport.GetComponent<Mask>();
                if (legacyMask != null)
                    legacyMask.enabled = false;
                RectMask2D rectMask = viewport.GetComponent<RectMask2D>();
                if (rectMask == null)
                    rectMask = viewport.gameObject.AddComponent<RectMask2D>();
                rectMask.enabled = true;

                RectTransform header =
                    detailEffectHeaderImage != null
                        ? detailEffectHeaderImage.rectTransform
                        : null;
                RectTransform commonParent =
                    viewport.parent as RectTransform;
                if (header != null && commonParent != null)
                {
                    var headerCorners = new Vector3[4];
                    var viewportCorners = new Vector3[4];
                    header.GetWorldCorners(headerCorners);
                    viewport.GetWorldCorners(viewportCorners);
                    float headerBottom = float.PositiveInfinity;
                    float viewportTop = float.NegativeInfinity;
                    for (int index = 0; index < 4; index++)
                    {
                        headerBottom = Mathf.Min(
                            headerBottom,
                            commonParent.InverseTransformPoint(
                                headerCorners[index]).y);
                        viewportTop = Mathf.Max(
                            viewportTop,
                            commonParent.InverseTransformPoint(
                                viewportCorners[index]).y);
                    }

                    const float HeaderGap = 8f;
                    float maximumViewportTop = headerBottom - HeaderGap;
                    if (viewportTop > maximumViewportTop)
                    {
                        Vector2 offsets = viewport.offsetMax;
                        offsets.y -= viewportTop - maximumViewportTop;
                        viewport.offsetMax = offsets;
                    }
                }
            }

            ScrollRect scroll = detailPanel.GetComponent<ScrollRect>();
            if (scroll != null)
            {
                scroll.enabled = true;
                scroll.content =
                    detailEffect.transform as RectTransform;
                if (viewport != null)
                    scroll.viewport = viewport;
                scroll.verticalNormalizedPosition = 1f;
            }

            Canvas.ForceUpdateCanvases();
            if (detailEffect.transform is RectTransform effectRect)
            {
                float minimumHeight = viewport != null
                    ? Mathf.Max(1f, viewport.rect.height - 12f)
                    : 1f;
                float preferredHeight = Mathf.Max(
                    minimumHeight,
                    detailEffect.preferredHeight + 16f);
                effectRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    preferredHeight);
                effectRect.SetAsLastSibling();
                LayoutRebuilder.ForceRebuildLayoutImmediate(effectRect);
            }
            if (viewport != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(viewport);
            Canvas.ForceUpdateCanvases();
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
        }

        private CardCatalogEntry LegacyEntryFor(uint code)
        {
            if (cardCatalog == null) return null;
            return cardCatalog.FindByOfficialId(code.ToString("00000000")) ??
                   cardCatalog.FindByOfficialId(code.ToString());
        }

        private void RefreshInspectedCombatStats()
        {
            if (inspectedCode == 0 || inspectedZone == null ||
                detailPanel == null || !detailPanel.activeSelf ||
                CodeAt(inspectedZone) != inspectedCode ||
                database == null ||
                !database.TryGet(inspectedCode, out CardRecord card) ||
                !TryGetCombatStats(
                    inspectedZone,
                    inspectedCode,
                    out int attack,
                    out int defense))
            {
                return;
            }
            ApplyDetailInformation(
                LegacyEntryFor(inspectedCode),
                card,
                attack,
                defense);
        }

        private void ApplyDetailInformation(
            CardCatalogEntry entry,
            CardRecord card,
            int? currentAttack = null,
            int? currentDefense = null)
        {
            bool isMonster = entry != null
                ? entry.Category == CardCategory.Monster
                : (card.Type & 0x1U) != 0;
            bool isSpell = entry != null
                ? entry.Category == CardCategory.Spell
                : (card.Type & 0x2U) != 0;
            bool isTrap = entry != null
                ? entry.Category == CardCategory.Trap
                : (card.Type & 0x4U) != 0;

            if (detailAttributeIcon != null)
            {
                detailAttributeIcon.gameObject.SetActive(
                    isMonster || isSpell || isTrap);
                detailAttributeIcon.sprite = isSpell
                    ? FindAttributeTemplate("spell")
                    : isTrap
                        ? FindAttributeTemplate("trap")
                        : FindAttributeTemplate(
                            entry != null
                                ? entry.Attribute.ToString()
                                : CoreAttributeKey(card.Attribute));
                detailAttributeIcon.enabled =
                    detailAttributeIcon.sprite != null;
            }

            int level =
                entry != null ? entry.Level : Mathf.Abs(card.Level);
            bool hasLevel = isMonster && level > 0;
            if (detailLevelIcon != null &&
                detailLevelIconTemplate != null)
            {
                detailLevelIcon.sprite = detailLevelIconTemplate;
            }
            SetDetailActive(detailLevelIcon, hasLevel);
            SetDetailActive(detailLevel, hasLevel);
            if (hasLevel && detailLevel != null)
                detailLevel.text = Mathf.Clamp(level, 1, 13).ToString();

            Sprite typeSprite = entry != null && isMonster
                ? FindTypeTemplate(TypeTemplateKey(entry.RaceName))
                : null;
            if (detailTypeIcon != null)
            {
                detailTypeIcon.sprite = typeSprite;
                detailTypeIcon.gameObject.SetActive(
                    isMonster && typeSprite != null);
                detailTypeIcon.enabled = typeSprite != null;
            }

            int attack = currentAttack ??
                         (entry != null ? entry.Attack : card.Attack);
            int defense = currentDefense ??
                          (entry != null ? entry.Defense : card.Defense);
            if (detailAttackIcon != null &&
                detailAttackIconTemplate != null)
            {
                detailAttackIcon.sprite = detailAttackIconTemplate;
            }
            if (detailDefenseIcon != null &&
                detailDefenseIconTemplate != null)
            {
                detailDefenseIcon.sprite = detailDefenseIconTemplate;
            }
            bool hasAttack = isMonster && attack >= 0;
            bool hasDefense =
                isMonster &&
                defense >= 0 &&
                (entry == null ||
                 entry.MonsterFrame != MonsterFrameKind.Link);
            SetDetailActive(detailAttackIcon, hasAttack);
            SetDetailActive(detailAttack, hasAttack);
            if (hasAttack && detailAttack != null)
                detailAttack.text = attack.ToString();
            SetDetailActive(detailDefenseIcon, hasDefense);
            SetDetailActive(detailDefense, hasDefense);
            if (hasDefense && detailDefense != null)
                detailDefense.text = defense.ToString();
        }

        private static void SetDetailActive(Component component, bool active)
        {
            if (component != null)
                component.gameObject.SetActive(active);
        }

        private void ApplyDetailTheme(
            CardCatalogEntry entry,
            CardRecord card)
        {
            Color theme = DetailThemeColor(entry, card);
            if (detailCardHeaderImage != null)
                detailCardHeaderImage.color = theme;
            if (detailEffectHeaderImage != null)
                detailEffectHeaderImage.color = theme;
            if (detailCardOutline != null)
            {
                detailCardOutline.effectColor =
                    new Color(theme.r, theme.g, theme.b, 0.78f);
            }
        }

        private static Color DetailThemeColor(
            CardCatalogEntry entry,
            CardRecord card)
        {
            if (entry == null)
            {
                if ((card.Type & 0x2U) != 0) return Hex("#39D4EE");
                if ((card.Type & 0x4U) != 0) return Hex("#D064A5");
                return Hex("#D88943");
            }
            if (entry.Category == CardCategory.Spell)
                return Hex("#39D4EE");
            if (entry.Category == CardCategory.Trap)
                return Hex("#D064A5");
            return entry.MonsterFrame switch
            {
                MonsterFrameKind.Normal => Hex("#E1B85B"),
                MonsterFrameKind.Ritual => Hex("#65AFC9"),
                MonsterFrameKind.Fusion => Hex("#9B72BE"),
                MonsterFrameKind.Synchro => Hex("#E6EDF0"),
                MonsterFrameKind.Xyz => Hex("#646672"),
                MonsterFrameKind.Link => Hex("#4C87C6"),
                MonsterFrameKind.Pendulum => Hex("#61B78F"),
                _ => Hex("#D88943")
            };
        }

        private Sprite FindAttributeTemplate(string key)
        {
            return FindTemplateSprite(
                detailAttributeIconTemplates,
                key,
                false);
        }

        private Sprite FindTypeTemplate(string key)
        {
            return FindTemplateSprite(
                detailTypeIconTemplates,
                key,
                true);
        }

        private static Sprite FindTemplateSprite(
            IReadOnlyList<Sprite> templates,
            string key,
            bool typeTemplate)
        {
            if (templates == null || string.IsNullOrWhiteSpace(key))
                return null;

            string normalizedKey = NormalizeTemplateKey(key);
            string expectedTypeKey = $"type{normalizedKey}madu";
            foreach (Sprite sprite in templates)
            {
                if (sprite == null) continue;
                string candidate = NormalizeTemplateKey(sprite.name);
                if (typeTemplate
                        ? candidate.Contains(expectedTypeKey)
                        : candidate.StartsWith(normalizedKey))
                {
                    return sprite;
                }
            }
            return null;
        }

        private static string TypeTemplateKey(string raceName)
        {
            return NormalizeTemplateKey(raceName) switch
            {
                "bestaguerreira" or "beastwarrior" => "beastwarrior",
                "bestaalada" or "wingedbeast" => "wingedbeast",
                "bestadivina" or "divinebeast" => "divinebeast",
                "deuscriador" or "creatorgod" => "creatorgod",
                "serpentemarinha" or "seaserpent" => "seaserpent",
                "mago" or "spellcaster" => "spellcaster",
                "guerreiro" or "warrior" => "warrior",
                "dragao" or "dragon" => "dragon",
                "demonio" or "fiend" => "fiend",
                "fada" or "fairy" => "fairy",
                "maquina" or "machine" => "machine",
                "trovao" or "thunder" => "thunder",
                "dinossauro" or "dinosaur" => "dinosaur",
                "peixe" or "fish" => "fish",
                "zumbi" or "zombie" => "zombie",
                "rocha" or "rock" => "rock",
                "reptil" or "reptile" => "reptile",
                "psiquico" or "psychic" => "psychic",
                "planta" or "plant" => "plant",
                "inseto" or "insect" => "insect",
                "ilusao" or "illusion" => "illusion",
                "besta" or "beast" => "beast",
                string value => value
            };
        }

        private static string NormalizeTemplateKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(decomposed.Length);
            foreach (char character in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) ==
                    UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }
                if (char.IsLetterOrDigit(character))
                    builder.Append(char.ToLowerInvariant(character));
            }
            return builder.ToString();
        }

        private static string CoreAttributeKey(uint attribute)
        {
            if ((attribute & 0x20U) != 0) return "dark";
            if ((attribute & 0x10U) != 0) return "light";
            if ((attribute & 0x08U) != 0) return "wind";
            if ((attribute & 0x04U) != 0) return "fire";
            if ((attribute & 0x02U) != 0) return "water";
            if ((attribute & 0x01U) != 0) return "earth";
            if ((attribute & 0x40U) != 0) return "divine";
            return string.Empty;
        }

        private static string CombatTypeLine(CardCatalogEntry entry)
        {
            if (entry.Category != CardCategory.Monster)
                return $"[{entry.TypeName}]";
            string subtype = entry.MonsterFrame == MonsterFrameKind.Normal
                ? "Normal"
                : entry.MonsterFrame == MonsterFrameKind.Fusion
                    ? "Fusão"
                    : entry.TypeName.Contains("Virar")
                        ? "Efeito / Virar"
                        : "Efeito";
            return $"[{entry.RaceName} / {subtype}]";
        }

        private void CloseCardDetails()
        {
            if (detailPanel != null)
                detailPanel.SetActive(false);
            inspectedCode = 0;
            inspectedZone = null;
        }

        private void OpenPhaseChoices()
        {
            if (InteractionLocked)
            {
                SetStatus(
                    "Aguarde a conclusão da apresentação da fase.",
                    Muted);
                return;
            }
            DuelPrompt prompt = core.CurrentPrompt;
            if (prompt == null) return;
            List<DuelChoice> phases =
                DuelPromptPresentationRules.PhaseChoices(prompt);
            if (phases.Count > 0)
            {
                OpenPhaseNavigator(prompt, phases);
                return;
            }
            SetStatus(
                prompt.Player == 1
                    ? "A IA está resolvendo o turno dela."
                    : "Conclua a escolha atual antes de avançar a fase.",
                Gold);
        }

        private void BuildChoiceModalLegacy()
        {
            choiceModal = CreatePanel(
                frame,
                "Escolha do ygopro-core",
                new Vector2(0.18f, 0.12f),
                new Vector2(0.82f, 0.48f),
                new Color(0.006f, 0.025f, 0.045f, 0.985f));
            AddOutline(choiceModal, Cyan);
            choiceTitle = CreateText(
                choiceModal.transform,
                "ESCOLHA UMA AÇÃO",
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.04f, 0.80f),
                new Vector2(0.96f, 0.96f),
                TextAnchor.MiddleCenter);
            choiceContent = CreateRect(
                choiceModal.transform,
                "Conteúdo",
                new Vector2(0.035f, 0.19f),
                new Vector2(0.965f, 0.79f),
                Vector2.zero);
            choiceConfirm = CreateButton(
                choiceModal.transform,
                "Confirmar Escolha",
                "SELECIONAR",
                new Vector2(0.37f, 0.025f),
                new Vector2(0.63f, 0.17f),
                Lime,
                ConfirmMultiSelection);
            choiceModal.SetActive(false);
        }

        private void OpenChoiceModalLegacy(
            DuelPrompt prompt,
            IReadOnlyList<DuelChoice> choices)
        {
            if (prompt == null || choices == null || choices.Count == 0)
                return;
            ApplyChoicePresentationProfile(prompt);
            ClearChildren(choiceContent);
            selectedPromptIndexes.Clear();
            choiceTitle.text =
                ChoicePresentationHeading(prompt).ToUpperInvariant();
            int count = choices.Count;
            float width = Mathf.Min(150f, 860f / Mathf.Max(1, count));
            float start = -(count - 1) * width * 0.5f;
            foreach ((DuelChoice choice, int index) in
                     choices.Select((choice, index) => (choice, index)))
            {
                GameObject buttonObject = CreatePanel(
                    choiceContent,
                    $"Escolha {index + 1}",
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Color(0.02f, 0.10f, 0.16f, 0.98f));
                RectTransform rect =
                    buttonObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(width - 8f, 178f);
                rect.anchoredPosition =
                    new Vector2(start + index * width, 0f);
                AddOutline(buttonObject, choiceModalAccent);
                var button = buttonObject.AddComponent<Button>();
                button.targetGraphic = buttonObject.GetComponent<Image>();
                if (choice.CardCode != 0)
                {
                    Image art = CreateImage(
                        buttonObject.transform,
                        "Arte",
                        new Vector2(0.20f, 0.25f),
                        new Vector2(0.80f, 0.96f),
                        Color.white);
                    art.sprite = SpriteFor(choice.CardCode);
                    art.preserveAspect = true;
                }
                CreateText(
                    buttonObject.transform,
                    ChoiceLabel(choice),
                    11,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.03f, 0.01f),
                    new Vector2(0.97f, 0.26f),
                    TextAnchor.MiddleCenter);
                DuelChoice captured = choice;
                button.onClick.AddListener(
                    () => SubmitSelectionChoice(captured));
            }
            bool multi =
                prompt.Message == CoreMessage.SelectCard ||
                prompt.Message == CoreMessage.SelectTribute ||
                prompt.Message == CoreMessage.SelectSum;
            choiceConfirm.gameObject.SetActive(
                multi && prompt.MaximumSelections > 1);
            SetDuelExperienceObscured(true);
            choiceModal.SetActive(true);
            choiceModal.transform.SetAsLastSibling();
        }

        private void SubmitSelectionChoice(DuelChoice choice)
        {
            DuelPrompt prompt = core.CurrentPrompt;
            if (prompt == null || choice == null) return;
            bool multi =
                (prompt.Message == CoreMessage.SelectCard ||
                 prompt.Message == CoreMessage.SelectTribute ||
                 prompt.Message == CoreMessage.SelectSum) &&
                prompt.MaximumSelections > 1 &&
                choice.ChoiceIndex >= 0;
            if (!multi)
            {
                core.SubmitChoice(choice);
                RefreshEverything(true);
                return;
            }
            if (!selectedPromptIndexes.Add(choice.ChoiceIndex))
                selectedPromptIndexes.Remove(choice.ChoiceIndex);
            while (selectedPromptIndexes.Count > prompt.MaximumSelections)
                selectedPromptIndexes.Remove(
                    selectedPromptIndexes.First());
            choiceConfirm.interactable =
                CoreMessageDecoder.IsValidSelection(
                    prompt,
                    selectedPromptIndexes);
            choiceConfirm.GetComponentInChildren<Text>().text =
                $"SELECIONAR · {selectedPromptIndexes.Count}";
        }

        private void ConfirmMultiSelectionLegacy()
        {
            DuelPrompt prompt = core.CurrentPrompt;
            if (prompt == null ||
                !CoreMessageDecoder.IsValidSelection(
                    prompt,
                    selectedPromptIndexes))
            {
                return;
            }
            core.SubmitCoreResponse(
                CoreMessageDecoder.CardSelectionResponse(
                    selectedPromptIndexes
                        .OrderBy(index => index)
                        .Select(index => (uint)index)
                        .ToArray()),
                prompt.RequestId);
            RefreshEverything(true);
        }

        private void CloseChoiceModal()
        {
            if (choiceModal != null) choiceModal.SetActive(false);
            ResetChoiceSelectionState();
            SetDuelExperienceObscured(false);
        }

        private void BuildZoneBrowser()
        {
            zoneBrowser = CreatePanel(
                frame,
                "Navegador de Zona",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0.01f, 0.018f, 0.48f));
            var dismissButton = zoneBrowser.AddComponent<Button>();
            dismissButton.targetGraphic = zoneBrowser.GetComponent<Image>();
            dismissButton.transition = Selectable.Transition.None;
            dismissButton.onClick.AddListener(CloseZoneBrowser);

            zoneBrowserTray = CreatePanel(
                zoneBrowser.transform,
                "Bandeja do Deck Adicional",
                new Vector2(0.295f, 0.15f),
                new Vector2(0.96f, 0.60f),
                new Color(0.006f, 0.025f, 0.045f, 0.985f));
            ConfigureZoneBrowserTrayArtwork();
            AddOutline(zoneBrowserTray, Gold);
            var trayInputBlocker = zoneBrowserTray.AddComponent<Button>();
            trayInputBlocker.targetGraphic =
                zoneBrowserTray.GetComponent<Image>();
            trayInputBlocker.transition = Selectable.Transition.None;

            zoneBrowserTitle = CreateText(
                zoneBrowserTray.transform,
                "DECK ADICIONAL",
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.04f, 0.83f),
                new Vector2(0.96f, 0.97f),
                TextAnchor.MiddleCenter);

            GameObject viewportObject = CreatePanel(
                zoneBrowserTray.transform,
                "Área de rolagem",
                new Vector2(0.025f, 0.22f),
                new Vector2(0.975f, 0.81f),
                new Color(0.012f, 0.055f, 0.075f, 0.82f));
            AddOutline(viewportObject, new Color(0.18f, 0.55f, 0.62f, 1f));
            RectTransform viewport =
                viewportObject.GetComponent<RectTransform>();
            viewportObject.AddComponent<RectMask2D>();
            zoneBrowserContent = CreateRect(
                viewportObject.transform,
                "Cartas",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, -16f));
            zoneBrowserContent.pivot = new Vector2(0f, 0.5f);
            zoneBrowserContent.anchoredPosition = new Vector2(12f, 0f);
            var layout = zoneBrowserContent.gameObject
                .AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(4, 16, 8, 8);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var fitter = zoneBrowserContent.gameObject
                .AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            zoneBrowserScroll = viewportObject.AddComponent<ScrollRect>();
            zoneBrowserScroll.viewport = viewport;
            zoneBrowserScroll.content = zoneBrowserContent;
            zoneBrowserScroll.horizontal = true;
            zoneBrowserScroll.vertical = false;
            zoneBrowserScroll.movementType = ScrollRect.MovementType.Elastic;
            zoneBrowserScroll.scrollSensitivity = 38f;
            zoneBrowserScroll.decelerationRate = 0.12f;

            BuildZoneBrowserConfirmation(zoneBrowserTray.transform);

            CreateText(
                zoneBrowserTray.transform,
                "SELECIONE UMA CARTA · CONFIRME ABAIXO · ARRASTE OU USE A RODA PARA ROLAR",
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.03f, 0.15f),
                new Vector2(0.97f, 0.21f),
                TextAnchor.MiddleCenter);
            zoneBrowser.SetActive(false);
        }

        private void OpenZoneChoices(DuelZone3D zone, DuelPrompt prompt)
        {
            List<DuelChoice> choices = prompt?.Choices
                .Where(choice =>
                    choice.HasLocation &&
                    choice.Controller == StatePlayerForZone(zone) &&
                    (choice.Location & LocationFor(zone.Kind)) != 0)
                .ToList() ?? new List<DuelChoice>();
            bool browsingExtraDeck =
                zone.Kind == DuelZoneKind.ExtraDeck &&
                IsLocalZone(zone);
            List<ZoneBrowserEntry> entries = BuildZoneBrowserEntries(
                browsingExtraDeck,
                choices);
            bool summonMode =
                browsingExtraDeck &&
                prompt != null &&
                prompt == core.CurrentPrompt &&
                prompt.Message == CoreMessage.SelectIdleCommand &&
                choices.Count > 0;
            if (!browsingExtraDeck && choices.Count == 0)
            {
                InspectZone(zone);
                return;
            }
            ResizeZoneBrowserTray(entries.Count);
            ResetZoneBrowserSelection(prompt);
            ConfigureZoneBrowserActionMode(summonMode);
            CloseChoiceModal();
            ClearChildren(zoneBrowserContent);
            int legalCardCount = entries.Count(entry =>
                entry.LegalChoices.Count > 0);
            zoneBrowserTitle.text = browsingExtraDeck
                ? legalCardCount > 0
                    ? $"DECK ADICIONAL · {legalCardCount} INVOCÁVEL(IS) AGORA"
                    : $"DECK ADICIONAL · {entries.Count} CARTA(S) · SOMENTE CONSULTA"
                : PileLabel(zone).ToUpperInvariant();

            for (int index = 0; index < entries.Count; index++)
            {
                ZoneBrowserEntry entry = entries[index];
                CreateZoneBrowserCard(
                    entry.Code,
                    index,
                    prompt,
                    entry.LegalChoices);
            }
            if (entries.Count == 0)
            {
                CreateText(
                    zoneBrowserContent,
                    "O Deck Adicional está vazio.",
                    18,
                    FontStyle.Bold,
                    Muted,
                    Vector2.zero,
                    Vector2.one,
                    TextAnchor.MiddleCenter);
            }
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(zoneBrowserContent);
            zoneBrowserScroll.horizontalNormalizedPosition = 0f;
            SetDuelExperienceObscured(true);
            zoneBrowser.SetActive(true);
            zoneBrowser.transform.SetAsLastSibling();
        }

        private void CreateZoneBrowserCard(
            uint code,
            int index,
            DuelPrompt prompt,
            IReadOnlyList<DuelChoice> legalChoices)
        {
            bool canUse = legalChoices != null && legalChoices.Count > 0;
            GameObject card = CreatePanel(
                zoneBrowserContent,
                $"Carta {index + 1}",
                Vector2.zero,
                Vector2.one,
                new Color(0.018f, 0.075f, 0.10f, 0.98f));
            var cardLayout = card.AddComponent<LayoutElement>();
            cardLayout.minWidth = ChoiceCardWidth;
            cardLayout.preferredWidth = ChoiceCardWidth;
            cardLayout.flexibleWidth = 0f;
            AddOutline(card, canUse ? EffectGlow : Muted);

            Image artwork = CreateImage(
                card.transform,
                "Arte",
                new Vector2(0.07f, 0.06f),
                new Vector2(0.93f, 0.96f),
                Color.white);
            artwork.sprite = SpriteFor(code);
            artwork.preserveAspect = true;
            var inspectButton = card.AddComponent<Button>();
            inspectButton.targetGraphic = card.GetComponent<Image>();
            Outline cardOutline = card.GetComponent<Outline>();
            RegisterZoneBrowserChoice(cardOutline);
            IReadOnlyList<DuelChoice> capturedChoices =
                legalChoices?.ToArray();
            inspectButton.onClick.AddListener(
                () => StageZoneBrowserSelection(
                    code,
                    prompt,
                    capturedChoices,
                    cardOutline));
        }

        private void SubmitZoneBrowserAction(
            DuelPrompt prompt,
            IReadOnlyList<DuelChoice> choices)
        {
            if (prompt == null ||
                core.CurrentPrompt != prompt ||
                choices == null ||
                choices.Count == 0)
            {
                CloseZoneBrowser();
                return;
            }
            CloseZoneBrowser();
            if (choices.Count > 1)
            {
                OpenChoiceModal(prompt, choices);
                return;
            }
            core.SubmitChoice(choices[0]);
            RefreshEverything(true);
        }

        private void CloseZoneBrowser()
        {
            if (zoneBrowser != null) zoneBrowser.SetActive(false);
            ResetZoneBrowserSelection();
            SetDuelExperienceObscured(false);
        }

        private void InspectZone(DuelZone3D zone)
        {
            uint code = CodeAt(zone);
            if (code != 0) ShowInspector(zone);
            SetStatus(PileLabel(zone), Muted);
        }

        public void BeginMonsterAttackDrag(
            DuelZone3D zone,
            Vector2 screenPosition)
        {
            if (InteractionLocked ||
                zone == null || core.CurrentPrompt?.Message !=
                CoreMessage.SelectBattleCommand)
                return;
            DuelChoice attack = ChoicesForCard(
                    core.CurrentPrompt,
                    CodeAt(zone),
                    StatePlayerForZone(zone),
                    (byte)DuelLocation.MonsterZone,
                    zone.ZoneIndex)
                .FirstOrDefault(choice => Contains(choice.Label, "Atacar"));
            if (attack == null) return;
            draggingAttacker = zone;
            EnsureAttackLine();
            UpdateMonsterAttackDrag(screenPosition);
        }

        public void UpdateMonsterAttackDrag(Vector2 screenPosition)
        {
            if (draggingAttacker == null || attackLine == null ||
                Camera.main == null)
                return;
            Vector3 start =
                draggingAttacker.transform.position + Vector3.up * 0.7f;
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            Vector3 end = ray.origin + ray.direction * 14f;
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                end = hit.point + Vector3.up * 0.25f;
            attackLine.SetPosition(0, start);
            attackLine.SetPosition(1, end);
            attackLine.enabled = true;
        }

        public void EndMonsterAttackDrag(Vector2 screenPosition)
        {
            if (InteractionLocked ||
                draggingAttacker == null) return;
            DuelZone3D attacker = draggingAttacker;
            draggingAttacker = null;
            if (attackLine != null) attackLine.enabled = false;

            DuelChoice attack = ChoicesForCard(
                    core.CurrentPrompt,
                    CodeAt(attacker),
                    StatePlayerForZone(attacker),
                    (byte)DuelLocation.MonsterZone,
                    attacker.ZoneIndex)
                .FirstOrDefault(choice => Contains(choice.Label, "Atacar"));
            if (attack == null) return;
            core.SubmitChoice(attack);
            if (TryRaycastZone(screenPosition, out DuelZone3D target))
                HandleZoneClick(target, 1);
            RefreshEverything(true);
        }

        private void EnsureAttackLine()
        {
            if (attackLine != null) return;
            var line = new GameObject("Seta de Ataque");
            attackLine = line.AddComponent<LineRenderer>();
            attackLine.positionCount = 2;
            attackLine.startWidth = 0.18f;
            attackLine.endWidth = 0.04f;
            attackLine.startColor = Cyan;
            attackLine.endColor = Gold;
            Shader shader =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
                attackLine.material = new Material(shader);
            attackLine.enabled = false;
        }

        private IEnumerator ShowCardPresentation(
            uint code,
            string heading,
            Color accent,
            ArcaneCardSound sound,
            bool hideIdentity,
            bool extraDeckSummon)
        {
            if ((!hideIdentity && code == 0) || arenaCanvas == null)
                yield break;
            GameObject overlay = CreatePanel(
                arenaCanvas.transform,
                "Apresentação de Card",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0.01f, 0.025f, 0.62f));
            overlay.transform.SetAsLastSibling();
            var group = overlay.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            ExtraDeckSummonFocus summonFocus = extraDeckSummon
                ? CreateExtraDeckSummonFocus(overlay.transform, accent)
                : null;
            Image art = CreateImage(
                overlay.transform,
                "Carta Apresentada",
                new Vector2(0.395f, 0.19f),
                new Vector2(0.605f, 0.82f),
                Color.white);
            art.sprite = hideIdentity ? cardBackSprite : SpriteFor(code);
            art.preserveAspect = true;
            art.rectTransform.localScale = Vector3.one * 0.62f;
            CreateText(
                overlay.transform,
                heading,
                20,
                FontStyle.Bold,
                accent,
                new Vector2(0.30f, 0.84f),
                new Vector2(0.70f, 0.91f),
                TextAnchor.MiddleCenter);
            CreateText(
                overlay.transform,
                hideIdentity ? "CARTA VIRADA PARA BAIXO" : CardName(code),
                30,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.20f, 0.08f),
                new Vector2(0.80f, 0.17f),
                TextAnchor.MiddleCenter);
            cardAudioDirector ??= GetComponent<ArcaneAudioDirector>();
            float soundDuration =
                cardAudioDirector?.PlayCardCue(sound) ?? 0f;
            float totalDuration = soundDuration > 0f
                ? soundDuration
                : 1.02f;
            float enter = Mathf.Min(0.34f, totalDuration * 0.32f);
            float exit = Mathf.Min(0.20f, totalDuration * 0.20f);
            float elapsed = 0f;
            cardPresentationAccelerated = false;
            cardPresentationCanAccelerate = true;
            lastCardPresentationClick = -10f;
            while (elapsed < totalDuration)
            {
                float speed = cardPresentationAccelerated ? 2f : 1f;
                elapsed += Time.unscaledDeltaTime * speed;
                if (elapsed < enter)
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / enter);
                    group.alpha = t;
                    art.rectTransform.localScale =
                        Vector3.one * Mathf.Lerp(0.62f, 1f, t);
                    art.rectTransform.localRotation = Quaternion.Euler(
                        0f,
                        0f,
                        Mathf.Lerp(50f, 0f, t));
                }
                else if (elapsed > totalDuration - exit)
                {
                    group.alpha = Mathf.Clamp01(
                        (totalDuration - elapsed) / exit);
                }
                else
                {
                    group.alpha = 1f;
                    art.rectTransform.localScale = Vector3.one;
                    art.rectTransform.localRotation = Quaternion.identity;
                }
                UpdateExtraDeckSummonFocus(
                    summonFocus,
                    elapsed,
                    totalDuration,
                    speed);
                yield return null;
            }
            cardPresentationCanAccelerate = false;
            Destroy(overlay);
        }

        private IEnumerator FlashLifeDamage(byte player)
        {
            Text life = player == 0 ? localLife : opponentLife;
            if (life == null) yield break;
            Color original = life.color;
            life.color = Red;
            yield return new WaitForSecondsRealtime(0.28f);
            life.color = original;
        }

        private Sprite SpriteFor(uint code)
        {
            if (runtimeSprites.TryGetValue(code, out Sprite cached))
                return cached;
            string official = code.ToString("00000000");
            CardCatalogEntry entry =
                cardCatalog?.FindByOfficialId(official) ??
                cardCatalog?.FindByOfficialId(code.ToString());
            try
            {
                visualCatalog ??= CardVisualCatalog.LoadDefault();
                string path = visualCatalog.ArtPath(code);
                if (File.Exists(path))
                {
                    Texture2D texture = RuntimeCardTextureLoader.Load(
                        path,
                        official);
                    if (texture == null)
                        return cardBackSprite;
                    var sprite = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                    sprite.name = official;
                    runtimeTextures[code] = texture;
                    runtimeSprites[code] = sprite;
                    return sprite;
                }
            }
            catch (Exception exception)
            {
                if (entry?.Artwork == null)
                {
                    Debug.LogWarning(
                        $"Arte {official} não carregada: {exception.Message}");
                }
            }

            if (entry?.Artwork != null)
            {
                runtimeSprites[code] = entry.Artwork;
                return entry.Artwork;
            }
            return cardBackSprite;
        }

        private uint CodeAt(DuelZone3D zone)
        {
            if (zone == null || state == null ||
                !Enum.IsDefined(typeof(DuelPlayerSide), zone.Owner))
            {
                return 0;
            }
            int player = StatePlayerForZone(zone);
            if (player < 0 || player >= state.Players.Length)
                return 0;
            int sequence = SequenceFor(zone);
            if (zone.Kind == DuelZoneKind.Monster)
            {
                return sequence >= 0 &&
                       sequence < state.Players[player].MonsterZones.Length
                    ? state.Players[player].MonsterZones[sequence]
                    : 0;
            }
            if (zone.Kind == DuelZoneKind.SpellTrap ||
                zone.Kind == DuelZoneKind.Field)
            {
                return sequence >= 0 &&
                       sequence < state.Players[player].SpellTrapZones.Length
                    ? state.Players[player].SpellTrapZones[sequence]
                    : 0;
            }
            if (zone.Kind == DuelZoneKind.Graveyard)
                return state.Players[player].Graveyard.LastOrDefault();
            if (zone.Kind == DuelZoneKind.Banishment)
                return state.Players[player].Banished.LastOrDefault();
            return 0;
        }

        private uint PositionAt(DuelZone3D zone)
        {
            if (zone == null || state == null ||
                !Enum.IsDefined(typeof(DuelPlayerSide), zone.Owner))
            {
                return FaceUpAttack;
            }
            int player = StatePlayerForZone(zone);
            if (player < 0 || player >= state.Players.Length)
                return FaceUpAttack;
            int sequence = SequenceFor(zone);
            if (zone.Kind == DuelZoneKind.Monster &&
                sequence >= 0 &&
                sequence < state.Players[player].MonsterPositions.Length)
                return state.Players[player].MonsterPositions[sequence];
            if ((zone.Kind == DuelZoneKind.SpellTrap ||
                 zone.Kind == DuelZoneKind.Field) &&
                sequence >= 0 &&
                sequence < state.Players[player].SpellTrapPositions.Length)
                return state.Players[player].SpellTrapPositions[sequence];
            return FaceUpAttack;
        }

        private DuelZone3D FindZone(
            byte controller,
            byte location,
            int sequence)
        {
            DuelZoneKind kind =
                (location & DuelLocation.MonsterZone) != 0
                    ? DuelZoneKind.Monster
                    : (location & DuelLocation.SpellTrapZone) != 0
                        ? sequence == 5
                            ? DuelZoneKind.Field
                            : DuelZoneKind.SpellTrap
                        : (location & DuelLocation.Extra) != 0
                            ? DuelZoneKind.ExtraDeck
                            : (location & DuelLocation.Deck) != 0
                                ? DuelZoneKind.MainDeck
                                : (location & DuelLocation.Graveyard) != 0
                                    ? DuelZoneKind.Graveyard
                                    : DuelZoneKind.Banishment;
            return AllZones().FirstOrDefault(zone =>
                StatePlayerForZone(zone) == controller &&
                zone.Kind == kind &&
                (kind != DuelZoneKind.Monster &&
                 kind != DuelZoneKind.SpellTrap
                    ? true
                    : zone.ZoneIndex == sequence));
        }

        private IEnumerable<DuelZone3D> AllZones()
        {
            return FindObjectsByType<DuelZone3D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
        }

        // The host arena uses the authored P1/P2 table directly. A joining
        // player sees a perspective-mapped state (own side is logical P0),
        // so convert between that safe state and the physical P2 half of the
        // authored table only at the presentation boundary.
        private byte StatePlayerForZone(DuelZone3D zone)
        {
            if (zone == null)
                return 0;
            bool invert = core != null && core.IsNetworkReplica &&
                          core.NetworkLocalPlayer == 1;
            byte physical = (byte)zone.Owner;
            return invert ? (byte)(1 - physical) : physical;
        }

        private DuelPlayerSide PhysicalSideForStatePlayer(byte player)
        {
            bool invert = core != null && core.IsNetworkReplica &&
                          core.NetworkLocalPlayer == 1;
            byte physical = invert ? (byte)(1 - player) : player;
            return physical == 0
                ? DuelPlayerSide.PlayerOne
                : DuelPlayerSide.PlayerTwo;
        }

        private bool IsLocalZone(DuelZone3D zone)
        {
            return StatePlayerForZone(zone) == 0;
        }

        private void ClearZoneHighlights()
        {
            foreach (DuelZone3D zone in AllZones())
                zone.SetDropHighlight(false);
        }

        private static int SequenceFor(DuelZone3D zone)
        {
            return zone.Kind == DuelZoneKind.Field
                ? 5
                : zone.ZoneIndex;
        }

        private static byte LocationFor(DuelZoneKind kind)
        {
            return kind switch
            {
                DuelZoneKind.Monster => (byte)DuelLocation.MonsterZone,
                DuelZoneKind.SpellTrap => (byte)DuelLocation.SpellTrapZone,
                DuelZoneKind.Field => (byte)DuelLocation.SpellTrapZone,
                DuelZoneKind.MainDeck => (byte)DuelLocation.Deck,
                DuelZoneKind.ExtraDeck => (byte)DuelLocation.Extra,
                DuelZoneKind.Graveyard => (byte)DuelLocation.Graveyard,
                DuelZoneKind.Banishment => (byte)DuelLocation.Banished,
                _ => 0
            };
        }

        private List<DuelChoice> ChoicesForCard(
            DuelPrompt prompt,
            uint code,
            byte controller,
            byte location,
            int sequence)
        {
            if (prompt == null || code == 0)
                return new List<DuelChoice>();
            return prompt.Choices.Where(choice =>
                choice.CardCode == code &&
                (choice.RequestId == 0 ||
                 prompt.RequestId == 0 ||
                 choice.RequestId == prompt.RequestId) &&
                (!choice.HasLocation ||
                 (choice.Controller == controller &&
                  (choice.Location & location) != 0 &&
                  choice.Sequence == sequence))).ToList();
        }

        private List<DuelChoice> ChoicesForCard(
            DuelPrompt prompt,
            CardInstanceKey instanceKey)
        {
            return CoreCardActionBinding
                .ChoicesFor(prompt, instanceKey)
                .ToList();
        }

        private DuelChoice ChoiceForCard(
            DuelPrompt prompt,
            uint code,
            byte controller,
            byte location,
            int sequence)
        {
            return ChoicesForCard(
                    prompt,
                    code,
                    controller,
                    location,
                    sequence)
                .FirstOrDefault();
        }

        private DuelChoice ChoiceForCard(
            DuelPrompt prompt,
            CardInstanceKey instanceKey)
        {
            return CoreCardActionBinding.FirstChoiceFor(
                prompt,
                instanceKey);
        }

        private static bool IsDirectSelectionPrompt(DuelPrompt prompt)
        {
            return prompt != null &&
                   (prompt.Message == CoreMessage.SelectCard ||
                    prompt.Message == CoreMessage.SelectTribute ||
                    prompt.Message == CoreMessage.SelectSum ||
                    prompt.Message == CoreMessage.SelectUnselectCard);
        }

        private static bool HasOffFieldChoices(DuelPrompt prompt)
        {
            return prompt != null && prompt.Choices.Any(choice =>
                !choice.HasLocation ||
                (choice.Location &
                 (DuelLocation.Deck |
                  DuelLocation.Extra |
                  DuelLocation.Graveyard |
                  DuelLocation.Banished)) != 0);
        }

        private bool IsMonster(uint code)
        {
            return database != null &&
                   database.TryGet(code, out CardRecord card) &&
                   (card.Type & 0x1U) != 0;
        }

        private string CardName(uint code)
        {
            return database != null &&
                   database.TryGet(code, out CardRecord card)
                ? card.Name
                : code.ToString("00000000");
        }

        private static string CardTypeLabel(CardRecord card)
        {
            if ((card.Type & 0x2U) != 0) return "[Magia]";
            if ((card.Type & 0x4U) != 0) return "[Armadilha]";
            return $"[Monstro · Nível {card.Level}]";
        }

        private static bool IsFaceUp(uint position)
        {
            return position == 0 ||
                   (position & (FaceUpAttack | FaceUpDefense)) != 0;
        }

        private static bool IsSpecialZone(DuelZoneKind kind)
        {
            return kind == DuelZoneKind.MainDeck ||
                   kind == DuelZoneKind.ExtraDeck ||
                   kind == DuelZoneKind.Graveyard ||
                   kind == DuelZoneKind.Banishment;
        }

        private string PileLabel(DuelZone3D zone)
        {
            if (zone == null)
            {
                DuelDevelopmentLog.Write(
                    DuelLogCategory.Zone,
                    "PileLabel received a null zone.",
                    this);
                return "Zona indisponivel";
            }
            if (!zone.HasValidIdentity)
            {
                DuelDevelopmentLog.Write(
                    DuelLogCategory.Zone,
                    $"PileLabel ignored an uninitialized zone object " +
                    $"'{zone.gameObject.name}'.",
                    zone);
                return "Zona ainda nao inicializada";
            }
            if (state == null || state.Players == null)
                return "Zona sendo inicializada";
            int player = StatePlayerForZone(zone);
            if (player < 0 || player >= state.Players.Length ||
                state.Players[player] == null)
            {
                DuelDevelopmentLog.Write(
                    DuelLogCategory.Zone,
                    $"PileLabel rejected controller {player} for " +
                    $"{zone.StableId}.",
                    zone);
                return "Zona com controlador invalido";
            }
            return zone.Kind switch
            {
                DuelZoneKind.MainDeck =>
                    $"Deck · {state.Players[player].DeckCount} cartas",
                DuelZoneKind.ExtraDeck =>
                    $"Deck Adicional · {state.Players[player].ExtraDeckCount} cartas",
                DuelZoneKind.Graveyard =>
                    $"Cemitério · {state.Players[player].Graveyard.Count} cartas",
                DuelZoneKind.Banishment =>
                    $"Banimento · {state.Players[player].Banished.Count} cartas",
                DuelZoneKind.Monster =>
                    $"Zona de Monstro {zone.ZoneIndex + 1}",
                DuelZoneKind.SpellTrap =>
                    $"Zona de Magia/Armadilha {zone.ZoneIndex + 1}",
                DuelZoneKind.Field =>
                    "Zona de Campo",
                _ => string.IsNullOrWhiteSpace(zone.StableId)
                    ? "Zona desconhecida"
                    : zone.StableId
            };
        }

        private static string ChoiceLabel(DuelChoice choice)
        {
            return choice.CardCode == 0
                ? choice.Label
                : choice.Label;
        }

        private void SetStatus(string value, Color color)
        {
            if (status != null)
            {
                status.text = value;
                status.color = color;
            }
            UpdateDecisionRibbon(value, color);
        }

        private static bool Contains(string source, string value)
        {
            return (source ?? string.Empty).IndexOf(
                value,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string InteractionLockStatus(string fallback)
        {
            string online = DuelOnlineSession.Instance?.InteractionWaitMessage;
            return string.IsNullOrWhiteSpace(online) ? fallback : online;
        }

        private void UpdateOnlineInteractionWaitStatus()
        {
            string online = DuelOnlineSession.Instance?.InteractionWaitMessage;
            if (!string.IsNullOrWhiteSpace(online) &&
                (status == null || status.text != online))
            {
                SetStatus(online, Gold);
            }
        }

        private static void RebindButton(
            GameObject root,
            UnityEngine.Events.UnityAction action)
        {
            Button button = root.GetComponent<Button>() ??
                            root.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Graphic>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void LayoutActionButtons(IReadOnlyList<GameObject> visible)
        {
            if (visible.Count == 0) return;
            RectTransform panelRect =
                actionPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax =
                new Vector2(0.5f, 0.405f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            const float width = 92f;
            const float height = 92f;
            const float gap = 14f;
            panelRect.sizeDelta = new Vector2(
                width * visible.Count + gap * (visible.Count - 1),
                height);
            for (int index = 0; index < visible.Count; index++)
            {
                RectTransform rect =
                    visible[index].GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax =
                    new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = new Vector2(
                    (index - (visible.Count - 1) * 0.5f) *
                    (width + gap),
                    0f);
            }
        }

        private static Text FindLifeValue(GameObject panel)
        {
            return panel == null
                ? null
                : panel.GetComponentsInChildren<Text>(true)
                    .Where(text =>
                        int.TryParse(
                            text.text.Replace(".", string.Empty)
                                .Replace(",", string.Empty),
                            out _))
                    .OrderByDescending(text => text.fontSize)
                    .FirstOrDefault();
        }

        private static void DisableLegacyDuplicate()
        {
            foreach (CardArenaBootstrap arena in
                     FindObjectsByType<CardArenaBootstrap>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (arena != null && !arena.primaryDuelInterface)
                    arena.gameObject.SetActive(false);
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var root = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            DontDestroyOnLoad(root);
        }

        private static bool TryRaycastZone(
            Vector2 screenPosition,
            out DuelZone3D zone)
        {
            zone = null;
            if (Camera.main == null) return false;
            Ray ray = Camera.main.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 100f))
                return false;
            zone = hit.collider.GetComponentInParent<DuelZone3D>();
            return zone != null;
        }

        private static GameObject FindObject(
            Transform parent,
            string objectName)
        {
            Transform found = FindTransform(parent, objectName);
            return found != null ? found.gameObject : null;
        }

        private static RectTransform FindRect(
            Transform parent,
            string objectName)
        {
            return FindTransform(parent, objectName) as RectTransform;
        }

        private static Transform FindTransform(
            Transform parent,
            string objectName)
        {
            if (parent == null) return null;
            if (string.Equals(parent.name, objectName, StringComparison.Ordinal))
                return parent;
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform found =
                    FindTransform(parent.GetChild(index), objectName);
                if (found != null) return found;
            }
            return null;
        }

        private static RectTransform CreateRect(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 size)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            Image image = CreateImage(
                parent,
                name,
                anchorMin,
                anchorMax,
                color);
            return image.gameObject;
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = root.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string value,
            int size,
            FontStyle style,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor alignment)
        {
            var root = new GameObject(
                value,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            root.transform.SetParent(parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = root.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color accent,
            UnityEngine.Events.UnityAction action)
        {
            GameObject panel = CreatePanel(
                parent,
                name,
                anchorMin,
                anchorMax,
                new Color(0.02f, 0.08f, 0.12f, 0.98f));
            AddOutline(panel, accent);
            var button = panel.AddComponent<Button>();
            button.targetGraphic = panel.GetComponent<Image>();
            button.onClick.AddListener(action);
            CreateText(
                panel.transform,
                label,
                14,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            return button;
        }

        private static void AddOutline(GameObject root, Color color)
        {
            Outline outline = root.GetComponent<Outline>() ??
                              root.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static Color Hex(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color color)
                ? color
                : Color.white;
        }
    }
}
