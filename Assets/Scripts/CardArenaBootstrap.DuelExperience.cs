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
    /// Player-facing duel guidance. It only presents state and choices emitted
    /// by ygopro-core and never creates commands or card rules.
    /// </summary>
    public sealed partial class CardArenaBootstrap
    {
        private sealed class DuelFeedItem
        {
            public string Text;
            public Color Accent;
            public int Turn;
            public uint Phase;
        }

        private const int MaximumDuelHistoryEntries = 320;
        // The compact priority/status ribbon was retired from the duel HUD.
        // Core prompts still drive the actionable buttons, highlights and
        // modals; they must never reopen a passive message over the field.
        private const bool DecisionGuidanceRibbonEnabled = false;
        private readonly List<DuelFeedItem> duelHistory = new();
        private GameObject decisionRibbon;
        private CanvasGroup decisionRibbonGroup;
        private Image decisionRibbonAccent;
        private Text decisionRibbonKicker;
        private Text decisionRibbonText;
        private Outline decisionRibbonOutline;
        private DuelHudSurfaceGraphic decisionRibbonSurface;
        private string renderedDecisionRibbonValue = string.Empty;
        private Color renderedDecisionRibbonColor = Color.clear;
        private bool decisionRibbonVisible;
        private GameObject opponentHandFan;
        private RectTransform opponentHandContent;
        private Text opponentHandCount;
        private DuelHandLayoutAnchor opponentHandLayoutAnchor;
        private int renderedOpponentHandCount = -1;
        private Button duelHistoryButton;
        private GameObject duelHistoryOverlay;
        private Text duelHistorySummary;
        private Text duelHistoryContent;
        private RectTransform duelHistoryContentRect;
        private ScrollRect duelHistoryScroll;
        private int duelHistorySummons;
        private int duelHistoryDestroyed;
        private int duelHistoryChains;
        private long localLifeRecoveredInDuel;
        private long opponentLifeRecoveredInDuel;
        private GameObject chainIndicator;
        private CanvasGroup chainIndicatorGroup;
        private Text chainIndicatorCount;
        private Text chainIndicatorDetails;
        private DuelHudSurfaceGraphic chainIndicatorSurface;
        private int activeChainLinks;
        private float experiencePulse;
        private bool experienceObscured;
        private Color choiceModalAccent = Cyan;

        private void BuildDuelExperience()
        {
            BuildDecisionRibbon();
            BuildOpponentHandFan();
            BuildDuelHistoryPanel();
            BuildChainIndicator();
            if (status != null) status.gameObject.SetActive(false);
        }

        private void BuildDecisionRibbon()
        {
            decisionRibbon = CreatePanel(frame, "Orientação do Duelo",
                new Vector2(0.305f, 0.790f), new Vector2(0.695f, 0.875f),
                Color.clear);
            decisionRibbon.transform.SetAsLastSibling();
            decisionRibbonGroup = decisionRibbon.AddComponent<CanvasGroup>();
            decisionRibbonGroup.interactable = false;
            decisionRibbonGroup.blocksRaycasts = false;
            decisionRibbonSurface = AttachDuelSurface(
                decisionRibbon,
                "Superfície da Orientação",
                Cyan,
                true,
                0.90f,
                false,
                12f);
            decisionRibbonOutline = decisionRibbon.GetComponent<Outline>();
            decisionRibbonAccent = CreateImage(decisionRibbon.transform,
                "Acento de Prioridade", Vector2.zero,
                new Vector2(0.018f, 1f), Cyan);
            decisionRibbonAccent.raycastTarget = false;
            decisionRibbonKicker = CreateText(decisionRibbon.transform,
                "SUA PRIORIDADE", 11, FontStyle.Bold, Cyan,
                new Vector2(0.055f, 0.60f), new Vector2(0.95f, 0.92f),
                TextAnchor.MiddleCenter);
            decisionRibbonText = CreateText(decisionRibbon.transform,
                "ESCOLHA UMA AÇÃO VÁLIDA", 16, FontStyle.Bold, Color.white,
                new Vector2(0.055f, 0.10f), new Vector2(0.95f, 0.66f),
                TextAnchor.MiddleCenter);
            decisionRibbonKicker.raycastTarget = false;
            decisionRibbonText.raycastTarget = false;
            HideDecisionRibbon();
        }

        private void BuildOpponentHandFan()
        {
            opponentHandFan = FindObject(
                frame,
                "POSICAO DA MAO DO OPONENTE");
            bool authoredHandFan = opponentHandFan != null;
            if (opponentHandFan == null)
            {
                opponentHandFan = CreatePanel(
                    frame,
                    "POSICAO DA MAO DO OPONENTE",
                    new Vector2(0.365f, 0.865f),
                    new Vector2(0.635f, 0.995f),
                    Color.clear);
            }
            if (!authoredHandFan)
            {
                Image opponentHandBackground =
                    opponentHandFan.GetComponent<Image>() ??
                    opponentHandFan.AddComponent<Image>();
                opponentHandBackground.color = Color.clear;
                opponentHandBackground.raycastTarget = false;
            }
            opponentHandLayoutAnchor = opponentHandFan
                .GetComponent<DuelHandLayoutAnchor>();
            if (opponentHandLayoutAnchor == null)
            {
                opponentHandLayoutAnchor = opponentHandFan
                    .AddComponent<DuelHandLayoutAnchor>();
                opponentHandLayoutAnchor.ConfigureOwner(
                    DuelHandLayoutAnchor.HandOwner.Opponent);
            }
            opponentHandContent = FindRect(
                opponentHandFan.transform,
                "Cartas Ocultas do Oponente");
            if (opponentHandContent == null)
            {
                opponentHandContent = CreateRect(
                    opponentHandFan.transform,
                    "Cartas Ocultas do Oponente",
                    Vector2.zero,
                    Vector2.one,
                    Vector2.zero);
            }
            opponentHandCount = FindTransform(
                    opponentHandFan.transform,
                    "QUANTIDADE DE CARTAS")
                ?.GetComponent<Text>();
            if (preserveAuthoredDuelInterface)
            {
                if (opponentHandCount != null)
                    opponentHandCount.gameObject.SetActive(false);
                opponentHandCount = null;
                DisableLegacyOpponentHandPreview();
            }
            else if (opponentHandCount == null)
            {
                DuelTestPerspectiveController.Instance
                    ?.SetHiddenHandPreviewsEnabled(true);
                opponentHandCount = CreateText(
                    opponentHandFan.transform,
                    "0 CARTAS",
                    10,
                    FontStyle.Bold,
                    Muted,
                    new Vector2(0.72f, 0.72f),
                    new Vector2(1f, 0.98f),
                    TextAnchor.MiddleRight);
                opponentHandCount.gameObject.name = "QUANTIDADE DE CARTAS";
            }
            if (opponentHandCount != null)
                opponentHandCount.raycastTarget = false;
        }

        private void DisableLegacyOpponentHandPreview()
        {
            DuelTestPerspectiveController perspective =
                DuelTestPerspectiveController.Instance;
            if (perspective != null)
                perspective.SetHiddenHandPreviewsEnabled(false);

            foreach (Transform item in
                     FindObjectsByType<MasterDuelArena3D>(
                             FindObjectsInactive.Include)
                         .Where(arena =>
                             arena != null &&
                             arena.gameObject.scene == gameObject.scene)
                         .SelectMany(arena =>
                             arena.GetComponentsInChildren<Transform>(true)))
            {
                if (item != null &&
                    string.Equals(
                        item.name,
                        "OpponentHandPreview",
                        StringComparison.Ordinal))
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        private void BuildDuelHistoryPanel()
        {
            duelHistoryButton = CreateButton(
                frame,
                "Botão Histórico",
                "HISTÓRICO",
                new Vector2(0.900f, 0.088f),
                new Vector2(0.985f, 0.148f),
                Cyan,
                OpenDuelHistory);
            duelHistoryButton.gameObject.transform.SetAsLastSibling();
            AttachDuelSurface(
                duelHistoryButton.gameObject,
                "Superfície do Histórico",
                Cyan,
                true,
                0.92f,
                false,
                8f);

            duelHistoryOverlay = CreatePanel(
                frame,
                "Janela do Histórico do Duelo",
                Vector2.zero,
                Vector2.one,
                new Color(0.004f, 0.010f, 0.022f, 0.88f));
            duelHistoryOverlay.GetComponent<Image>().raycastTarget = true;

            GameObject window = CreatePanel(
                duelHistoryOverlay.transform,
                "Central do Histórico",
                new Vector2(0.17f, 0.105f),
                new Vector2(0.83f, 0.895f),
                new Color(0.010f, 0.035f, 0.055f, 0.98f));
            AttachDuelSurface(
                window,
                "Superfície da Central do Histórico",
                Gold,
                true,
                0.97f,
                false,
                12f);
            CreateText(
                window.transform,
                "HISTÓRICO DO DUELO",
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.045f, 0.905f),
                new Vector2(0.66f, 0.975f),
                TextAnchor.MiddleLeft).raycastTarget = false;
            Text caption = CreateText(
                window.transform,
                "EVENTOS E ESTATÍSTICAS DA PARTIDA",
                11,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.56f, 0.915f),
                new Vector2(0.955f, 0.965f),
                TextAnchor.MiddleRight);
            caption.raycastTarget = false;
            CreateImage(
                window.transform,
                "Linha do Histórico",
                new Vector2(0.04f, 0.895f),
                new Vector2(0.96f, 0.899f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f))
                .raycastTarget = false;

            duelHistorySummary = CreateText(
                window.transform,
                string.Empty,
                12,
                FontStyle.Bold,
                Muted,
                new Vector2(0.05f, 0.79f),
                new Vector2(0.95f, 0.885f),
                TextAnchor.MiddleLeft);
            duelHistorySummary.supportRichText = true;
            duelHistorySummary.raycastTarget = false;

            GameObject scrollRoot = CreatePanel(
                window.transform,
                "Rolagem do Histórico",
                new Vector2(0.05f, 0.14f),
                new Vector2(0.95f, 0.78f),
                new Color(0.002f, 0.016f, 0.028f, 0.94f));
            AddOutline(scrollRoot, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f));
            duelHistoryScroll = scrollRoot.AddComponent<ScrollRect>();
            duelHistoryScroll.horizontal = false;
            duelHistoryScroll.vertical = true;
            duelHistoryScroll.movementType = ScrollRect.MovementType.Elastic;
            duelHistoryScroll.elasticity = 0.085f;
            duelHistoryScroll.inertia = true;
            duelHistoryScroll.decelerationRate = 0.12f;
            duelHistoryScroll.scrollSensitivity = 32f;

            GameObject viewport = CreatePanel(
                scrollRoot.transform,
                "Área Visível",
                new Vector2(0.015f, 0.02f),
                new Vector2(0.985f, 0.98f),
                Color.clear);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            duelHistoryScroll.viewport = viewport.GetComponent<RectTransform>();

            var contentObject = new GameObject(
                "Conteúdo do Histórico",
                typeof(RectTransform));
            contentObject.transform.SetParent(viewport.transform, false);
            duelHistoryContentRect =
                contentObject.GetComponent<RectTransform>();
            duelHistoryContentRect.anchorMin = new Vector2(0f, 1f);
            duelHistoryContentRect.anchorMax = new Vector2(1f, 1f);
            duelHistoryContentRect.pivot = new Vector2(0.5f, 1f);
            duelHistoryContentRect.anchoredPosition = Vector2.zero;
            duelHistoryContentRect.sizeDelta = new Vector2(0f, 400f);
            duelHistoryScroll.content = duelHistoryContentRect;

            duelHistoryContent = CreateText(
                duelHistoryContentRect,
                string.Empty,
                13,
                FontStyle.Normal,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.UpperLeft);
            duelHistoryContent.supportRichText = true;
            duelHistoryContent.raycastTarget = false;
            duelHistoryContent.rectTransform.offsetMin = new Vector2(14f, 8f);
            duelHistoryContent.rectTransform.offsetMax = new Vector2(-14f, -8f);

            Button close = CreateButton(
                window.transform,
                "Fechar Histórico",
                "FECHAR",
                new Vector2(0.38f, 0.035f),
                new Vector2(0.62f, 0.115f),
                Cyan,
                CloseDuelHistory);
            AttachDuelSurface(
                close.gameObject,
                "Superfície de Fechar Histórico",
                Cyan,
                true,
                0.94f,
                false,
                8f);
            duelHistoryOverlay.SetActive(false);
        }

        private void BuildChainIndicator()
        {
            chainIndicator = CreatePanel(frame, "Indicador de Corrente",
                new Vector2(0.745f, 0.405f), new Vector2(0.975f, 0.525f),
                Color.clear);
            chainIndicatorSurface = AttachDuelSurface(
                chainIndicator,
                "Superfície da Corrente",
                OpponentTurnRed,
                false,
                0.91f,
                false,
                12f);
            chainIndicatorGroup = chainIndicator.AddComponent<CanvasGroup>();
            chainIndicatorGroup.interactable = false;
            chainIndicatorGroup.blocksRaycasts = false;
            CreateText(chainIndicator.transform, "CORRENTE", 9,
                FontStyle.Bold, Red, new Vector2(0.02f, 0.63f),
                new Vector2(0.21f, 1f),
                TextAnchor.MiddleCenter);
            chainIndicatorCount = CreateText(chainIndicator.transform, "1", 28,
                FontStyle.Bold, Color.white, new Vector2(0.02f, 0f),
                new Vector2(0.21f, 0.70f), TextAnchor.MiddleCenter);
            chainIndicatorDetails = CreateText(
                chainIndicator.transform,
                string.Empty,
                10,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.23f, 0.08f),
                new Vector2(0.97f, 0.92f),
                TextAnchor.MiddleLeft);
            chainIndicatorDetails.supportRichText = true;
            chainIndicator.SetActive(false);
        }

        private void DisposeDuelExperience()
        {
            duelHistory.Clear();
            duelHistorySummons = 0;
            duelHistoryDestroyed = 0;
            duelHistoryChains = 0;
            localLifeRecoveredInDuel = 0;
            opponentLifeRecoveredInDuel = 0;
            duelHistoryButton = null;
            duelHistoryOverlay = null;
            duelHistorySummary = null;
            duelHistoryContent = null;
            duelHistoryContentRect = null;
            duelHistoryScroll = null;
        }

        private void OpenDuelHistory()
        {
            if (duelHistoryOverlay == null)
                return;
            OpenExclusiveDuelUiSurface(DuelUiSurfaceKind.DuelHistory);
            RefreshDuelHistoryWindow();
            duelHistoryOverlay.SetActive(true);
            duelHistoryOverlay.transform.SetAsLastSibling();
        }

        private void CloseDuelHistory()
        {
            if (duelHistoryOverlay != null)
                duelHistoryOverlay.SetActive(false);
            MarkDuelUiSurfaceClosed(DuelUiSurfaceKind.DuelHistory);
            RestoreSuspendedPromptIfCurrent();
        }

        private void RefreshDuelHistoryWindow()
        {
            if (duelHistorySummary == null || duelHistoryContent == null ||
                duelHistoryContentRect == null)
            {
                return;
            }

            int localLp = state?.Players[0].LifePoints ?? 0;
            int opponentLp = state?.Players[1].LifePoints ?? 0;
            duelHistorySummary.text =
                $"<color=#52E8E0>VOCÊ</color>  LP {localLp:N0}  ·  " +
                $"dano sofrido {localDamageReceivedInDuel:N0}  ·  " +
                $"vida ganha {localLifeRecoveredInDuel:N0}\n" +
                $"<color=#FF536B>OPONENTE</color>  LP {opponentLp:N0}  ·  " +
                $"dano sofrido {localDamageDealtInDuel:N0}  ·  " +
                $"vida ganha {opponentLifeRecoveredInDuel:N0}  ·  " +
                $"invocações {duelHistorySummons}  ·  " +
                $"destruídas {duelHistoryDestroyed}  ·  " +
                $"correntes {duelHistoryChains}";

            duelHistoryContent.text = duelHistory.Count == 0
                ? "<color=#87A8B7>Nenhum evento registrado ainda.</color>"
                : string.Join(
                    "\n\n",
                    duelHistory.Select(item =>
                        $"<color=#{ColorUtility.ToHtmlStringRGB(item.Accent)}>" +
                        $"T{Mathf.Max(1, item.Turn)} · " +
                        $"{CoreMessageDecoder.PhaseName(item.Phase).ToUpperInvariant()}" +
                        $"</color>\n{item.Text}"));

            Canvas.ForceUpdateCanvases();
            float viewportHeight = duelHistoryScroll?.viewport != null
                ? duelHistoryScroll.viewport.rect.height
                : 380f;
            float estimatedHeight = Mathf.Max(
                viewportHeight,
                42f + duelHistory.Count * 58f);
            duelHistoryContentRect.sizeDelta =
                new Vector2(0f, estimatedHeight);
            duelHistoryContentRect.anchoredPosition = Vector2.zero;
            if (duelHistoryScroll != null)
                duelHistoryScroll.verticalNormalizedPosition = 1f;
        }

        private void RefreshDuelExperienceState()
        {
            if (state == null) return;
            UpdateChainIndicator();
            int count = state.Players[1].Hand.Count;
            if (count != renderedOpponentHandCount)
            {
                renderedOpponentHandCount = count;
                RebuildOpponentHandFan(count);
            }
            if (!preserveAuthoredDuelInterface)
            {
                DuelTestPerspectiveController.Instance
                    ?.SetHiddenHandCardCount(
                        DuelPlayerSide.PlayerTwo,
                        count);
            }
            if (opponentHandCount != null)
                opponentHandCount.text =
                    $"{count} CARTA{(count == 1 ? string.Empty : "S")}";
            if (decisionRibbonKicker != null &&
                core?.CurrentPrompt?.Player == 0)
            {
                decisionRibbonKicker.text = "SUA PRIORIDADE";
            }
            else if (core?.CurrentPrompt?.Player == 1)
            {
                HideDecisionRibbon();
            }
        }

        private void RebuildOpponentHandFan(int count)
        {
            if (opponentHandContent == null) return;
            ClearChildren(opponentHandContent);
            int visible = Mathf.Min(10, count);
            for (int index = 0; index < visible; index++)
            {
                Image card = CreateImage(opponentHandContent,
                    $"Carta Oculta {index + 1}", new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Color.white);
                RectTransform rect = card.rectTransform;
                rect.sizeDelta = opponentHandLayoutAnchor != null
                    ? opponentHandLayoutAnchor.CardSize
                    : new Vector2(42f, 61f);
                Vector2 position = opponentHandLayoutAnchor != null
                    ? opponentHandLayoutAnchor.PositionFor(index, visible)
                    : new Vector2(
                        (index - (visible - 1) * 0.5f) * 29f,
                        4f - Mathf.Abs(
                            index - (visible - 1) * 0.5f) * 2f);
                float angle = opponentHandLayoutAnchor != null
                    ? opponentHandLayoutAnchor.AngleFor(index, visible)
                    : (index - (visible - 1) * 0.5f) * -3.2f;
                rect.anchoredPosition = position;
                rect.localEulerAngles = new Vector3(0f, 0f, angle);
                card.sprite = cardBackSprite;
                card.preserveAspect = true;
                card.raycastTarget = false;
                if (!preserveAuthoredDuelInterface)
                {
                    AddOutline(card.gameObject,
                        new Color(Gold.r, Gold.g, Gold.b, 0.62f));
                }
            }
            if (count > visible && !preserveAuthoredDuelInterface)
            {
                Text remainder = CreateText(opponentHandContent,
                    $"+{count - visible}", 11, FontStyle.Bold, Gold,
                    new Vector2(0.84f, 0.16f), new Vector2(0.99f, 0.52f),
                    TextAnchor.MiddleCenter);
                remainder.raycastTarget = false;
            }
        }

        private void UpdateDuelExperienceForPrompt(DuelPrompt prompt)
        {
            if (prompt == null || prompt.Player != 0)
            {
                HideDecisionRibbon();
                return;
            }
            switch (prompt.Message)
            {
                case CoreMessage.SelectIdleCommand:
                    UpdateDecisionRibbon(
                        "Escolha uma carta iluminada para jogar.", Cyan);
                    break;
                case CoreMessage.SelectBattleCommand:
                    UpdateDecisionRibbon(
                        "Selecione o monstro que vai atacar.", Gold);
                    break;
                case CoreMessage.SelectPlace:
                case CoreMessage.SelectDisableField:
                    UpdateDecisionRibbon(
                        "Escolha uma zona iluminada no campo.", Cyan);
                    break;
                case CoreMessage.SelectChain:
                    string chainMessage =
                        DuelPromptPresentationRules
                            .ShouldAutoPassEmptyChain(prompt)
                            ? "Nenhuma resposta legal disponível."
                            : prompt.Forced
                                ? "Uma resposta é obrigatória."
                                : "Você pode responder à corrente.";
                    UpdateDecisionRibbon(chainMessage, Red);
                    break;
                case CoreMessage.SelectEffectYesNo:
                    DuelChoice effectChoice =
                        DuelPromptPresentationRules
                            .ActionableResponseChoices(prompt)
                            .FirstOrDefault();
                    UpdateDecisionRibbon(
                        effectChoice == null
                            ? "Escolha se deseja ativar este efeito."
                            : ChoiceLabel(effectChoice)
                                .Replace("\n", " — "),
                        EffectGlow);
                    break;
                case CoreMessage.SelectPosition:
                    UpdateDecisionRibbon(
                        "Escolha a posição de batalha.", Cyan);
                    break;
                default:
                    UpdateDecisionRibbon(string.IsNullOrWhiteSpace(prompt.Title)
                        ? "Conclua a decisão atual." : prompt.Title, Gold);
                    break;
            }
        }

        private void UpdateDecisionRibbon(string value, Color color)
        {
            if (decisionRibbon == null || decisionRibbonText == null) return;
            if (!DecisionGuidanceRibbonEnabled ||
                !DuelActivationPreferences.GuidanceMessagesEnabled)
            {
                HideDecisionRibbon();
                return;
            }
            string normalized = value ?? string.Empty;
            bool contentChanged =
                !string.Equals(
                    renderedDecisionRibbonValue,
                    normalized,
                    StringComparison.Ordinal) ||
                renderedDecisionRibbonColor != color;
            if (!decisionRibbon.activeSelf)
                decisionRibbon.SetActive(true);
            decisionRibbonVisible = true;
            if (!contentChanged)
                return;

            renderedDecisionRibbonValue = normalized;
            renderedDecisionRibbonColor = color;
            decisionRibbonText.text = normalized;
            if (decisionRibbonAccent != null)
                decisionRibbonAccent.color = color;
            if (decisionRibbonKicker != null)
                decisionRibbonKicker.color = color;
            decisionRibbonSurface?.SetStyle(
                color,
                true,
                0.90f,
                false,
                12f);
            if (decisionRibbonOutline != null)
                decisionRibbonOutline.effectColor = color;
        }

        private void HideDecisionRibbon()
        {
            if (!decisionRibbonVisible &&
                (decisionRibbon == null || !decisionRibbon.activeSelf))
            {
                return;
            }
            decisionRibbonVisible = false;
            if (decisionRibbonGroup != null)
                decisionRibbonGroup.alpha = 0f;
            if (decisionRibbon != null)
                decisionRibbon.SetActive(false);
        }

        private void UpdateDuelExperienceAnimation()
        {
            if (decisionRibbon == null) return;
            experiencePulse += Time.unscaledDeltaTime;
            decisionRibbonGroup.alpha = experienceObscured ? 0.18f : 0.96f;
            if (chainIndicator != null && chainIndicator.activeSelf)
            {
                chainIndicatorGroup.alpha =
                    0.90f + Mathf.Sin(experiencePulse * 4f) * 0.10f;
                chainIndicator.transform.localScale = Vector3.one *
                    (1f + Mathf.Sin(experiencePulse * 4f) * 0.008f);
            }
        }

        private void HandleDuelExperienceEvent(DuelEvent duelEvent)
        {
            string entry = null;
            Color accent = Muted;
            switch (duelEvent.Message)
            {
                case CoreMessage.NewTurn:
                    entry = duelEvent.Player == 0
                        ? "Seu turno começou" : "Turno do oponente";
                    accent = duelEvent.Player == 0
                        ? LocalTurnBlue
                        : OpponentTurnRed;
                    break;
                case CoreMessage.NewPhase:
                    entry = CoreMessageDecoder.PhaseName(duelEvent.Value);
                    accent = PhaseAccent(duelEvent.Value);
                    break;
                case CoreMessage.Draw:
                    entry = duelEvent.Player == 0
                        ? "Você comprou uma carta"
                        : "Oponente comprou uma carta";
                    break;
                case CoreMessage.Summoning:
                case CoreMessage.SpecialSummoning:
                case CoreMessage.FlipSummoning:
                    entry = $"Tentativa de invocação: {SafeCardName(duelEvent.Code)}";
                    accent = Cyan;
                    break;
                case CoreMessage.Summoned:
                case CoreMessage.SpecialSummoned:
                case CoreMessage.FlipSummoned:
                    duelHistorySummons++;
                    entry = state?.LastSummon != null
                        ? $"Invocação confirmada: {SafeCardName(state.LastSummon.CardCode)}"
                        : "Invocação confirmada";
                    accent = Lime;
                    break;
                case CoreMessage.Chaining:
                    duelHistoryChains++;
                    activeChainLinks = Mathf.Max(activeChainLinks + 1,
                        (int)duelEvent.Value);
                    UpdateChainIndicator();
                    entry = $"Corrente {activeChainLinks}: " +
                        ChainEffectName(duelEvent);
                    accent = Red;
                    break;
                case CoreMessage.Chained:
                    UpdateChainIndicator();
                    entry = $"CL{Mathf.Max(1, (int)duelEvent.Value)} encadeado";
                    accent = Red;
                    break;
                case CoreMessage.ChainSolving:
                    UpdateChainIndicator();
                    entry = "Resolvendo corrente " +
                        Mathf.Max(1, (int)duelEvent.Value);
                    accent = Gold;
                    break;
                case CoreMessage.ChainSolved:
                    UpdateChainIndicator();
                    entry = $"CL{Mathf.Max(1, (int)duelEvent.Value)} resolvido";
                    accent = Lime;
                    break;
                case CoreMessage.ChainNegated:
                    UpdateChainIndicator();
                    entry = $"Ativação de CL{Mathf.Max(1, (int)duelEvent.Value)} negada";
                    accent = Red;
                    break;
                case CoreMessage.ChainDisabled:
                    UpdateChainIndicator();
                    entry = $"Efeito de CL{Mathf.Max(1, (int)duelEvent.Value)} desabilitado";
                    accent = Muted;
                    break;
                case CoreMessage.ChainEnd:
                    UpdateChainIndicator();
                    entry = "Corrente concluída · sincronizando campo";
                    accent = Lime;
                    break;
                case CoreMessage.Attack:
                    entry = duelEvent.DirectAttack
                        ? "Ataque direto declarado" : "Ataque declarado";
                    accent = Gold;
                    break;
                case CoreMessage.Set:
                    entry = $"Carta baixada: {SafeCardName(duelEvent.Code)}";
                    accent = Muted;
                    break;
                case CoreMessage.Move:
                    bool destroyed = (duelEvent.Value & 0x1U) != 0U;
                    if (destroyed)
                    {
                        duelHistoryDestroyed++;
                        entry = $"Carta destruída: {SafeCardName(duelEvent.Code)}";
                        accent = Red;
                    }
                    else if (duelEvent.Current != null &&
                             (duelEvent.Current.Location &
                              DuelLocation.Graveyard) != 0)
                    {
                        entry = $"Enviada ao Cemitério: {SafeCardName(duelEvent.Code)}";
                    }
                    else if (duelEvent.Current != null &&
                             (duelEvent.Current.Location &
                              DuelLocation.Banished) != 0)
                    {
                        entry = $"Banida: {SafeCardName(duelEvent.Code)}";
                        accent = Gold;
                    }
                    break;
                case CoreMessage.Damage:
                    entry = duelEvent.Player == 0
                        ? $"Você sofreu {duelEvent.Value:N0} de dano"
                        : $"Oponente sofreu {duelEvent.Value:N0} de dano";
                    accent = Red;
                    break;
                case CoreMessage.Recover:
                    if (duelEvent.Player == 0)
                        localLifeRecoveredInDuel += duelEvent.Value;
                    else
                        opponentLifeRecoveredInDuel += duelEvent.Value;
                    entry = duelEvent.Player == 0
                        ? $"Você recuperou {duelEvent.Value:N0} LP"
                        : $"Oponente recuperou {duelEvent.Value:N0} LP";
                    accent = Lime;
                    break;
                case CoreMessage.PayLifePointCost:
                    entry = duelEvent.Player == 0
                        ? $"Você pagou {duelEvent.Value:N0} LP"
                        : $"Oponente pagou {duelEvent.Value:N0} LP";
                    accent = Gold;
                    break;
                case CoreMessage.Win:
                    entry = duelEvent.Player == 0 ? "Vitória!" : "Derrota";
                    accent = duelEvent.Player == 0 ? Lime : Red;
                    break;
            }
            if (!string.IsNullOrWhiteSpace(entry))
                PushDuelFeed(entry, accent);
        }

        private string SafeCardName(uint code)
        {
            return code == 0 ? "carta" : CardName(code);
        }

        private string ChainEffectName(DuelEvent duelEvent)
        {
            string cardName = SafeCardName(duelEvent.Code);
            return DuelEffectDescriptionResolver.TryResolve(
                duelEvent.DescriptionId,
                database,
                out string effectText,
                out _,
                out _)
                ? cardName + " — " + effectText
                : cardName;
        }

        private void PushDuelFeed(string value, Color accent)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            while (duelHistory.Count >= MaximumDuelHistoryEntries)
                duelHistory.RemoveAt(0);
            duelHistory.Add(new DuelFeedItem
            {
                Text = value.Trim(),
                Accent = accent,
                Turn = Mathf.Max(1, state?.TurnNumber ?? 1),
                Phase = state?.Phase ?? 0U
            });
            if (duelHistoryOverlay?.activeInHierarchy == true)
                RefreshDuelHistoryWindow();
        }

        private void UpdateChainIndicator()
        {
            if (chainIndicator == null) return;
            if (state != null)
                activeChainLinks = state.ChainLinks.Count;
            chainIndicator.SetActive(
                DuelActivationPreferences.ChainPanelEnabled &&
                activeChainLinks > 0);
            if (chainIndicatorCount != null)
                chainIndicatorCount.text =
                    Mathf.Max(1, activeChainLinks).ToString();
            if (chainIndicatorDetails != null)
            {
                chainIndicatorDetails.text = state == null
                    ? string.Empty
                    : string.Join(
                        "\n",
                        state.ChainLinks
                            .OrderBy(link => link.ChainIndex)
                            .Select(ChainLinkPresentation));
            }
            if (activeChainLinks > 0) chainIndicator.transform.SetAsLastSibling();
            if (activeChainLinks > 0)
            {
                Color accent = state?.ChainLinks
                    .OrderBy(link => link.ChainIndex)
                    .LastOrDefault()?.Status switch
                {
                    DuelChainLinkStatus.Solving => Gold,
                    DuelChainLinkStatus.Solved => Lime,
                    DuelChainLinkStatus.Negated => OpponentTurnRed,
                    DuelChainLinkStatus.Disabled => Muted,
                    _ => OpponentTurnRed
                };
                chainIndicatorSurface?.SetStyle(
                    accent,
                    false,
                    0.91f,
                    false,
                    12f);
            }
        }

        private string ChainLinkPresentation(DuelChainLinkSnapshot link)
        {
            if (link == null)
                return string.Empty;
            int effectNumber = 0;
            DuelEffectDescriptionResolver.TryResolve(
                link.DescriptionId,
                database,
                out _,
                out effectNumber,
                out _);
            string effect = effectNumber > 0
                ? $" · efeito {effectNumber}"
                : string.Empty;
            return $"<color={ChainStatusColor(link.Status)}>" +
                   $"CL{link.ChainIndex} · {SafeCardName(link.CardCode)}" +
                   $"{effect} · {ChainStatusLabel(link.Status)}</color>";
        }

        private static string ChainStatusLabel(DuelChainLinkStatus status)
        {
            return status switch
            {
                DuelChainLinkStatus.Chaining => "ATIVANDO",
                DuelChainLinkStatus.Chained => "ENCADEADO",
                DuelChainLinkStatus.Solving => "RESOLVENDO",
                DuelChainLinkStatus.Solved => "RESOLVIDO",
                DuelChainLinkStatus.Negated => "ATIVAÇÃO NEGADA",
                DuelChainLinkStatus.Disabled => "EFEITO DESABILITADO",
                _ => status.ToString().ToUpperInvariant()
            };
        }

        private static string ChainStatusColor(DuelChainLinkStatus status)
        {
            return status switch
            {
                DuelChainLinkStatus.Solving => "#FFD166",
                DuelChainLinkStatus.Solved => "#B8FF3D",
                DuelChainLinkStatus.Negated => "#FF536B",
                DuelChainLinkStatus.Disabled => "#9CAFC2",
                _ => "#FFFFFF"
            };
        }

        private void UpdateCardActionPresentation()
        {
            if (actionPanel == null || !actionPanel.activeSelf) return;
            foreach (GameObject action in
                     new[] { activateAction, summonAction, setAction })
            {
                if (action == null) continue;
                Image image = action.GetComponent<Image>();
                if (phaseCircleSprite != null)
                {
                    image.sprite = phaseCircleSprite;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                }
                Color accent = action == activateAction ? EffectGlow
                    : action == summonAction ? SummonBlue : Gold;
                image.color = new Color(0.012f, 0.07f, 0.10f, 0.98f);
                AddOutline(action, accent);
                Text label = action.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.color = Color.white;
                    label.fontSize = 12;
                }
                Button button = action.GetComponent<Button>();
                if (button == null) continue;
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = accent;
                colors.pressedColor = Lime;
                colors.fadeDuration = 0.08f;
                button.colors = colors;
            }
        }

        private void ApplyChoicePresentationProfile(DuelPrompt prompt)
        {
            choiceModalAccent = ChoiceAccent(prompt);
            if (choiceModal != null)
            {
                Outline outline = choiceModal.GetComponent<Outline>();
                if (outline != null) outline.effectColor = choiceModalAccent;
            }
            if (choiceConfirm != null)
            {
                Outline outline = choiceConfirm.GetComponent<Outline>();
                if (outline != null) outline.effectColor = choiceModalAccent;
            }
        }

        private static Color ChoiceAccent(DuelPrompt prompt)
        {
            if (prompt == null) return Cyan;
            switch (prompt.Message)
            {
                case CoreMessage.SelectChain: return EffectGlow;
                case CoreMessage.SelectYesNo:
                case CoreMessage.SelectEffectYesNo: return EffectGlow;
                case CoreMessage.SelectPosition: return Cyan;
                case CoreMessage.SelectTribute: return Gold;
                case CoreMessage.SelectSum:
                    return new Color(0.70f, 0.37f, 1f, 1f);
                default: return Cyan;
            }
        }

        private static string ChoicePresentationHeading(DuelPrompt prompt)
        {
            if (prompt == null) return "Escolha uma ação";
            switch (prompt.Message)
            {
                case CoreMessage.SelectChain: return "Responder à corrente?";
                case CoreMessage.SelectYesNo:
                case CoreMessage.SelectEffectYesNo: return "Ativar este efeito?";
                case CoreMessage.SelectPosition:
                    return "Escolha a posição de batalha";
                case CoreMessage.SelectTribute: return "Escolha os tributos";
                case CoreMessage.SelectSum: return "Escolha os materiais";
                case CoreMessage.SelectCard:
                case CoreMessage.SelectUnselectCard: return "Escolha as cartas";
                default:
                    return string.IsNullOrWhiteSpace(prompt.Title)
                        ? "Escolha uma ação" : prompt.Title;
            }
        }

        private void SetDuelExperienceObscured(bool obscured)
        {
            experienceObscured = obscured;
            if (duelHistoryButton != null)
                duelHistoryButton.gameObject.SetActive(!obscured);
        }

        private bool PrepareDuelExperienceCapture(string captureState)
        {
            if (!string.Equals(captureState, "experience",
                    StringComparison.OrdinalIgnoreCase)) return false;
            duelHistory.Clear();
            RebuildOpponentHandFan(
                Mathf.Max(5, state?.Players[1].Hand.Count ?? 5));
            PushDuelFeed("Fase Principal 1", Cyan);
            PushDuelFeed("Oponente comprou uma carta", Gold);
            PushDuelFeed("Seu turno começou", Lime);
            activeChainLinks = 2;
            UpdateChainIndicator();
            UpdateDecisionRibbon(
                "Selecione uma carta iluminada para jogar.", Cyan);
            return true;
        }
    }
}
