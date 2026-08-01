using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.DuelEngine.Protocol;
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
        private int renderedOpponentHandCount = -1;
        private GameObject recentActionsPanel;
        private CanvasGroup recentActionsGroup;
        private readonly List<Text> recentActionLines = new();
        private Coroutine recentActionHideRoutine;
        private GameObject chainIndicator;
        private CanvasGroup chainIndicatorGroup;
        private Text chainIndicatorCount;
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
        }

        private void BuildOpponentHandFan()
        {
            opponentHandFan = CreatePanel(frame, "Mão do Oponente",
                new Vector2(0.365f, 0.865f), new Vector2(0.635f, 0.995f),
                Color.clear);
            opponentHandFan.GetComponent<Image>().raycastTarget = false;
            opponentHandContent = CreateRect(opponentHandFan.transform,
                "Cartas Ocultas do Oponente", Vector2.zero, Vector2.one,
                Vector2.zero);
            opponentHandCount = CreateText(opponentHandFan.transform,
                "0 CARTAS", 10, FontStyle.Bold, Muted,
                new Vector2(0.72f, 0.72f), new Vector2(1f, 0.98f),
                TextAnchor.MiddleRight);
            opponentHandCount.raycastTarget = false;
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
                new Vector2(0.725f, 0.455f), new Vector2(0.795f, 0.565f),
                new Color(0.07f, 0.01f, 0.10f, 0.95f));
            AddOutline(chainIndicator, Red);
            chainIndicatorGroup = chainIndicator.AddComponent<CanvasGroup>();
            chainIndicatorGroup.interactable = false;
            chainIndicatorGroup.blocksRaycasts = false;
            CreateText(chainIndicator.transform, "CORRENTE", 9,
                FontStyle.Bold, Red, new Vector2(0f, 0.63f), Vector2.one,
                TextAnchor.MiddleCenter);
            chainIndicatorCount = CreateText(chainIndicator.transform, "1", 28,
                FontStyle.Bold, Color.white, Vector2.zero,
                new Vector2(1f, 0.70f), TextAnchor.MiddleCenter);
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
            int count = state.Players[1].Hand.Count;
            if (count != renderedOpponentHandCount)
            {
                renderedOpponentHandCount = count;
                RebuildOpponentHandFan(count);
            }
            if (opponentHandCount != null)
                opponentHandCount.text =
                    $"{count} CARTA{(count == 1 ? string.Empty : "S")}";
            if (decisionRibbonKicker != null && core?.CurrentPrompt != null)
                decisionRibbonKicker.text = core.CurrentPrompt.Player == 0
                    ? "SUA PRIORIDADE" : "OPONENTE PENSANDO";
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
                rect.sizeDelta = new Vector2(42f, 61f);
                float center = (visible - 1) * 0.5f;
                rect.anchoredPosition = new Vector2((index - center) * 29f,
                    4f - Mathf.Abs(index - center) * 2f);
                rect.localEulerAngles =
                    new Vector3(0f, 0f, (index - center) * -3.2f);
                card.sprite = cardBackSprite;
                card.preserveAspect = true;
                card.raycastTarget = false;
                AddOutline(card.gameObject,
                    new Color(Gold.r, Gold.g, Gold.b, 0.62f));
            }
            if (count > visible)
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
            if (prompt == null) return;
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
                    UpdateDecisionRibbon(prompt.Forced
                        ? "Uma resposta é obrigatória."
                        : "Você pode responder à corrente.", Red);
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
                    entry = $"Invocação: {SafeCardName(duelEvent.Code)}";
                    accent = Cyan;
                    break;
                case CoreMessage.Chaining:
                    activeChainLinks = Mathf.Max(activeChainLinks + 1,
                        (int)duelEvent.Value);
                    UpdateChainIndicator();
                    entry = $"Corrente {activeChainLinks}: " +
                        SafeCardName(duelEvent.Code);
                    accent = Red;
                    break;
                case CoreMessage.ChainSolving:
                    entry = "Resolvendo corrente " +
                        Mathf.Max(1, (int)duelEvent.Value);
                    accent = Gold;
                    break;
                case CoreMessage.ChainEnd:
                    activeChainLinks = 0;
                    UpdateChainIndicator();
                    entry = "Corrente resolvida";
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
            chainIndicator.SetActive(activeChainLinks > 0);
            if (chainIndicatorCount != null)
                chainIndicatorCount.text =
                    Mathf.Max(1, activeChainLinks).ToString();
            if (activeChainLinks > 0) chainIndicator.transform.SetAsLastSibling();
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
