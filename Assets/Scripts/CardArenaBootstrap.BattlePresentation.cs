using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private GameObject announcementRoot;
        private CanvasGroup announcementGroup;
        private Image announcementAccent;
        private Text announcementTitle;
        private Text announcementSubtitle;
        private Coroutine announcementRoutine;

        private GameObject battleHud;
        private CanvasGroup battleHudGroup;
        private Text battleHudTitle;
        private Text battleHudSubtitle;
        private LineRenderer battlePresentationLine;
        private Material battlePresentationMaterial;
        private Coroutine battlePresentationRoutine;
        private DuelEvent latestBattleEvent;
        private Transform animatedBattleCard;
        private Vector3 animatedBattleCardPosition;
        private Vector3 animatedBattleCardScale;
        private DuelZone3D animatedBattleTarget;

        private GameObject phaseNavigator;
        private Text phaseNavigatorSubtitle;
        private Sprite phaseCircleSprite;
        private Texture2D phaseCircleTexture;

        private void BuildArenaPresentation()
        {
            BuildAnnouncementBanner();
            BuildBattleHud();
            BuildPhaseNavigator();
            BuildDuelExperience();
            PolishPhaseControl();
        }

        private void DisposeArenaPresentation()
        {
            DisposeDuelExperience();
            announcementQueue.Clear();
            if (announcementRoutine != null)
                StopCoroutine(announcementRoutine);
            ResetTurnFlowPresentation(true);
            if (battlePresentationRoutine != null)
                StopCoroutine(battlePresentationRoutine);
            ResetBattlePresentationVisuals();
            if (battlePresentationMaterial != null)
                Destroy(battlePresentationMaterial);
            if (phaseCircleSprite != null)
                Destroy(phaseCircleSprite);
            if (phaseCircleTexture != null)
                Destroy(phaseCircleTexture);
        }

        private void HandleArenaPresentationEvent(DuelEvent duelEvent)
        {
            if (duelEvent == null)
                return;
            if (!replayingDeferredPresentation)
                HandleDuelExperienceEvent(duelEvent);
            if (DeferBattlePresentationIfNeeded(duelEvent))
                return;

            switch (duelEvent.Message)
            {
                case CoreMessage.NewTurn:
                    QueueAnnouncement(
                        duelEvent.Player == 0
                            ? "SEU TURNO"
                            : "TURNO DO OPONENTE",
                        $"TURNO {Mathf.Max(1, state?.TurnNumber ?? 1)}",
                        duelEvent.Player == 0 ? Cyan : Gold,
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
                        PhaseAccent(duelEvent.Value),
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
                    StartCoroutine(
                        PlayDamagePresentation(
                            duelEvent.Player,
                            duelEvent.Value));
                    break;
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
                new Vector2(0.17f, 0.43f),
                new Vector2(0.83f, 0.585f),
                new Color(0.003f, 0.018f, 0.036f, 0.96f));
            announcementRoot.transform.SetAsLastSibling();
            announcementGroup =
                announcementRoot.AddComponent<CanvasGroup>();
            announcementGroup.alpha = 0f;
            announcementGroup.interactable = false;
            announcementGroup.blocksRaycasts = false;
            Image background = announcementRoot.GetComponent<Image>();
            background.raycastTarget = false;
            AddOutline(announcementRoot, Cyan);

            announcementAccent = CreateImage(
                announcementRoot.transform,
                "Linha de Energia",
                new Vector2(0f, 0f),
                new Vector2(0.012f, 1f),
                Cyan);
            announcementAccent.raycastTarget = false;
            announcementTitle = CreateText(
                announcementRoot.transform,
                "FASE PRINCIPAL 1",
                32,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.06f, 0.38f),
                new Vector2(0.94f, 0.88f),
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
                new Vector2(0.30f, 0.73f),
                new Vector2(0.70f, 0.865f),
                new Color(0.005f, 0.022f, 0.045f, 0.94f));
            battleHud.transform.SetAsLastSibling();
            battleHudGroup = battleHud.AddComponent<CanvasGroup>();
            battleHudGroup.alpha = 0f;
            battleHudGroup.interactable = false;
            battleHudGroup.blocksRaycasts = false;
            battleHud.GetComponent<Image>().raycastTarget = false;
            AddOutline(battleHud, Gold);
            Image topLine = CreateImage(
                battleHud.transform,
                "Linha de Ataque",
                new Vector2(0f, 0.92f),
                new Vector2(1f, 1f),
                Gold);
            topLine.raycastTarget = false;
            battleHudTitle = CreateText(
                battleHud.transform,
                "DECLARAÇÃO DE ATAQUE",
                23,
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
            phaseCircleSprite = CreateCircleSprite();
            phaseNavigator = CreatePanel(
                frame,
                "Navegador Profissional de Fases",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0.008f, 0.018f, 0.78f));
            phaseNavigator.transform.SetAsLastSibling();

            GameObject window = CreatePanel(
                phaseNavigator.transform,
                "Painel de Fases",
                new Vector2(0.105f, 0.13f),
                new Vector2(0.895f, 0.50f),
                new Color(0.006f, 0.035f, 0.06f, 0.985f));
            AddOutline(window, Cyan);
            CreateText(
                window.transform,
                "SELECIONE UMA FASE PARA AVANÇAR",
                23,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.04f, 0.79f),
                new Vector2(0.96f, 0.96f),
                TextAnchor.MiddleCenter);
            phaseNavigatorSubtitle = CreateText(
                window.transform,
                "TURNO 1",
                12,
                FontStyle.Bold,
                Muted,
                new Vector2(0.04f, 0.70f),
                new Vector2(0.96f, 0.80f),
                TextAnchor.MiddleCenter);

            Image rail = CreateImage(
                window.transform,
                "Trilho das Fases",
                new Vector2(0.12f, 0.42f),
                new Vector2(0.88f, 0.435f),
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.38f));
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
                float x = Mathf.Lerp(0.12f, 0.88f, index / 5f);
                GameObject node = CreatePanel(
                    window.transform,
                    $"Fase {labels[index]}",
                    new Vector2(x, 0.43f),
                    new Vector2(x, 0.43f),
                    new Color(0.015f, 0.075f, 0.11f, 1f));
                RectTransform rect =
                    node.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(104f, 104f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                Image image = node.GetComponent<Image>();
                image.sprite = phaseCircleSprite;
                image.preserveAspect = true;
                AddOutline(node, new Color(Cyan.r, Cyan.g, Cyan.b, 0.32f));
                Button button = node.AddComponent<Button>();
                button.targetGraphic = image;
                int captured = index;
                button.onClick.AddListener(
                    () => SubmitPhaseNode(captured));
                Text label = CreateText(
                    node.transform,
                    labels[index],
                    14,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.08f, 0.12f),
                    new Vector2(0.92f, 0.88f),
                    TextAnchor.MiddleCenter);
                label.raycastTarget = false;
                phaseNodes[index] = node;
                phaseNodeButtons[index] = button;
                phaseNodeLabels[index] = label;
            }

            CreateButton(
                window.transform,
                "Cancelar Navegação",
                "CANCELAR",
                new Vector2(0.39f, 0.045f),
                new Vector2(0.61f, 0.18f),
                Gold,
                ClosePhaseNavigatorFromUser);
            phaseNavigator.SetActive(false);
        }

        private void PolishPhaseControl()
        {
            if (phaseButton == null)
                return;
            Graphic graphic = phaseButton.targetGraphic;
            if (graphic != null)
                graphic.color = new Color(0.01f, 0.16f, 0.20f, 0.98f);
            AddOutline(phaseButton.gameObject, Cyan);
            ColorBlock colors = phaseButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Lime;
            colors.pressedColor = Gold;
            colors.disabledColor = new Color(0.35f, 0.45f, 0.50f, 0.72f);
            colors.fadeDuration = 0.10f;
            phaseButton.colors = colors;
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
                Image image = phaseNodes[index].GetComponent<Image>();
                Outline outline = phaseNodes[index].GetComponent<Outline>();
                if (legal)
                {
                    image.color = new Color(
                        Lime.r,
                        Lime.g,
                        Lime.b,
                        0.96f);
                    phaseNodeLabels[index].color =
                        new Color(0.01f, 0.04f, 0.03f, 1f);
                    outline.effectColor = Lime;
                }
                else if (active)
                {
                    image.color = new Color(
                        Cyan.r,
                        Cyan.g,
                        Cyan.b,
                        0.90f);
                    phaseNodeLabels[index].color =
                        new Color(0.01f, 0.04f, 0.06f, 1f);
                    outline.effectColor = Color.white;
                }
                else
                {
                    image.color =
                        new Color(0.02f, 0.075f, 0.10f, 0.92f);
                    phaseNodeLabels[index].color =
                        new Color(Muted.r, Muted.g, Muted.b, 0.58f);
                    outline.effectColor =
                        new Color(Cyan.r, Cyan.g, Cyan.b, 0.20f);
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
            if (activeDuelUiSurface != DuelUiSurfaceKind.PhaseNavigator)
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
            if (announcementRoutine == null)
                announcementRoutine =
                    StartCoroutine(PlayAnnouncementQueue());
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
                    Outline outline =
                        announcementRoot.GetComponent<Outline>();
                    if (outline != null)
                        outline.effectColor = item.Accent;

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
            StartCoroutine(ShakeArenaCamera(0.16f, 0.055f));
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
            Outline outline = battleHud.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = accent;
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

        private IEnumerator PlayDamagePresentation(
            byte player,
            uint value)
        {
            StartCoroutine(FlashLifeDamage(player));
            GameObject floating = CreatePanel(
                frame,
                "Dano Flutuante",
                player == 0
                    ? new Vector2(0.035f, 0.12f)
                    : new Vector2(0.72f, 0.78f),
                player == 0
                    ? new Vector2(0.27f, 0.23f)
                    : new Vector2(0.965f, 0.89f),
                new Color(0.09f, 0.005f, 0.012f, 0.88f));
            floating.transform.SetAsLastSibling();
            floating.GetComponent<Image>().raycastTarget = false;
            AddOutline(floating, Red);
            CanvasGroup group = floating.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            Text amount = CreateText(
                floating.transform,
                $"-{value:N0} PV",
                29,
                FontStyle.Bold,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter);
            amount.raycastTarget = false;
            RectTransform rect =
                floating.GetComponent<RectTransform>();
            Vector2 from = rect.anchoredPosition;
            float duration =
                DuelAnimationPreferences.Duration(0.62f);
            for (float elapsed = 0f;
                 elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition =
                    from + Vector2.up * Mathf.Lerp(0f, 54f, t);
                rect.localScale =
                    Vector3.one *
                    (1f + Mathf.Sin(t * Mathf.PI) * 0.16f);
                group.alpha =
                    t < 0.62f
                        ? 1f
                        : 1f - (t - 0.62f) / 0.38f;
                yield return null;
            }
            Destroy(floating);
        }

        private IEnumerator ShakeArenaCamera(
            float baseDuration,
            float strength)
        {
            Camera camera = Camera.main;
            if (camera == null)
                yield break;
            Transform cameraTransform = camera.transform;
            Vector3 origin = cameraTransform.localPosition;
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
}
