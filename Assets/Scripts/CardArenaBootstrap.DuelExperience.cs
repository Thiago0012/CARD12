using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        }

        private readonly Queue<DuelFeedItem> duelFeed = new();
        private GameObject decisionRibbon;
        private CanvasGroup decisionRibbonGroup;
        private Image decisionRibbonAccent;
        private Text decisionRibbonKicker;
        private Text decisionRibbonText;
        private Outline decisionRibbonOutline;
        private GameObject opponentHandFan;
        private RectTransform opponentHandContent;
        private Text opponentHandCount;
        private DuelHandLayoutAnchor opponentHandLayoutAnchor;
        private int renderedOpponentHandCount = -1;
        private GameObject recentActionsPanel;
        private CanvasGroup recentActionsGroup;
        private readonly List<Text> recentActionLines = new();
        private Coroutine recentActionHideRoutine;
        private GameObject chainIndicator;
        private CanvasGroup chainIndicatorGroup;
        private Text chainIndicatorCount;
        private Text chainIndicatorDetails;
        private int activeChainLinks;
        private float experiencePulse;
        private bool experienceObscured;
        private Color choiceModalAccent = Cyan;

        private void BuildDuelExperience()
        {
            BuildDecisionRibbon();
            BuildOpponentHandFan();
            BuildRecentActionsPanel();
            BuildChainIndicator();
            if (status != null) status.gameObject.SetActive(false);
        }

        private void BuildDecisionRibbon()
        {
            decisionRibbon = CreatePanel(frame, "Orientação do Duelo",
                new Vector2(0.305f, 0.790f), new Vector2(0.695f, 0.875f),
                new Color(0.004f, 0.025f, 0.045f, 0.96f));
            decisionRibbon.transform.SetAsLastSibling();
            decisionRibbonGroup = decisionRibbon.AddComponent<CanvasGroup>();
            decisionRibbonGroup.interactable = false;
            decisionRibbonGroup.blocksRaycasts = false;
            decisionRibbonOutline = decisionRibbon.AddComponent<Outline>();
            decisionRibbonOutline.effectColor = Cyan;
            decisionRibbonOutline.effectDistance = new Vector2(2f, -2f);
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
            foreach (Transform item in
                     FindObjectsByType<MasterDuelArena3D>(
                             FindObjectsInactive.Include,
                             FindObjectsSortMode.None)
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

        private void BuildRecentActionsPanel()
        {
            recentActionsPanel = CreatePanel(frame, "Notificação de Ação",
                new Vector2(0.735f, 0.555f), new Vector2(0.985f, 0.595f),
                Color.clear);
            Image background = recentActionsPanel.GetComponent<Image>();
            if (background != null) background.raycastTarget = false;
            recentActionsGroup = recentActionsPanel.AddComponent<CanvasGroup>();
            recentActionsGroup.interactable = false;
            recentActionsGroup.blocksRaycasts = false;
            recentActionsGroup.alpha = 0f;

            Text line = CreateText(recentActionsPanel.transform, string.Empty, 11,
                FontStyle.Bold, Muted, Vector2.zero, Vector2.one,
                TextAnchor.MiddleRight);
            line.raycastTarget = false;
            recentActionLines.Add(line);
            recentActionsPanel.SetActive(false);
        }

        private void BuildChainIndicator()
        {
            chainIndicator = CreatePanel(frame, "Indicador de Corrente",
                new Vector2(0.700f, 0.390f), new Vector2(0.985f, 0.555f),
                new Color(0.07f, 0.01f, 0.10f, 0.95f));
            AddOutline(chainIndicator, Red);
            chainIndicatorGroup = chainIndicator.AddComponent<CanvasGroup>();
            chainIndicatorGroup.interactable = false;
            chainIndicatorGroup.blocksRaycasts = false;
            CreateText(chainIndicator.transform, "CORRENTE", 9,
                FontStyle.Bold, Red, new Vector2(0.02f, 0.63f),
                new Vector2(0.24f, 1f),
                TextAnchor.MiddleCenter);
            chainIndicatorCount = CreateText(chainIndicator.transform, "1", 28,
                FontStyle.Bold, Color.white, new Vector2(0.02f, 0f),
                new Vector2(0.24f, 0.70f), TextAnchor.MiddleCenter);
            chainIndicatorDetails = CreateText(
                chainIndicator.transform,
                string.Empty,
                10,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.27f, 0.08f),
                new Vector2(0.97f, 0.92f),
                TextAnchor.MiddleLeft);
            chainIndicatorDetails.supportRichText = true;
            chainIndicator.SetActive(false);
        }

        private void DisposeDuelExperience()
        {
            if (recentActionHideRoutine != null)
                StopCoroutine(recentActionHideRoutine);
            recentActionHideRoutine = null;
            duelFeed.Clear();
            recentActionLines.Clear();
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
            decisionRibbon.SetActive(true);
            decisionRibbonText.text = value ?? string.Empty;
            decisionRibbonAccent.color = color;
            decisionRibbonKicker.color = color;
            if (decisionRibbonOutline != null)
                decisionRibbonOutline.effectColor = color;
        }

        private void HideDecisionRibbon()
        {
            if (decisionRibbonGroup != null)
                decisionRibbonGroup.alpha = 0f;
            if (decisionRibbon != null)
                decisionRibbon.SetActive(false);
        }

        private void UpdateDuelExperienceAnimation()
        {
            if (decisionRibbon == null) return;
            experiencePulse += Time.unscaledDeltaTime;
            decisionRibbonGroup.alpha = experienceObscured ? 0.18f
                : 0.90f + Mathf.Sin(experiencePulse * 3.2f) * 0.08f;
            if (chainIndicator != null && chainIndicator.activeSelf)
            {
                chainIndicatorGroup.alpha =
                    0.82f + Mathf.Sin(experiencePulse * 5f) * 0.18f;
                chainIndicator.transform.localScale = Vector3.one *
                    (1f + Mathf.Sin(experiencePulse * 5f) * 0.035f);
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
                    accent = duelEvent.Player == 0 ? Cyan : Gold;
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
                    entry = state?.LastSummon != null
                        ? $"Invocação confirmada: {SafeCardName(state.LastSummon.CardCode)}"
                        : "Invocação confirmada";
                    accent = Lime;
                    break;
                case CoreMessage.Chaining:
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
                case CoreMessage.Damage:
                    entry = $"{duelEvent.Value:N0} de dano";
                    accent = Red;
                    break;
                case CoreMessage.Recover:
                    entry = $"{duelEvent.Value:N0} PV recuperados";
                    accent = Lime;
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
            if (recentActionsPanel == null || recentActionLines.Count == 0)
                return;

            duelFeed.Clear();
            duelFeed.Enqueue(new DuelFeedItem { Text = value, Accent = accent });
            recentActionLines[0].text = $"› {value}";
            recentActionLines[0].color = accent;
            recentActionsPanel.SetActive(true);
            recentActionsPanel.transform.SetAsLastSibling();
            if (recentActionHideRoutine != null)
                StopCoroutine(recentActionHideRoutine);
            recentActionHideRoutine = StartCoroutine(
                HideRecentActionAfterDelay());
        }

        private IEnumerator HideRecentActionAfterDelay()
        {
            recentActionsGroup.alpha = experienceObscured ? 0.25f : 1f;
            yield return new WaitForSecondsRealtime(1.65f);
            const float fadeDuration = 0.35f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float visible = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                recentActionsGroup.alpha = visible *
                    (experienceObscured ? 0.25f : 1f);
                yield return null;
            }
            recentActionsGroup.alpha = 0f;
            recentActionsPanel.SetActive(false);
            recentActionHideRoutine = null;
        }

        private void UpdateChainIndicator()
        {
            if (chainIndicator == null) return;
            if (state != null)
                activeChainLinks = state.ChainLinks.Count;
            chainIndicator.SetActive(activeChainLinks > 0);
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
            if (recentActionsPanel == null || recentActionsGroup == null)
                return;
            if (recentActionsPanel.activeSelf)
                recentActionsGroup.alpha = obscured ? 0.25f : 1f;
        }

        private bool PrepareDuelExperienceCapture(string captureState)
        {
            if (!string.Equals(captureState, "experience",
                    StringComparison.OrdinalIgnoreCase)) return false;
            duelFeed.Clear();
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
