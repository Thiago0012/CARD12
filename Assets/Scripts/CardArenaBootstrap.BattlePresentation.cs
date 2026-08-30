using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Frontend;
using ArcaneArena.Multiplayer;
using ArcaneArena.Presentation;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena
{
    /// <summary>
    /// Presentation-only layer for battle, damage and phase navigation.
    /// The authoritative duel state and every legal transition continue to
    /// come exclusively from ygopro-core.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private const float DuelConclusionSlowMotionDuration = 0.62f;
        private static readonly Color LocalTurnBlue =
            new(0.12f, 0.62f, 1f, 1f);
        private static readonly Color OpponentTurnRed =
            new(1f, 0.22f, 0.30f, 1f);

        private sealed class ArenaAnnouncement
        {
            public string Title;
            public string Subtitle;
            public Color Accent;
            public float Hold;
            public uint DisplayPhase;
            public bool TurnFlow;
            public DrawPresentationRequest Draw;
        }

        private readonly Queue<ArenaAnnouncement> announcementQueue = new();
        private readonly DuelChoice[] phaseNodeChoices = new DuelChoice[6];
        private readonly GameObject[] phaseNodes = new GameObject[6];
        private readonly Button[] phaseNodeButtons = new Button[6];
        private readonly Text[] phaseNodeLabels = new Text[6];
        private readonly DuelPhaseNodeGraphic[] phaseNodeSurfaces =
            new DuelPhaseNodeGraphic[6];

        private GameObject announcementRoot;
        private CanvasGroup announcementGroup;
        private Image announcementAccent;
        private Text announcementTitle;
        private Text announcementSubtitle;
        private DuelHudSurfaceGraphic announcementSurface;
        private Coroutine announcementRoutine;
        private bool openingTitlePresentationActive;

        private GameObject battleHud;
        private CanvasGroup battleHudGroup;
        private Text battleHudTitle;
        private Text battleHudSubtitle;
        private DuelHudSurfaceGraphic battleHudSurface;
        private LineRenderer battlePresentationLine;
        private Material battlePresentationMaterial;
        private Coroutine battlePresentationRoutine;
        private Coroutine duelConclusionSlowMotionRoutine;
        private Coroutine arenaCameraShakeRoutine;
        private Transform shakenCameraTransform;
        private Vector3 shakenCameraOrigin;
        private Coroutine localDamageVignetteRoutine;
        private GameObject localDamageVignette;
        private float duelConclusionOriginalTimeScale = 1f;
        private bool duelConclusionTimeScaleApplied;
        private float damagePresentationDeadline = -1f;
        private readonly Coroutine[] lifePointPresentationRoutines =
            new Coroutine[2];
        private readonly GameObject[] lifePointFloatingOverlays =
            new GameObject[2];
        private readonly bool[] lifePointPresentationOverride =
            new bool[2];
        private readonly int[] presentedLifePoints = new int[2];
        private DuelEvent latestBattleEvent;
        private Transform animatedBattleCard;
        private Vector3 animatedBattleCardPosition;
        private Vector3 animatedBattleCardScale;
        private DuelZone3D animatedBattleTarget;

        private GameObject phaseNavigator;
        private Text phaseNavigatorSubtitle;
        private Sprite phaseCircleSprite;
        private Texture2D phaseCircleTexture;
        private DuelPhaseControlGraphic phaseControlSurface;
        private DuelTurnFieldGlowGraphic turnOwnershipGlow;
        private CanvasGroup turnOwnershipGlowGroup;
        private Coroutine turnOwnershipGlowRoutine;
        private int lastTurnOwnerVisual = -1;
        private bool turnOwnershipActionObserved;

        private void BuildArenaPresentation()
        {
            BuildTurnOwnershipGlow();
            BuildPileCounterPresentation();
            BuildAnnouncementBanner();
            BuildBattleHud();
            BuildPhaseNavigator();
            BuildDuelExperience();
            PolishPhaseControl();
            RefreshTurnOwnershipVisuals(false);
            RefreshPileCounterPresentation(true);
        }

        private void DisposeArenaPresentation()
        {
            DisposePileCounterPresentation();
            DisposeDuelExperience();
            announcementQueue.Clear();
            if (announcementRoutine != null)
                StopCoroutine(announcementRoutine);
            ResetTurnFlowPresentation(true);
            if (battlePresentationRoutine != null)
                StopCoroutine(battlePresentationRoutine);
            if (duelConclusionSlowMotionRoutine != null)
                StopCoroutine(duelConclusionSlowMotionRoutine);
            RestoreDuelConclusionTimeScale();
            if (turnOwnershipGlowRoutine != null)
                StopCoroutine(turnOwnershipGlowRoutine);
            StopArenaCameraShake();
            StopLocalDamageVignette();
            ResetLifePointPresentations();
            ResetBattlePresentationVisuals();
            if (battlePresentationMaterial != null)
                Destroy(battlePresentationMaterial);
            if (phaseCircleSprite != null)
                Destroy(phaseCircleSprite);
            if (phaseCircleTexture != null)
                Destroy(phaseCircleTexture);
        }

        private void BuildTurnOwnershipGlow()
        {
            GameObject glowObject = new(
                "Transição Visual do Dono do Turno",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelTurnFieldGlowGraphic),
                typeof(CanvasGroup));
            glowObject.transform.SetParent(frame, false);
            RectTransform rect = glowObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            turnOwnershipGlow = glowObject.GetComponent<DuelTurnFieldGlowGraphic>();
            turnOwnershipGlow.raycastTarget = false;
            turnOwnershipGlowGroup = glowObject.GetComponent<CanvasGroup>();
            turnOwnershipGlowGroup.alpha = 0f;
            turnOwnershipGlowGroup.interactable = false;
            turnOwnershipGlowGroup.blocksRaycasts = false;
            glowObject.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// Mantém o controle lateral azul/vermelho durante o turno e executa
        /// a névoa somente quando a posse realmente muda. A névoa é uma
        /// confirmação transitória, não um filtro persistente sobre a arena.
        /// </summary>
        private void RefreshTurnOwnershipVisuals(
            bool forcePulse = false,
            byte? ownerOverride = null)
        {
            byte owner = ownerOverride ?? state?.TurnPlayer ?? 0;
            bool localTurn = owner == 0;
            Color accent = localTurn ? LocalTurnBlue : OpponentTurnRed;
            phaseControlSurface?.SetStyle(accent, true);

            int ownerIndex = localTurn ? 0 : 1;
            if (!forcePulse && ownerIndex == lastTurnOwnerVisual)
                return;

            lastTurnOwnerVisual = ownerIndex;
            turnOwnershipActionObserved = false;
            if (turnOwnershipGlow == null || turnOwnershipGlowGroup == null)
                return;

            turnOwnershipGlow.SetTurn(localTurn, accent);
            if (turnOwnershipGlowRoutine != null)
                StopCoroutine(turnOwnershipGlowRoutine);
            turnOwnershipGlowRoutine =
                StartCoroutine(PlayTurnOwnershipTransition());
        }

        private IEnumerator PlayTurnOwnershipTransition()
        {
            const float fadeInSeconds = 0.24f;
            const float minimumStrongHoldSeconds = 1.75f;
            const float maximumVisibleSeconds = 4.60f;
            const float fadeOutSeconds = 0.85f;

            turnOwnershipGlowGroup.alpha = 0f;
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < fadeInSeconds)
            {
                float t = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) / fadeInSeconds);
                turnOwnershipGlowGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }

            turnOwnershipGlowGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(
                minimumStrongHoldSeconds);

            // A indicação permanece presente para orientar o jogador, mas
            // cai para uma intensidade discreta. Assim que a primeira ação
            // real do turno acontece, ela pode desaparecer sem encobrir o
            // campo. Caso ninguém aja, há um limite para não virar filtro.
            float visibleStartedAt = Time.realtimeSinceStartup;
            while (!turnOwnershipActionObserved &&
                   Time.realtimeSinceStartup - visibleStartedAt <
                       maximumVisibleSeconds - minimumStrongHoldSeconds)
            {
                turnOwnershipGlowGroup.alpha = Mathf.MoveTowards(
                    turnOwnershipGlowGroup.alpha,
                    0.68f,
                    Time.unscaledDeltaTime * 0.72f);
                yield return null;
            }

            startedAt = Time.realtimeSinceStartup;
            float fadeStartAlpha = turnOwnershipGlowGroup.alpha;
            while (Time.realtimeSinceStartup - startedAt < fadeOutSeconds)
            {
                float t = Mathf.Clamp01(
                    (Time.realtimeSinceStartup - startedAt) / fadeOutSeconds);
                turnOwnershipGlowGroup.alpha =
                    Mathf.Lerp(
                        fadeStartAlpha,
                        0f,
                        Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            turnOwnershipGlowGroup.alpha = 0f;
            turnOwnershipGlowRoutine = null;
        }

        private static DuelHudSurfaceGraphic AttachDuelSurface(
            GameObject root,
            string objectName,
            Color accent,
            bool strongOnLeft,
            float opacity,
            bool directional,
            float chamfer = 10f)
        {
            if (root == null)
                return null;
            Image legacy = root.GetComponent<Image>();
            if (legacy != null)
            {
                legacy.color = Color.clear;
                legacy.raycastTarget = false;
            }
            Outline outline = root.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = Color.clear;
                outline.effectDistance = Vector2.zero;
            }
            Transform existing = root.transform.Find(objectName);
            GameObject surfaceObject = existing != null
                ? existing.gameObject
                : new GameObject(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(DuelHudSurfaceGraphic));
            if (existing == null)
                surfaceObject.transform.SetParent(root.transform, false);
            RectTransform rect = surfaceObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            DuelHudSurfaceGraphic surface =
                surfaceObject.GetComponent<DuelHudSurfaceGraphic>();
            surface.raycastTarget = false;
            surface.SetStyle(
                accent,
                strongOnLeft,
                opacity,
                directional,
                chamfer);
            surfaceObject.transform.SetAsFirstSibling();
            return surface;
        }

        private void HandleArenaPresentationEvent(DuelEvent duelEvent)
        {
            if (duelEvent == null)
                return;
            if (BeginsVisibleTurnAction(duelEvent.Message))
                turnOwnershipActionObserved = true;
            if (!replayingDeferredPresentation)
                HandleDuelExperienceEvent(duelEvent);
            if (DeferBattlePresentationIfNeeded(duelEvent))
                return;

            switch (duelEvent.Message)
            {
                case CoreMessage.NewTurn:
                    RefreshTurnOwnershipVisuals(true, duelEvent.Player);
                    QueueAnnouncement(
                        duelEvent.Player == 0
                            ? "SEU TURNO"
                            : "TURNO DO OPONENTE",
                        $"TURNO {Mathf.Max(1, state?.TurnNumber ?? 1)}",
                        duelEvent.Player == 0 ? LocalTurnBlue : OpponentTurnRed,
                        0.48f,
                        0x001U,
                        true);
                    break;
                case CoreMessage.NewPhase:
                    byte turnPlayer = state?.TurnPlayer ?? 0;
                    QueueAnnouncement(
                        CoreMessageDecoder.PhaseName(duelEvent.Value)
                            .ToUpperInvariant(),
                        turnPlayer == 0
                            ? "VOCÊ TEM A PRIORIDADE"
                            : "O OPONENTE TEM A PRIORIDADE",
                        turnPlayer == 0 ? LocalTurnBlue : OpponentTurnRed,
                        IsMajorPhase(duelEvent.Value) ? 0.66f : 0.42f,
                        duelEvent.Value,
                        true);
                    break;
                case CoreMessage.Draw:
                    QueueDrawPresentation(duelEvent);
                    break;
                case CoreMessage.Attack:
                    latestBattleEvent = null;
                    StartBattlePresentation(duelEvent);
                    break;
                case CoreMessage.Battle:
                    latestBattleEvent = duelEvent;
                    UpdateBattleHud(duelEvent);
                    break;
                case CoreMessage.AttackDisabled:
                    latestBattleEvent = null;
                    if (battlePresentationRoutine != null)
                    {
                        StopCoroutine(battlePresentationRoutine);
                        battlePresentationRoutine = null;
                    }
                    ResetBattlePresentationVisuals();
                    SetDuelExperienceObscured(false);
                    criticalInteractionLocked = false;
                    ResetPromptPresentationIdentity();
                    RefreshEverything(true);
                    StartCoroutine(
                        ShowTimedBattleStatus(
                            "ATAQUE NEGADO",
                            "O ataque foi interrompido.",
                            Red,
                            0.62f));
                    break;
                case CoreMessage.DamageStepStart:
                    SetStatus("ETAPA DE DANO · calculando a batalha...", Gold);
                    break;
                case CoreMessage.DamageStepEnd:
                    SetStatus("ETAPA DE DANO CONCLUÍDA", Muted);
                    break;
                case CoreMessage.Damage:
                    StartLifePointPresentation(
                        duelEvent.Player,
                        duelEvent.Value,
                        false);
                    break;
                case CoreMessage.Recover:
                    StartLifePointPresentation(
                        duelEvent.Player,
                        duelEvent.Value,
                        true);
                    break;
                case CoreMessage.Win:
                    if (duelConclusionSlowMotionRoutine != null)
                        StopCoroutine(duelConclusionSlowMotionRoutine);
                    RestoreDuelConclusionTimeScale();
                    duelConclusionSlowMotionRoutine = StartCoroutine(
                        PlayDuelConclusionSlowMotion());
                    break;
            }
        }

        private static bool BeginsVisibleTurnAction(CoreMessage message)
        {
            switch (message)
            {
                case CoreMessage.Summoning:
                case CoreMessage.SpecialSummoning:
                case CoreMessage.FlipSummoning:
                case CoreMessage.Set:
                case CoreMessage.Chaining:
                case CoreMessage.Attack:
                case CoreMessage.PositionChange:
                    return true;
                default:
                    return false;
            }
        }

        private bool PrepareArenaPresentationCapture(string captureState)
        {
            if (PrepareDuelExperienceCapture(captureState))
                return true;

            if (string.Equals(
                    captureState,
                    "phase-navigator",
                    StringComparison.OrdinalIgnoreCase))
            {
                DuelPrompt prompt = core?.CurrentPrompt;
                List<DuelChoice> phases =
                    DuelPromptPresentationRules.PhaseChoices(prompt);
                if (phases.Count > 0)
                {
                    OpenPhaseNavigator(prompt, phases);
                    return true;
                }
            }

            if (string.Equals(
                    captureState,
                    "battle",
                    StringComparison.OrdinalIgnoreCase))
            {
                StartCoroutine(PlayBattleCaptureFixture());
                return true;
            }

            return false;
        }

        private void BuildAnnouncementBanner()
        {
            announcementRoot = CreatePanel(
                frame,
                "Anúncio de Turno e Fase",
                new Vector2(0.215f, 0.445f),
                new Vector2(0.785f, 0.565f),
                Color.clear);
            announcementRoot.transform.SetAsLastSibling();
            announcementGroup =
                announcementRoot.AddComponent<CanvasGroup>();
            announcementGroup.alpha = 0f;
            announcementGroup.interactable = false;
            announcementGroup.blocksRaycasts = false;
            Image background = announcementRoot.GetComponent<Image>();
            background.raycastTarget = false;
            announcementSurface = AttachDuelSurface(
                announcementRoot,
                "Moldura do Anúncio",
                Cyan,
                true,
                0.91f,
                false,
                14f);

            announcementAccent = CreateImage(
                announcementRoot.transform,
                "Linha de Energia",
                new Vector2(0f, 0f),
                new Vector2(0.009f, 1f),
                Cyan);
            announcementAccent.raycastTarget = false;
            announcementTitle = CreateText(
                announcementRoot.transform,
                "FASE PRINCIPAL 1",
                27,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.38f),
                new Vector2(0.93f, 0.88f),
                TextAnchor.MiddleCenter);
            announcementSubtitle = CreateText(
                announcementRoot.transform,
                "VOCÊ TEM A PRIORIDADE",
                13,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.06f, 0.08f),
                new Vector2(0.94f, 0.38f),
                TextAnchor.MiddleCenter);
            announcementTitle.raycastTarget = false;
            announcementSubtitle.raycastTarget = false;
            announcementRoot.SetActive(false);
        }

        private void BuildBattleHud()
        {
            battleHud = CreatePanel(
                frame,
                "Apresentação da Batalha",
                new Vector2(0.31f, 0.75f),
                new Vector2(0.69f, 0.855f),
                Color.clear);
            battleHud.transform.SetAsLastSibling();
            battleHudGroup = battleHud.AddComponent<CanvasGroup>();
            battleHudGroup.alpha = 0f;
            battleHudGroup.interactable = false;
            battleHudGroup.blocksRaycasts = false;
            battleHud.GetComponent<Image>().raycastTarget = false;
            battleHudSurface = AttachDuelSurface(
                battleHud,
                "Moldura da Batalha",
                Gold,
                true,
                0.92f,
                false,
                12f);
            Image topLine = CreateImage(
                battleHud.transform,
                "Linha de Ataque",
                new Vector2(0.10f, 0.95f),
                new Vector2(1f, 1f),
                new Color(Gold.r, Gold.g, Gold.b, 0.78f));
            topLine.raycastTarget = false;
            battleHudTitle = CreateText(
                battleHud.transform,
                "DECLARAÇÃO DE ATAQUE",
                20,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.04f, 0.43f),
                new Vector2(0.96f, 0.88f),
                TextAnchor.MiddleCenter);
            battleHudSubtitle = CreateText(
                battleHud.transform,
                "ATK",
                13,
                FontStyle.Bold,
                Gold,
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.43f),
                TextAnchor.MiddleCenter);
            battleHudTitle.raycastTarget = false;
            battleHudSubtitle.raycastTarget = false;
            battleHud.SetActive(false);
        }

        private void BuildPhaseNavigator()
        {
            phaseNavigator = CreatePanel(
                frame,
                "Navegador Profissional de Fases",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0.006f, 0.018f, 0.56f));
            phaseNavigator.transform.SetAsLastSibling();

            GameObject window = CreatePanel(
                phaseNavigator.transform,
                "Painel de Fases",
                new Vector2(0.10f, 0.15f),
                new Vector2(0.90f, 0.53f),
                Color.clear);
            AttachDuelSurface(
                window,
                "Moldura do Navegador",
                Cyan,
                true,
                0.96f,
                false,
                18f);
            CreateText(
                window.transform,
                "SELECIONE UMA FASE PARA AVANÇAR",
                21,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.04f, 0.82f),
                new Vector2(0.96f, 0.96f),
                TextAnchor.MiddleCenter);
            phaseNavigatorSubtitle = CreateText(
                window.transform,
                "TURNO 1",
                12,
                FontStyle.Bold,
                Muted,
                new Vector2(0.04f, 0.71f),
                new Vector2(0.96f, 0.81f),
                TextAnchor.MiddleCenter);

            Image rail = CreateImage(
                window.transform,
                "Trilho das Fases",
                new Vector2(0.085f, 0.465f),
                new Vector2(0.915f, 0.476f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.30f));
            rail.raycastTarget = false;

            string[] labels =
            {
                "COMPRA",
                "APOIO",
                "MAIN 1",
                "BATALHA",
                "MAIN 2",
                "FINAL"
            };
            for (int index = 0; index < phaseNodes.Length; index++)
            {
                float x = Mathf.Lerp(0.092f, 0.908f, index / 5f);
                GameObject node = CreatePanel(
                    window.transform,
                    $"Fase {labels[index]}",
                    new Vector2(x - 0.058f, 0.335f),
                    new Vector2(x + 0.058f, 0.605f),
                    Color.clear);
                Image legacyNodeImage = node.GetComponent<Image>();
                legacyNodeImage.color = Color.clear;
                legacyNodeImage.raycastTarget = false;
                GameObject surfaceObject = new(
                    "Superfície Angular da Fase",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(DuelPhaseNodeGraphic));
                surfaceObject.transform.SetParent(node.transform, false);
                RectTransform surfaceRect =
                    surfaceObject.GetComponent<RectTransform>();
                surfaceRect.anchorMin = Vector2.zero;
                surfaceRect.anchorMax = Vector2.one;
                surfaceRect.offsetMin = Vector2.zero;
                surfaceRect.offsetMax = Vector2.zero;
                DuelPhaseNodeGraphic nodeSurface =
                    surfaceObject.GetComponent<DuelPhaseNodeGraphic>();
                nodeSurface.raycastTarget = true;
                nodeSurface.SetState(Cyan, false, false);
                surfaceObject.transform.SetAsFirstSibling();
                Button button = node.AddComponent<Button>();
                button.targetGraphic = nodeSurface;
                ColorBlock nodeColors = button.colors;
                nodeColors.normalColor = Color.white;
                nodeColors.highlightedColor = new Color(1f, 1f, 1f, 1f);
                nodeColors.pressedColor = new Color(0.82f, 0.92f, 1f, 1f);
                nodeColors.disabledColor = new Color(0.38f, 0.44f, 0.48f, 0.62f);
                nodeColors.fadeDuration = 0.08f;
                button.colors = nodeColors;
                int captured = index;
                button.onClick.AddListener(
                    () => SubmitPhaseNode(captured));
                Text label = CreateText(
                    node.transform,
                    labels[index],
                    12,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.06f, 0.10f),
                    new Vector2(0.94f, 0.90f),
                    TextAnchor.MiddleCenter);
                label.raycastTarget = false;
                phaseNodes[index] = node;
                phaseNodeButtons[index] = button;
                phaseNodeLabels[index] = label;
                phaseNodeSurfaces[index] = nodeSurface;
            }

            Button cancel = CreateButton(
                window.transform,
                "Cancelar Navegação",
                "CANCELAR",
                new Vector2(0.40f, 0.055f),
                new Vector2(0.60f, 0.18f),
                Gold,
                ClosePhaseNavigatorFromUser);
            if (cancel != null)
            {
                DuelHudSurfaceGraphic cancelSurface = AttachDuelSurface(
                    cancel.gameObject,
                    "Superfície de Cancelamento",
                    Gold,
                    true,
                    0.88f,
                    false,
                    8f);
                cancelSurface.raycastTarget = true;
                cancel.targetGraphic = cancelSurface;
                foreach (Text cancelLabel in
                         cancel.GetComponentsInChildren<Text>(true))
                {
                    cancelLabel.raycastTarget = false;
                }
            }
            phaseNavigator.SetActive(false);
        }

        private void PolishPhaseControl()
        {
            if (phaseButton == null)
                return;
            GameObject controlRoot = phaseControlPanel != null
                ? phaseControlPanel
                : phaseButton.gameObject;
            foreach (Graphic graphic in
                     controlRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is Image)
                {
                    graphic.color = Color.clear;
                    graphic.raycastTarget = false;
                }
            }
            GameObject surfaceObject = new(
                "Superfície Moderna do Controle de Fases",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelPhaseControlGraphic));
            surfaceObject.transform.SetParent(controlRoot.transform, false);
            RectTransform surfaceRect =
                surfaceObject.GetComponent<RectTransform>();
            surfaceRect.anchorMin = Vector2.zero;
            surfaceRect.anchorMax = Vector2.one;
            surfaceRect.offsetMin = Vector2.zero;
            surfaceRect.offsetMax = Vector2.zero;
            phaseControlSurface =
                surfaceObject.GetComponent<DuelPhaseControlGraphic>();
            // Esta camada só desenha. O clique continua pertencendo ao Button
            // "Avançar Fase" original da cena, que já era funcional.
            phaseControlSurface.raycastTarget = false;
            phaseControlSurface.SetStyle(LocalTurnBlue, true);
            surfaceObject.transform.SetAsFirstSibling();

            Graphic inputGraphic =
                phaseButton.GetComponent<Graphic>() ??
                phaseControlPanel?.GetComponent<Graphic>();
            if (inputGraphic != null)
            {
                inputGraphic.color = Color.clear;
                inputGraphic.raycastTarget = true;
                phaseButton.targetGraphic = inputGraphic;
            }
            phaseButton.transition = Selectable.Transition.None;

            foreach (Text label in controlRoot.GetComponentsInChildren<Text>(true))
            {
                label.raycastTarget = false;
                label.color = Color.white;
                label.fontStyle = FontStyle.Bold;
                if (label != phaseLabel &&
                    label.text.IndexOf("FASES", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    label.color = Gold;
                    label.fontSize = Mathf.Min(label.fontSize, 12);
                }
            }
        }

        private void OpenPhaseNavigator(
            DuelPrompt prompt,
            IReadOnlyList<DuelChoice> choices)
        {
            if (InteractionLocked ||
                phaseNavigator == null ||
                prompt == null ||
                choices == null ||
                choices.Count == 0)
            {
                return;
            }

            OpenExclusiveDuelUiSurface(
                DuelUiSurfaceKind.PhaseNavigator,
                prompt);
            CloseChoiceModal();
            CloseZoneBrowser();
            ClearHandSelection();
            CloseCardDetails();
            SuppressAnnouncementBanner();
            SetDuelExperienceObscured(true);
            Array.Clear(phaseNodeChoices, 0, phaseNodeChoices.Length);
            foreach (DuelChoice choice in choices)
            {
                int target = PhaseChoiceIndex(choice);
                if (target >= 0 && target < phaseNodeChoices.Length)
                    phaseNodeChoices[target] = choice;
            }

            int current = PhaseIndex(state?.Phase ?? 0);
            for (int index = 0; index < phaseNodes.Length; index++)
            {
                bool legal = phaseNodeChoices[index] != null;
                bool active = index == current;
                DuelPhaseNodeGraphic surface = phaseNodeSurfaces[index];
                if (legal)
                {
                    surface?.SetState(Lime, true, active);
                    phaseNodeLabels[index].color = Color.white;
                }
                else if (active)
                {
                    surface?.SetState(Cyan, false, true);
                    phaseNodeLabels[index].color = Color.white;
                }
                else
                {
                    surface?.SetState(Muted, false, false);
                    phaseNodeLabels[index].color =
                        new Color(Muted.r, Muted.g, Muted.b, 0.58f);
                }
                phaseNodeButtons[index].interactable = legal;
            }

            phaseNavigatorSubtitle.text =
                $"TURNO {Mathf.Max(1, state?.TurnNumber ?? 1)}  ·  " +
                $"{CoreMessageDecoder.PhaseName(state?.Phase ?? 0).ToUpperInvariant()}";
            MarkPromptPresented(prompt);
            phaseNavigator.SetActive(true);
            phaseNavigator.transform.SetAsLastSibling();
        }

        private void ClosePhaseNavigator()
        {
            if (phaseNavigator != null)
                phaseNavigator.SetActive(false);
            MarkDuelUiSurfaceClosed(DuelUiSurfaceKind.PhaseNavigator);
            SetDuelExperienceObscured(false);
        }

        private void SubmitPhaseNode(int index)
        {
            // A visibilidade real é a autoridade do clique. O estado de
            // coordenação pode mudar no mesmo frame ao fechar painéis antigos;
            // não deve descartar uma escolha legal que o jogador acabou de ver.
            if (phaseNavigator?.activeInHierarchy != true)
                return;
            if (index < 0 || index >= phaseNodeChoices.Length)
                return;
            DuelChoice choice = phaseNodeChoices[index];
            if (choice == null)
                return;
            ClosePhaseNavigator();
            core.SubmitChoice(choice);
            RefreshEverything(true);
        }

        private static int PhaseChoiceIndex(DuelChoice choice)
        {
            string label = choice?.Label ?? string.Empty;
            if (label.IndexOf(
                    "Batalha",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;
            if (label.IndexOf(
                    "Principal 2",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return 4;
            if (label.IndexOf(
                    "Encerrar",
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return 5;
            return -1;
        }

        private static int PhaseIndex(uint phase)
        {
            if ((phase & 0x200U) != 0) return 5;
            if ((phase & 0x100U) != 0) return 4;
            if ((phase & 0x0F8U) != 0) return 3;
            if ((phase & 0x004U) != 0) return 2;
            if ((phase & 0x002U) != 0) return 1;
            return 0;
        }

        private void QueueAnnouncement(
            string title,
            string subtitle,
            Color accent,
            float hold,
            uint displayPhase = 0,
            bool turnFlow = false)
        {
            if (turnFlow)
                BeginTurnFlowPresentation();
            announcementQueue.Enqueue(
                new ArenaAnnouncement
                {
                    Title = title,
                    Subtitle = subtitle,
                    Accent = accent,
                    Hold = hold,
                    DisplayPhase = displayPhase,
                    TurnFlow = turnFlow
                });
            TryStartAnnouncementQueue();
        }

        private void TryStartAnnouncementQueue()
        {
            if (openingTitlePresentationActive ||
                announcementRoutine != null ||
                announcementQueue.Count == 0)
            {
                return;
            }
            announcementRoutine = StartCoroutine(PlayAnnouncementQueue());
        }

        private void SuspendAnnouncementsForOpening()
        {
            openingTitlePresentationActive = true;
            announcementQueue.Clear();
            if (announcementRoutine != null)
            {
                StopCoroutine(announcementRoutine);
                announcementRoutine = null;
            }
            if (announcementGroup != null)
                announcementGroup.alpha = 0f;
            if (announcementRoot != null)
                announcementRoot.SetActive(false);
            ResetTurnFlowPresentation(true);
        }

        private void ResumeAnnouncementsAfterOpening()
        {
            announcementQueue.Clear();
            if (announcementRoutine != null)
            {
                StopCoroutine(announcementRoutine);
                announcementRoutine = null;
            }
            ResetTurnFlowPresentation(true);

            uint phase = state?.Phase ?? 0x001U;
            byte turnPlayer = state?.TurnPlayer ?? 0;
            QueueAnnouncement(
                turnPlayer == 0 ? "SEU TURNO" : "TURNO DO OPONENTE",
                $"TURNO {Mathf.Max(1, state?.TurnNumber ?? 1)}",
                turnPlayer == 0 ? LocalTurnBlue : OpponentTurnRed,
                0.48f,
                phase,
                true);
            QueueAnnouncement(
                CoreMessageDecoder.PhaseName(phase).ToUpperInvariant(),
                turnPlayer == 0
                    ? "VOCÊ TEM A PRIORIDADE"
                    : "O OPONENTE TEM A PRIORIDADE",
                turnPlayer == 0 ? LocalTurnBlue : OpponentTurnRed,
                IsMajorPhase(phase) ? 0.66f : 0.42f,
                phase,
                true);

            openingTitlePresentationActive = false;
            TryStartAnnouncementQueue();
        }

        private IEnumerator PlayAnnouncementQueue()
        {
            bool completed = false;
            try
            {
                while (announcementQueue.Count > 0)
                {
                    ArenaAnnouncement item = announcementQueue.Dequeue();
                    if (item.TurnFlow)
                    {
                        presentationPhaseOverride = item.DisplayPhase;
                        UpdateLifeAndPhase();
                    }
                    if (item.Draw != null)
                    {
                        yield return PlayDrawPresentation(item.Draw);
                        continue;
                    }
                    announcementRoot.SetActive(true);
                    announcementRoot.transform.SetAsLastSibling();
                    announcementTitle.text = item.Title;
                    announcementSubtitle.text = item.Subtitle;
                    announcementSubtitle.color = item.Accent;
                    announcementAccent.color = item.Accent;
                    announcementSurface?.SetStyle(
                        item.Accent,
                        true,
                        0.91f,
                        false,
                        14f);

                    RectTransform rect =
                        announcementRoot.GetComponent<RectTransform>();
                    Vector3 startScale = new Vector3(0.82f, 0.92f, 1f);
                    float enter =
                        DuelAnimationPreferences.Duration(0.18f);
                    float enterStartedAt = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - enterStartedAt < enter)
                    {
                        float elapsed =
                            Time.realtimeSinceStartup - enterStartedAt;
                        float t = Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.Clamp01(elapsed / enter));
                        announcementGroup.alpha = t;
                        rect.localScale =
                            Vector3.Lerp(startScale, Vector3.one, t);
                        yield return null;
                    }
                    announcementGroup.alpha = 1f;
                    rect.localScale = Vector3.one;
                    yield return new WaitForSecondsRealtime(
                        DuelAnimationPreferences.Duration(item.Hold));
                    float exit =
                        DuelAnimationPreferences.Duration(0.16f);
                    float exitStartedAt = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - exitStartedAt < exit)
                    {
                        float elapsed =
                            Time.realtimeSinceStartup - exitStartedAt;
                        float t = Mathf.Clamp01(elapsed / exit);
                        announcementGroup.alpha = 1f - t;
                        rect.localScale =
                            Vector3.Lerp(
                                Vector3.one,
                                new Vector3(1.10f, 0.94f, 1f),
                                t);
                        yield return null;
                    }
                    announcementGroup.alpha = 0f;
                    announcementRoot.SetActive(false);
                }
                completed = true;
            }
            finally
            {
                announcementRoutine = null;
                if (completed)
                {
                    CompleteTurnFlowPresentation();
                }
                else
                {
                    announcementQueue.Clear();
                    ResetTurnFlowPresentation(true);
                    observedPrompt = null;
                    if (presentationReady)
                        RefreshEverything(true);
                }
            }
        }

        private void StartBattlePresentation(DuelEvent attack)
        {
            SuppressAnnouncementBanner();
            criticalInteractionLocked = true;
            SetDuelExperienceObscured(true);
            if (battlePresentationRoutine != null)
                StopCoroutine(battlePresentationRoutine);
            ResetBattlePresentationVisuals();
            battlePresentationRoutine =
                StartCoroutine(PlayAttackPresentation(attack));
        }

        private IEnumerator PlayAttackPresentation(DuelEvent attack)
        {
            DuelZone3D attackerZone = ZoneFor(attack.Previous);
            DuelZone3D targetZone =
                attack.DirectAttack ? null : ZoneFor(attack.Current);
            if (attackerZone == null)
            {
                ShowBattleStatus(
                    attack.DirectAttack
                        ? "ATAQUE DIRETO!"
                        : "DECLARAÇÃO DE ATAQUE",
                    attack.Detail,
                    Gold);
                yield return new WaitForSecondsRealtime(0.35f);
                HideBattleHud();
                SetDuelExperienceObscured(false);
                criticalInteractionLocked = false;
                battlePresentationRoutine = null;
                yield break;
            }

            uint attackerCode = CodeAt(attackerZone);
            uint targetCode = targetZone != null
                ? CodeAt(targetZone)
                : 0;
            string attackerName = CardName(attackerCode);
            string targetName = targetCode != 0
                ? CardName(targetCode)
                : "PONTOS DE VIDA";
            ShowBattleStatus(
                attack.DirectAttack
                    ? "ATAQUE DIRETO!"
                    : $"{attackerName} ATACA!",
                $"{attackerName}  →  {targetName}",
                attack.DirectAttack ? Red : Gold);

            // MSG_ATTACK is the authoritative declaration: attacker and
            // target are already fixed, but the Core may now ask either
            // player for a Chain response.  Keep the declaration visible and
            // interaction unlocked until MSG_BATTLE confirms that every
            // response has resolved.  MSG_ATTACK_DISABLED cancels this
            // coroutine through the normal event path.
            criticalInteractionLocked = false;
            ResetPromptPresentationIdentity();
            RefreshEverything(true);
            while (latestBattleEvent == null && core != null)
            {
                EnsureRequiredResponseTrayVisible();
                yield return null;
            }
            if (core == null)
            {
                HideBattleHud();
                SetDuelExperienceObscured(false);
                battlePresentationRoutine = null;
                yield break;
            }
            criticalInteractionLocked = true;

            Transform card =
                attackerZone.FindPresentedCard();
            if (card == null)
            {
                ValidatePresentationConsistency(attack, true);
                card = attackerZone.FindPresentedCard();
            }
            bool transientAttacker = false;
            if (card == null && attack.Code != 0)
            {
                byte controller = StatePlayerForZone(attackerZone);
                uint sequence = (uint)Mathf.Max(
                    0,
                    SequenceFor(attackerZone));
                CreateWorldCard(
                    attackerZone,
                    new CardInstanceKey(
                        SyntheticZoneRuntimeId(attackerZone),
                        attack.Code,
                        controller,
                        controller,
                         (byte)LocationFor(attackerZone.Kind),
                         sequence,
                         FaceUpAttack),
                    SpriteFor(attack.Code),
                    FaceUpAttack,
                    null);
                card = attackerZone.transform.Find("Carta Invocada");
                transientAttacker = card != null;
            }
            if (card == null)
            {
                DuelDevelopmentLog.Write(
                    DuelLogCategory.Error,
                    $"Attack animation cancelled: attacker has no view; " +
                    $"controller={attack.Previous?.Controller}; " +
                    $"location={attack.Previous?.Location:X2}; " +
                    $"sequence={attack.Previous?.Sequence}; " +
                    $"code={attackerCode:00000000}.",
                    this);
                ShowBattleStatus(
                    "ATAQUE EM RESSINCRONIZACAO",
                    "A arena aguardou a representacao autoritativa do atacante.",
                    Gold);
                yield return new WaitForSecondsRealtime(0.28f);
                HideBattleHud();
                SetDuelExperienceObscured(false);
                criticalInteractionLocked = false;
                battlePresentationRoutine = null;
                yield break;
            }
            Vector3 start = card != null
                ? card.position
                : attackerZone.transform.position + Vector3.up * 0.25f;
            Vector3 end = targetZone != null
                ? targetZone.transform.position + Vector3.up * 0.32f
                : DirectAttackPoint(attackerZone.Owner);
            Vector3 direction = (end - start).normalized;
            Vector3 lunge = Vector3.Lerp(start, end, 0.38f);
            Vector3 originalPosition =
                card != null ? card.position : start;
            Vector3 originalScale =
                card != null ? card.localScale : Vector3.one;
            animatedBattleCard = card;
            animatedBattleCardPosition = originalPosition;
            animatedBattleCardScale = originalScale;
            animatedBattleTarget = targetZone;

            EnsureBattlePresentationLine();
            battlePresentationLine.enabled = true;
            battlePresentationLine.startColor = Cyan;
            battlePresentationLine.endColor =
                attack.DirectAttack ? Red : Gold;
            battlePresentationLine.SetPosition(0, start);
            battlePresentationLine.SetPosition(1, start);

            float windup =
                DuelAnimationPreferences.MonsterDuration(0.12f);
            for (float elapsed = 0f;
                 elapsed < windup && card != null;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / windup));
                card.position =
                    Vector3.Lerp(
                        originalPosition,
                        originalPosition - direction * 0.18f,
                        t);
                card.localScale =
                    Vector3.Lerp(
                        originalScale,
                        originalScale * 1.07f,
                        t);
                yield return null;
            }

            float travel =
                DuelAnimationPreferences.MonsterDuration(0.24f);
            for (float elapsed = 0f;
                 elapsed < travel;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = 1f -
                          Mathf.Pow(
                              1f - Mathf.Clamp01(elapsed / travel),
                              3f);
                Vector3 beamEnd = Vector3.Lerp(start, end, t);
                battlePresentationLine.SetPosition(1, beamEnd);
                if (card != null)
                {
                    card.position =
                        Vector3.Lerp(
                            originalPosition - direction * 0.18f,
                            lunge,
                            t);
                    card.localScale =
                        originalScale *
                        Mathf.Lerp(1.07f, 1.14f, t);
                }
                yield return null;
            }

            battlePresentationLine.SetPosition(1, end);
            if (targetZone != null)
                targetZone.SetDropHighlight(true);
            StartArenaCameraShake(0.16f, 0.036f);
            yield return new WaitForSecondsRealtime(
                DuelAnimationPreferences.MonsterDuration(0.12f));
            if (latestBattleEvent != null)
                UpdateBattleHud(latestBattleEvent);

            float recover =
                DuelAnimationPreferences.MonsterDuration(0.22f);
            for (float elapsed = 0f;
                 elapsed < recover && card != null;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / recover));
                card.position =
                    Vector3.Lerp(lunge, originalPosition, t);
                card.localScale =
                    Vector3.Lerp(
                        originalScale * 1.14f,
                        originalScale,
                        t);
                yield return null;
            }
            if (card != null)
            {
                card.position = originalPosition;
                card.localScale = originalScale;
            }
            if (targetZone != null)
                targetZone.SetDropHighlight(false);
            yield return new WaitForSecondsRealtime(
                DuelAnimationPreferences.Duration(0.24f));
            battlePresentationLine.enabled = false;
            HideBattleHud();
            animatedBattleCard = null;
            animatedBattleTarget = null;
            if (transientAttacker)
            {
                ClearWorldCard(attackerZone);
                attackerZone.ClearPlacedCard();
            }
            SetDuelExperienceObscured(false);
            criticalInteractionLocked = false;
            battlePresentationRoutine = null;
        }

        private IEnumerator ShowTimedBattleStatus(
            string title,
            string subtitle,
            Color accent,
            float hold)
        {
            SuppressAnnouncementBanner();
            ShowBattleStatus(title, subtitle, accent);
            yield return new WaitForSecondsRealtime(
                DuelAnimationPreferences.Duration(hold));
            HideBattleHud();
        }

        private void UpdateBattleHud(DuelEvent battle)
        {
            if (battleHud == null || !battleHud.activeSelf)
                return;
            if (battle.DirectAttack)
            {
                battleHudSubtitle.text =
                    $"ATK {battle.AttackerAttack:N0}  ·  DANO DIRETO";
                return;
            }
            int targetValue =
                Mathf.Max(battle.TargetAttack, battle.TargetDefense);
            battleHudSubtitle.text =
                $"ATK {battle.AttackerAttack:N0}   ×   " +
                $"{targetValue:N0}" +
                (battle.TargetDestroyed ? "   ·   DESTRUÍDO" : string.Empty);
            battleHudSubtitle.color =
                battle.TargetDestroyed ? Red : Gold;
        }

        private void ShowBattleStatus(
            string title,
            string subtitle,
            Color accent)
        {
            if (battleHud == null)
                return;
            battleHud.SetActive(true);
            battleHud.transform.SetAsLastSibling();
            battleHudGroup.alpha = 1f;
            battleHudTitle.text = title;
            battleHudSubtitle.text = subtitle ?? string.Empty;
            battleHudSubtitle.color = accent;
            battleHudSurface?.SetStyle(
                accent,
                true,
                0.92f,
                false,
                12f);
        }

        private void HideBattleHud()
        {
            if (battleHud == null)
                return;
            battleHudGroup.alpha = 0f;
            battleHud.SetActive(false);
        }

        private void ResetBattlePresentationVisuals()
        {
            if (animatedBattleCard != null)
            {
                animatedBattleCard.position =
                    animatedBattleCardPosition;
                animatedBattleCard.localScale =
                    animatedBattleCardScale;
            }
            if (animatedBattleTarget != null)
                animatedBattleTarget.SetDropHighlight(false);
            if (battlePresentationLine != null)
                battlePresentationLine.enabled = false;
            animatedBattleCard = null;
            animatedBattleTarget = null;
            HideBattleHud();
        }

        private void SuppressAnnouncementBanner()
        {
            announcementQueue.Clear();
            if (announcementRoutine != null)
            {
                StopCoroutine(announcementRoutine);
                announcementRoutine = null;
            }
            if (announcementGroup != null)
                announcementGroup.alpha = 0f;
            if (announcementRoot != null)
                announcementRoot.SetActive(false);
            ResetTurnFlowPresentation(true);
        }

        private void StartLifePointPresentation(
            byte player,
            uint value,
            bool recovering)
        {
            if (player > 1 || value == 0 || state == null)
                return;
            int index = player;
            int target = Mathf.Max(0, state.Players[index].LifePoints);
            int expectedStart = recovering
                ? Mathf.Max(0, target - (int)Math.Min(value, int.MaxValue))
                : target + (int)Math.Min(
                    value,
                    (uint)Mathf.Max(0, int.MaxValue - target));
            int start = lifePointPresentationOverride[index]
                ? presentedLifePoints[index]
                : expectedStart;

            if (lifePointPresentationRoutines[index] != null)
                StopCoroutine(lifePointPresentationRoutines[index]);
            if (lifePointFloatingOverlays[index] != null)
                Destroy(lifePointFloatingOverlays[index]);
            RestoreLifePointTextPresentation(player);

            lifePointPresentationOverride[index] = true;
            presentedLifePoints[index] = start;
            ApplyPresentedLifePointValue(player);
            lifePointPresentationRoutines[index] = StartCoroutine(
                PlayLifePointPresentation(
                    player,
                    value,
                    recovering,
                    start,
                    target));
        }

        private IEnumerator PlayLifePointPresentation(
            byte player,
            uint value,
            bool recovering,
            int start,
            int target)
        {
            int index = player;
            ArcaneAudioDirector audio = core != null
                ? core.GetComponent<ArcaneAudioDirector>()
                : null;
            float impactLead = 0f;
            if (!recovering)
            {
                impactLead = audio != null
                    ? audio.PlayDamageImpactCue()
                    : 0f;
                bool localPlayerWasHit = player == 0;
                StartArenaCameraShake(
                    localPlayerWasHit ? 0.30f : 0.20f,
                    localPlayerWasHit ? 0.105f : 0.052f);
                if (localPlayerWasHit)
                {
                    StartLocalDamageVignette(
                        audio != null
                            ? audio.DamageImpactCueDuration
                            : 0.38f);
                }
            }
            float lifeCountDuration = audio != null
                ? audio.LifePointLossCueDuration
                : 0.72f;
            const float travelDuration = 0.46f;
            damagePresentationDeadline = Mathf.Max(
                damagePresentationDeadline,
                Time.unscaledTime + impactLead + travelDuration +
                lifeCountDuration);
            if (impactLead > 0f)
                yield return new WaitForSecondsRealtime(impactLead);

            GameObject floating = CreatePanel(
                frame,
                recovering ? "Contador de Vida Recuperada" : "Contador de Dano",
                Vector2.zero,
                Vector2.one,
                Color.clear);
            lifePointFloatingOverlays[index] = floating;
            floating.transform.SetAsLastSibling();
            floating.GetComponent<Image>().raycastTarget = false;
            CanvasGroup group = floating.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Color accent = recovering
                ? new Color(0.45f, 1f, 0.28f, 1f)
                : new Color(1f, 0.22f, 0.30f, 1f);
            Text amount = CreateText(
                floating.transform,
                $"{(recovering ? "+" : "−")} {value:N0}",
                68,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                TextAnchor.MiddleCenter);
            amount.raycastTarget = false;
            Shadow shadow = amount.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(4f, -4f);
            Outline outline = amount.gameObject.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            RectTransform rect = amount.rectTransform;
            rect.sizeDelta = new Vector2(500f, 150f);
            Vector2 from = Vector2.zero;
            Vector2 destination = LifePointTextDestination(
                player,
                out float destinationScale);
            for (float elapsed = 0f;
                 elapsed < travelDuration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / travelDuration);
                float impact = Mathf.Clamp01(t / 0.20f);
                float travel = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01((t - 0.16f) / 0.84f));
                float scale = t < 0.20f
                    ? Mathf.Lerp(0.70f, 1.16f, EaseOutBack(impact))
                    : Mathf.Lerp(1.16f, destinationScale, travel);
                rect.anchoredPosition = Vector2.Lerp(from, destination, travel);
                rect.localScale = Vector3.one * scale;
                group.alpha = 1f;
                yield return null;
            }
            rect.anchoredPosition = destination;
            rect.localScale = Vector3.one * destinationScale;
            group.alpha = 1f;
            yield return null;
            Destroy(floating);
            lifePointFloatingOverlays[index] = null;

            float duration = audio != null
                ? audio.PlayLifePointLossCue()
                : lifeCountDuration;
            if (duration <= 0f)
                duration = lifeCountDuration;

            Text life = player == 0 ? localLife : opponentLife;
            Color originalColor = life != null ? life.color : Color.white;
            Vector3 originalScale = life != null
                ? life.rectTransform.localScale
                : Vector3.one;
            for (float elapsed = 0f;
                 elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                presentedLifePoints[index] = Mathf.RoundToInt(
                    Mathf.Lerp(start, target, eased));
                ApplyPresentedLifePointValue(player);
                if (life != null)
                {
                    life.color = Color.Lerp(accent, Color.white, t * 0.62f);
                    float pulse = Mathf.Sin(t * Mathf.PI) * 0.075f;
                    life.rectTransform.localScale = originalScale *
                        (1f + pulse);
                }
                yield return null;
            }

            presentedLifePoints[index] = target;
            lifePointPresentationOverride[index] = false;
            ApplyPresentedLifePointValue(player);
            if (life != null)
            {
                life.color = originalColor;
                life.rectTransform.localScale = originalScale;
            }
            lifePointPresentationRoutines[index] = null;
        }

        private Vector2 LifePointTextDestination(
            byte player,
            out float destinationScale)
        {
            Text life = player == 0 ? localLife : opponentLife;
            RectTransform lifeRect = life != null
                ? life.rectTransform
                : null;
            destinationScale = life != null
                ? Mathf.Clamp(life.fontSize / 68f, 0.28f, 0.68f)
                : 0.42f;
            if (lifeRect == null || frame == null)
            {
                return player == 0
                    ? new Vector2(-740f, -380f)
                    : new Vector2(740f, 380f);
            }

            Camera eventCamera = arenaCanvas != null &&
                                 arenaCanvas.renderMode !=
                                 RenderMode.ScreenSpaceOverlay
                ? arenaCanvas.worldCamera
                : null;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                lifeRect.TransformPoint(lifeRect.rect.center));
            if (Mathf.Abs(frame.lossyScale.x) > 0.0001f)
            {
                destinationScale *= Mathf.Abs(
                    lifeRect.lossyScale.x / frame.lossyScale.x);
            }
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                frame,
                screenPoint,
                eventCamera,
                out Vector2 localPoint)
                ? localPoint
                : Vector2.zero;
        }

        private int LifePointValueForDisplay(byte player, int authoritative)
        {
            return player <= 1 && lifePointPresentationOverride[player]
                ? presentedLifePoints[player]
                : authoritative;
        }

        private void ApplyPresentedLifePointValue(byte player)
        {
            Text life = player == 0 ? localLife : opponentLife;
            if (life == null || player > 1)
                return;
            int authoritative = state != null
                ? state.Players[player].LifePoints
                : presentedLifePoints[player];
            life.text = LifePointValueForDisplay(
                player,
                authoritative).ToString("N0");
        }

        private void RestoreLifePointTextPresentation(byte player)
        {
            Text life = player == 0 ? localLife : opponentLife;
            if (life == null)
                return;
            life.color = Color.white;
            life.rectTransform.localScale = Vector3.one;
        }

        private void ResetLifePointPresentations()
        {
            for (byte player = 0; player < 2; player++)
            {
                if (lifePointPresentationRoutines[player] != null)
                    StopCoroutine(lifePointPresentationRoutines[player]);
                lifePointPresentationRoutines[player] = null;
                if (lifePointFloatingOverlays[player] != null)
                    Destroy(lifePointFloatingOverlays[player]);
                lifePointFloatingOverlays[player] = null;
                lifePointPresentationOverride[player] = false;
                RestoreLifePointTextPresentation(player);
            }
        }

        private IEnumerator PlayDuelConclusionSlowMotion()
        {
            duelConclusionOriginalTimeScale = Time.timeScale;
            duelConclusionTimeScaleApplied = true;
            Time.timeScale = Mathf.Max(
                0.05f,
                duelConclusionOriginalTimeScale * 0.34f);
            yield return new WaitForSecondsRealtime(
                DuelConclusionSlowMotionDuration);
            RestoreDuelConclusionTimeScale();
            duelConclusionSlowMotionRoutine = null;
        }

        private void RestoreDuelConclusionTimeScale()
        {
            if (!duelConclusionTimeScaleApplied)
                return;
            Time.timeScale = duelConclusionOriginalTimeScale;
            duelConclusionTimeScaleApplied = false;
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float shifted = Mathf.Clamp01(value) - 1f;
            return 1f +
                   (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
        }

        private void StartArenaCameraShake(
            float baseDuration,
            float strength)
        {
            StopArenaCameraShake();
            Camera camera = Camera.main;
            if (camera == null)
                return;
            shakenCameraTransform = camera.transform;
            shakenCameraOrigin = shakenCameraTransform.localPosition;
            arenaCameraShakeRoutine = StartCoroutine(
                ShakeArenaCamera(baseDuration, strength));
        }

        private IEnumerator ShakeArenaCamera(
            float baseDuration,
            float strength)
        {
            Transform cameraTransform = shakenCameraTransform;
            Vector3 origin = shakenCameraOrigin;
            float duration =
                DuelAnimationPreferences.Duration(baseDuration);
            for (float elapsed = 0f;
                 elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float fade =
                    1f - Mathf.Clamp01(elapsed / duration);
                cameraTransform.localPosition =
                    origin +
                    new Vector3(
                        Mathf.Sin(elapsed * 83f),
                        Mathf.Cos(elapsed * 67f),
                        0f) *
                    strength *
                    fade;
                yield return null;
            }
            if (cameraTransform != null)
                cameraTransform.localPosition = origin;
            arenaCameraShakeRoutine = null;
            shakenCameraTransform = null;
        }

        private void StopArenaCameraShake()
        {
            if (arenaCameraShakeRoutine != null)
            {
                StopCoroutine(arenaCameraShakeRoutine);
                arenaCameraShakeRoutine = null;
            }
            if (shakenCameraTransform != null)
                shakenCameraTransform.localPosition = shakenCameraOrigin;
            shakenCameraTransform = null;
        }

        private void StartLocalDamageVignette(float duration)
        {
            StopLocalDamageVignette();
            if (frame == null)
                return;

            localDamageVignette = new GameObject(
                "Impacto Vermelho de Dano Local",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelDamageVignetteGraphic),
                typeof(CanvasGroup));
            localDamageVignette.transform.SetParent(frame, false);
            RectTransform rect =
                localDamageVignette.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            DuelDamageVignetteGraphic graphic =
                localDamageVignette.GetComponent<DuelDamageVignetteGraphic>();
            graphic.color = new Color(1f, 0.035f, 0.06f, 0.92f);
            graphic.raycastTarget = false;
            CanvasGroup group = localDamageVignette.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            localDamageVignette.transform.SetAsLastSibling();
            localDamageVignetteRoutine = StartCoroutine(
                PlayLocalDamageVignette(
                    group,
                    Mathf.Max(0.24f, duration)));
        }

        private IEnumerator PlayLocalDamageVignette(
            CanvasGroup group,
            float duration)
        {
            for (float elapsed = 0f;
                 elapsed < duration && group != null;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float envelope = t < 0.16f
                    ? Mathf.SmoothStep(0f, 1f, t / 0.16f)
                    : 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        (t - 0.16f) / 0.84f);
                float impactPulse =
                    0.88f + Mathf.Sin(t * Mathf.PI * 7f) * 0.12f;
                group.alpha = Mathf.Clamp01(envelope * impactPulse);
                yield return null;
            }

            if (localDamageVignette != null)
                Destroy(localDamageVignette);
            localDamageVignette = null;
            localDamageVignetteRoutine = null;
        }

        private void StopLocalDamageVignette()
        {
            if (localDamageVignetteRoutine != null)
            {
                StopCoroutine(localDamageVignetteRoutine);
                localDamageVignetteRoutine = null;
            }
            if (localDamageVignette != null)
                Destroy(localDamageVignette);
            localDamageVignette = null;
        }

        private void EnsureBattlePresentationLine()
        {
            if (battlePresentationLine != null)
                return;
            var root = new GameObject("Traço da Batalha");
            root.transform.SetParent(transform, false);
            battlePresentationLine =
                root.AddComponent<LineRenderer>();
            battlePresentationLine.useWorldSpace = true;
            battlePresentationLine.positionCount = 2;
            battlePresentationLine.startWidth = 0.20f;
            battlePresentationLine.endWidth = 0.055f;
            battlePresentationLine.numCapVertices = 8;
            battlePresentationLine.numCornerVertices = 6;
            battlePresentationLine.alignment =
                LineAlignment.View;
            battlePresentationLine.sortingOrder = 30;
            Shader shader =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                battlePresentationMaterial =
                    new Material(shader);
                battlePresentationLine.material =
                    battlePresentationMaterial;
            }
            battlePresentationLine.enabled = false;
        }

        private DuelZone3D ZoneFor(CardLocation location)
        {
            if (location == null || location.Location == 0)
                return null;
            return FindZone(
                location.Controller,
                location.Location,
                (int)location.Sequence);
        }

        private Vector3 DirectAttackPoint(DuelPlayerSide attacker)
        {
            DuelPlayerSide opponent =
                attacker == DuelPlayerSide.PlayerOne
                    ? DuelPlayerSide.PlayerTwo
                    : DuelPlayerSide.PlayerOne;
            DuelZone3D[] zones = AllZones()
                .Where(zone =>
                    zone.Owner == opponent &&
                    zone.Kind == DuelZoneKind.Monster)
                .ToArray();
            if (zones.Length == 0)
                return transform.position + Vector3.up * 0.5f;
            Vector3 center = Vector3.zero;
            foreach (DuelZone3D zone in zones)
                center += zone.transform.position;
            return center / zones.Length + Vector3.up * 0.38f;
        }

        private IEnumerator PlayBattleCaptureFixture()
        {
            SuppressAnnouncementBanner();
            SetDuelExperienceObscured(true);
            DuelZone3D attacker = AllZones()
                .FirstOrDefault(zone =>
                    zone.Owner == DuelPlayerSide.PlayerOne &&
                    zone.Kind == DuelZoneKind.Monster);
            DuelZone3D target = AllZones()
                .FirstOrDefault(zone =>
                    zone.Owner == DuelPlayerSide.PlayerTwo &&
                    zone.Kind == DuelZoneKind.Monster);
            ShowBattleStatus(
                "DECLARAÇÃO DE ATAQUE",
                "ATK 2.500   ×   1.800   ·   ALVO SELECIONADO",
                Gold);
            if (attacker != null && target != null)
            {
                EnsureBattlePresentationLine();
                battlePresentationLine.startColor = Cyan;
                battlePresentationLine.endColor = Gold;
                battlePresentationLine.SetPosition(
                    0,
                    attacker.transform.position + Vector3.up * 0.32f);
                battlePresentationLine.SetPosition(
                    1,
                    target.transform.position + Vector3.up * 0.32f);
                battlePresentationLine.enabled = true;
                target.SetDropHighlight(true);
            }
            yield return new WaitForSecondsRealtime(4f);
        }

        private Sprite CreateCircleSprite()
        {
            const int size = 128;
            phaseCircleTexture =
                new Texture2D(
                    size,
                    size,
                    TextureFormat.RGBA32,
                    false);
            phaseCircleTexture.name = "ArcanePhaseCircle";
            phaseCircleTexture.wrapMode = TextureWrapMode.Clamp;
            var colors = new Color32[size * size];
            Vector2 center = Vector2.one * (size - 1) * 0.5f;
            float radius = size * 0.49f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance =
                        Vector2.Distance(new Vector2(x, y), center);
                    float alpha =
                        Mathf.Clamp01(radius - distance + 1f);
                    colors[y * size + x] =
                        new Color(1f, 1f, 1f, alpha);
                }
            }
            phaseCircleTexture.SetPixels32(colors);
            phaseCircleTexture.Apply(false, false);
            return Sprite.Create(
                phaseCircleTexture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        private static bool IsMajorPhase(uint phase)
        {
            return (phase &
                    (0x001U |
                     0x002U |
                     0x004U |
                     0x008U |
                     0x100U |
                     0x200U)) != 0;
        }

        private static Color PhaseAccent(uint phase)
        {
            if ((phase & 0x0F8U) != 0)
                return Hex("#FFB347");
            if ((phase & 0x200U) != 0)
                return Hex("#D18CFF");
            if ((phase & 0x004U) != 0 ||
                (phase & 0x100U) != 0)
                return Hex("#C8FF19");
            return Hex("#52E8E0");
        }
    }

    /// <summary>
    /// Procedural screen-edge impact.  Keeping the center transparent makes
    /// the duel readable and avoids another full-screen texture dependency.
    /// </summary>
    internal sealed class DuelDamageVignetteGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect outer = rectTransform.rect;
            if (outer.width <= 0f || outer.height <= 0f)
                return;

            float insetX = outer.width * 0.17f;
            float insetY = outer.height * 0.20f;
            Rect inner = Rect.MinMaxRect(
                outer.xMin + insetX,
                outer.yMin + insetY,
                outer.xMax - insetX,
                outer.yMax - insetY);
            Color32 edge = color;
            Color transparent = color;
            transparent.a = 0f;
            Color32 clear = transparent;

            AddGradientQuad(
                helper,
                new Vector2(outer.xMin, outer.yMax),
                new Vector2(outer.xMax, outer.yMax),
                new Vector2(inner.xMax, inner.yMax),
                new Vector2(inner.xMin, inner.yMax),
                edge,
                edge,
                clear,
                clear);
            AddGradientQuad(
                helper,
                new Vector2(outer.xMax, outer.yMax),
                new Vector2(outer.xMax, outer.yMin),
                new Vector2(inner.xMax, inner.yMin),
                new Vector2(inner.xMax, inner.yMax),
                edge,
                edge,
                clear,
                clear);
            AddGradientQuad(
                helper,
                new Vector2(outer.xMax, outer.yMin),
                new Vector2(outer.xMin, outer.yMin),
                new Vector2(inner.xMin, inner.yMin),
                new Vector2(inner.xMax, inner.yMin),
                edge,
                edge,
                clear,
                clear);
            AddGradientQuad(
                helper,
                new Vector2(outer.xMin, outer.yMin),
                new Vector2(outer.xMin, outer.yMax),
                new Vector2(inner.xMin, inner.yMax),
                new Vector2(inner.xMin, inner.yMin),
                edge,
                edge,
                clear,
                clear);
        }

        private static void AddGradientQuad(
            VertexHelper helper,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            Color32 firstColor,
            Color32 secondColor,
            Color32 thirdColor,
            Color32 fourthColor)
        {
            int start = helper.currentVertCount;
            helper.AddVert(first, firstColor, Vector2.zero);
            helper.AddVert(second, secondColor, Vector2.right);
            helper.AddVert(third, thirdColor, Vector2.one);
            helper.AddVert(fourth, fourthColor, Vector2.up);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
