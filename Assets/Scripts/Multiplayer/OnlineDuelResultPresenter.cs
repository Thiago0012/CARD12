using System;
using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Frontend;
using ArcaneArena.Presentation;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Multiplayer
{
    [Serializable]
    public sealed class DuelResultMissionSummary
    {
        public string name;
        public long current;
        public long target;
        public bool completed;
        public int rewardCoins;
    }

    [Serializable]
    public sealed class DuelResultSummary
    {
        public bool showsCoinReward;
        public int coinsEarned;
        public int balanceAfter;
        public long damageDealt;
        public long damageReceived;
        public int roundsOrTurns;
        public int confirmedPlays;
        public List<DuelResultMissionSummary> missions = new();

        public string PerformanceLabel()
        {
            if (damageDealt <= 0 && damageReceived <= 0)
                return "OBJETIVO CONCLUÍDO";
            if (damageDealt >= damageReceived + 2000)
                return "PRESSÃO OFENSIVA";
            if (damageReceived >= damageDealt + 2000)
                return "RESISTÊNCIA EM CAMPO";
            return "DESEMPENHO EQUILIBRADO";
        }

        public static DuelResultSummary Capture(
            GameFrontendBootstrap frontend,
            bool showsCoinReward,
            int coinsEarned,
            int balanceAfter,
            long damageDealt,
            long damageReceived,
            int roundsOrTurns,
            int confirmedPlays)
        {
            var summary = new DuelResultSummary
            {
                showsCoinReward = showsCoinReward,
                coinsEarned = Math.Max(0, coinsEarned),
                balanceAfter = balanceAfter,
                damageDealt = Math.Max(0L, damageDealt),
                damageReceived = Math.Max(0L, damageReceived),
                roundsOrTurns = Math.Max(0, roundsOrTurns),
                confirmedPlays = Math.Max(0, confirmedPlays)
            };
            IReadOnlyList<MissionProgressState> progress =
                frontend?.CaptureMissionProgress(3);
            if (progress == null)
                return summary;
            foreach (MissionProgressState mission in progress)
            {
                if (mission == null)
                    continue;
                summary.missions.Add(new DuelResultMissionSummary
                {
                    name = mission.displayName ?? "MISSÃO",
                    current = mission.currentValue,
                    target = Math.Max(1L, mission.targetValue),
                    completed = mission.completed,
                    rewardCoins = Math.Max(0, mission.rewardCoins)
                });
            }
            return summary;
        }
    }

    [DisallowMultipleComponent]
    public sealed class OnlineDuelResultPresenter : MonoBehaviour
    {
        private Canvas canvas;
        private RectTransform safeAreaPanel;
        private Text titleLabel;
        private Text detailLabel;
        private Button returnButton;
        private GameObject rankedRoot;
        private Button skipButton;
        private RankTransitionAnimator rankTransition;
        private RankPromotionCinematic rankCinematic;
        private GameObject rankPanel;
        private Coroutine developmentPreviewRoutine;
        private Action developmentPreviewCompleted;
        private GameObject summaryRoot;
        private Text summaryTitle;
        private readonly Text[] summaryStats = new Text[3];
        private readonly GameObject[] missionRows = new GameObject[3];
        private readonly Text[] missionLabels = new Text[3];
        private readonly Image[] missionFills = new Image[3];
        private Action returnAction;
        private Rect lastSafeArea;

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public bool ReturnButtonInteractable =>
            returnButton != null && returnButton.interactable;
        public bool IsDevelopmentPreviewPlaying =>
            developmentPreviewRoutine != null;

        public static bool CanPresentRankTransition(
            RankChangeReceipt receipt)
        {
            return receipt != null &&
                   (receipt.status == RankReceiptStatus.Applied ||
                    receipt.status == RankReceiptStatus.AlreadyProcessed);
        }

        public void Show(
            OnlineDuelResultKind result,
            string detail,
            Action onReturn)
        {
            ShowInternal(result, detail, null, onReturn);
        }

        public void ShowWithSummary(
            OnlineDuelResultKind result,
            string detail,
            DuelResultSummary summary,
            Action onReturn)
        {
            ShowInternal(result, detail, summary, onReturn);
        }

        private void ShowInternal(
            OnlineDuelResultKind result,
            string detail,
            DuelResultSummary summary,
            Action onReturn)
        {
            CancelDevelopmentRankShowcase();
            EnsureView();
            rankedRoot.SetActive(false);
            skipButton.gameObject.SetActive(false);
            titleLabel.gameObject.SetActive(true);
            detailLabel.gameObject.SetActive(true);
            titleLabel.text = Title(result);
            titleLabel.color = ColorFor(result);
            detailLabel.text = detail ?? string.Empty;
            PresentSummary(result, summary);
            returnAction = onReturn;
            returnButton.interactable = true;
            canvas.gameObject.SetActive(true);
            ApplySafeArea();
            DuelResultAudioPlayer.Play(result);
        }

        public void ShowRanked(
            OnlineDuelResultKind result,
            string detail,
            RankChangeReceipt committedReceipt,
            Action onReturn)
        {
            ShowRankedInternal(
                result,
                detail,
                committedReceipt,
                null,
                onReturn);
        }

        public void ShowRankedWithSummary(
            OnlineDuelResultKind result,
            string detail,
            RankChangeReceipt committedReceipt,
            DuelResultSummary summary,
            Action onReturn)
        {
            ShowRankedInternal(
                result,
                detail,
                committedReceipt,
                summary,
                onReturn);
        }

        private void ShowRankedInternal(
            OnlineDuelResultKind result,
            string detail,
            RankChangeReceipt committedReceipt,
            DuelResultSummary summary,
            Action onReturn)
        {
            CancelDevelopmentRankShowcase();
            if (!CanPresentRankTransition(committedReceipt))
            {
                ShowInternal(result, detail, summary, onReturn);
                return;
            }

            EnsureView();
            titleLabel.gameObject.SetActive(false);
            detailLabel.gameObject.SetActive(false);
            returnAction = onReturn;
            PresentSummary(result, summary);
            rankedRoot.SetActive(true);
            canvas.gameObject.SetActive(true);
            ApplySafeArea();
            rankTransition.Play(
                result,
                committedReceipt,
                () => { });
            DuelResultAudioPlayer.Play(result);
        }

        public void SetReturnButtonInteractable(bool interactable)
        {
            if (returnButton != null)
                returnButton.interactable = interactable;
        }

        public void Hide()
        {
            CancelDevelopmentRankShowcase();
            returnAction = null;
            DuelResultAudioPlayer.StopPlayback();
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
            if (summaryRoot != null)
                summaryRoot.SetActive(false);
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        public void PlayDevelopmentRankShowcase(
            Action onCompleted = null)
        {
            EnsureView();
            if (developmentPreviewRoutine != null)
                return;
            developmentPreviewCompleted = onCompleted;
            returnAction = null;
            titleLabel.gameObject.SetActive(false);
            detailLabel.gameObject.SetActive(false);
            summaryRoot?.SetActive(false);
            returnButton.gameObject.SetActive(false);
            skipButton.gameObject.SetActive(false);
            rankedRoot.SetActive(true);
            rankPanel?.SetActive(false);
            canvas.gameObject.SetActive(true);
            ApplySafeArea();
            developmentPreviewRoutine = StartCoroutine(
                PlayDevelopmentRankShowcaseSequence());
        }

        public void CancelDevelopmentRankShowcase()
        {
            if (developmentPreviewRoutine == null)
                return;
            StopCoroutine(developmentPreviewRoutine);
            developmentPreviewRoutine = null;
            FinishDevelopmentRankShowcase();
        }

        private IEnumerator PlayDevelopmentRankShowcaseSequence()
        {
            for (int tierValue = (int)RankTier.Wood;
                 tierValue < (int)RankTier.GrandMaster;
                 tierValue++)
            {
                RankTier tier = (RankTier)tierValue;
                RankTier next = (RankTier)(tierValue + 1);
                yield return rankCinematic.Play(tier, next, true);
            }
            developmentPreviewRoutine = null;
            FinishDevelopmentRankShowcase();
        }

        private void FinishDevelopmentRankShowcase()
        {
            rankCinematic?.Hide();
            rankPanel?.SetActive(true);
            rankedRoot?.SetActive(false);
            skipButton?.gameObject.SetActive(false);
            if (returnButton != null)
                returnButton.gameObject.SetActive(true);
            if (canvas != null)
                canvas.gameObject.SetActive(false);
            Action completed = developmentPreviewCompleted;
            developmentPreviewCompleted = null;
            completed?.Invoke();
        }

        private void Update()
        {
            if (IsVisible)
                ApplySafeArea();
        }

        private void EnsureView()
        {
            if (canvas != null)
                return;

            Font font = MasterDuelTypography.Resolve(FontStyle.Normal, 17);
            GameObject canvasObject = new GameObject(
                "OnlineDuelResultCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32761;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Image blocker = CreateImage(
                canvasObject.transform,
                "ResultBlocker",
                new Color(0.003f, 0.010f, 0.022f, 0.64f),
                Vector2.zero,
                Vector2.one);
            blocker.raycastTarget = true;
            GameObject safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(blocker.transform, false);
            safeAreaPanel = safe.GetComponent<RectTransform>();
            Stretch(safeAreaPanel, Vector2.zero, Vector2.one);

            titleLabel = CreateText(
                safe.transform,
                "ResultTitle",
                font,
                72,
                FontStyle.Bold,
                new Vector2(0.08f, 0.61f),
                new Vector2(0.92f, 0.80f));
            detailLabel = CreateText(
                safe.transform,
                "ResultDetail",
                font,
                25,
                FontStyle.Normal,
                new Vector2(0.14f, 0.43f),
                new Vector2(0.86f, 0.60f));
            detailLabel.color = new Color(0.78f, 0.84f, 0.90f, 1f);

            Image buttonImage = CreateImage(
                safe.transform,
                "ReturnToMenuButton",
                Color.clear,
                new Vector2(0.36f, 0.025f),
                new Vector2(0.64f, 0.09f));
            returnButton = buttonImage.gameObject.AddComponent<Button>();
            returnButton.targetGraphic = AddModernSurface(
                buttonImage,
                "Superfície do Botão de Retorno",
                new Color(0.15f, 0.82f, 1f, 1f),
                0.96f,
                12f);
            returnButton.onClick.AddListener(() => returnAction?.Invoke());
            Text buttonText = CreateText(
                buttonImage.transform,
                "Label",
                font,
                25,
                FontStyle.Bold,
                Vector2.zero,
                Vector2.one);
            buttonText.text = "VOLTAR AO MENU";
            buttonText.color = Color.white;
            BuildSummaryView(safe.transform, font);
            BuildRankedView(
                safe.transform,
                canvasObject.transform,
                font);
            canvasObject.SetActive(false);
        }

        private void BuildSummaryView(Transform parent, Font font)
        {
            Image panel = CreateImage(
                parent,
                "DuelResultSummary",
                Color.clear,
                new Vector2(0.06f, 0.105f),
                new Vector2(0.94f, 0.30f));
            summaryRoot = panel.gameObject;
            AddModernSurface(
                panel,
                "Superfície do Resumo do Duelo",
                new Color(0.15f, 0.82f, 1f, 1f),
                0.88f,
                15f);
            summaryTitle = CreateText(
                panel.transform,
                "DuelResultSummaryTitle",
                font,
                17,
                FontStyle.Bold,
                new Vector2(0.025f, 0.80f),
                new Vector2(0.475f, 0.98f));
            summaryTitle.alignment = TextAnchor.MiddleLeft;
            summaryTitle.color = new Color(0.62f, 0.94f, 1f, 1f);
            Text missionHeading = CreateText(
                panel.transform,
                "DuelResultMissionHeading",
                font,
                14,
                FontStyle.Bold,
                new Vector2(0.505f, 0.80f),
                new Vector2(0.975f, 0.98f));
            missionHeading.text = "PROGRESSO ATUAL DAS MISSÕES";
            missionHeading.alignment = TextAnchor.MiddleLeft;
            missionHeading.color = new Color(0.92f, 0.76f, 0.36f, 1f);

            for (int index = 0; index < summaryStats.Length; index++)
            {
                float left = 0.025f + index * 0.15f;
                Image tile = CreateImage(
                    panel.transform,
                    $"DuelResultStat{index + 1}",
                    Color.clear,
                    new Vector2(left, 0.10f),
                    new Vector2(left + 0.137f, 0.76f));
                AddModernSurface(
                    tile,
                    "Superfície da Estatística",
                    index == 0
                        ? new Color(0.92f, 0.72f, 0.24f, 1f)
                        : new Color(0.18f, 0.76f, 1f, 1f),
                    0.44f,
                    8f);
                summaryStats[index] = CreateText(
                    tile.transform,
                    "Value",
                    font,
                    15,
                    FontStyle.Bold,
                    new Vector2(0.06f, 0.06f),
                    new Vector2(0.94f, 0.94f));
            }

            for (int index = 0; index < missionRows.Length; index++)
            {
                float top = 0.76f - index * 0.235f;
                Image row = CreateImage(
                    panel.transform,
                    $"DuelResultMission{index + 1}",
                    Color.clear,
                    new Vector2(0.505f, top - 0.19f),
                    new Vector2(0.975f, top));
                missionRows[index] = row.gameObject;
                AddModernSurface(
                    row,
                    "Superfície da Missão",
                    new Color(0.18f, 0.76f, 1f, 1f),
                    0.32f,
                    6f);
                missionLabels[index] = CreateText(
                    row.transform,
                    "MissionLabel",
                    font,
                    12,
                    FontStyle.Bold,
                    new Vector2(0.025f, 0.30f),
                    new Vector2(0.975f, 0.96f));
                missionLabels[index].alignment = TextAnchor.MiddleLeft;
                Image track = CreateImage(
                    row.transform,
                    "MissionProgressTrack",
                    new Color(0.006f, 0.025f, 0.05f, 0.92f),
                    new Vector2(0.025f, 0.09f),
                    new Vector2(0.975f, 0.23f));
                Image fill = CreateImage(
                    track.transform,
                    "MissionProgressFill",
                    new Color(0.16f, 0.86f, 1f, 1f),
                    Vector2.zero,
                    Vector2.one);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
                fill.raycastTarget = false;
                missionFills[index] = fill;
            }
            summaryRoot.SetActive(false);
        }

        private void PresentSummary(
            OnlineDuelResultKind result,
            DuelResultSummary summary)
        {
            bool visible = summary != null && summaryRoot != null;
            summaryRoot?.SetActive(visible);
            if (!visible)
                return;

            summaryTitle.text =
                "RESUMO DO DUELO  ·  " + summary.PerformanceLabel();
            summaryStats[0].text = summary.showsCoinReward
                ? $"MOEDAS\n+{Mathf.Max(0, summary.coinsEarned)}" +
                  (summary.balanceAfter >= 0
                      ? $"\nSALDO {summary.balanceAfter:N0}"
                      : string.Empty)
                : summary.confirmedPlays > 0
                    ? $"JOGADAS\n{summary.confirmedPlays}"
                    : "RESULTADO\nVITÓRIA";
            summaryStats[1].text =
                $"DANO CAUSADO\n{Math.Max(0L, summary.damageDealt):N0}";
            summaryStats[2].text =
                $"DANO RECEBIDO\n{Math.Max(0L, summary.damageReceived):N0}" +
                (summary.roundsOrTurns > 0
                    ? $"\n{summary.roundsOrTurns} TURNOS"
                    : string.Empty);

            IReadOnlyList<DuelResultMissionSummary> missions =
                summary.missions ?? new List<DuelResultMissionSummary>();
            for (int index = 0; index < missionRows.Length; index++)
            {
                bool hasMission = index < missions.Count &&
                                  missions[index] != null;
                missionRows[index].SetActive(hasMission);
                if (!hasMission)
                    continue;
                DuelResultMissionSummary mission = missions[index];
                long target = Math.Max(1L, mission.target);
                long current = Math.Max(0L, Math.Min(mission.current, target));
                missionLabels[index].text = mission.completed
                    ? $"{mission.name}  ·  CONCLUÍDA  ·  +{Mathf.Max(0, mission.rewardCoins)} MOEDAS"
                    : $"{mission.name}  ·  {current:N0}/{target:N0}";
                missionLabels[index].color = mission.completed
                    ? new Color(0.68f, 1f, 0.22f, 1f)
                    : Color.white;
                missionFills[index].fillAmount = current / (float)target;
                missionFills[index].color = mission.completed
                    ? new Color(0.64f, 1f, 0.18f, 1f)
                    : new Color(0.16f, 0.86f, 1f, 1f);
            }
        }

        private void BuildRankedView(
            Transform parent,
            Transform fullscreenParent,
            Font font)
        {
            rankedRoot = new GameObject(
                "RankedResult",
                typeof(RectTransform));
            rankedRoot.transform.SetParent(parent, false);
            Stretch(
                rankedRoot.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.31f),
                new Vector2(0.95f, 0.94f));

            Image panel = CreateImage(
                rankedRoot.transform,
                "RankPanel",
                Color.clear,
                new Vector2(0.05f, 0.02f),
                new Vector2(0.95f, 0.98f));
            rankPanel = panel.gameObject;
            AddModernSurface(
                panel,
                "Superfície do Resultado Ranqueado",
                new Color(0.15f, 0.82f, 1f, 1f),
                0.68f,
                22f);

            GameObject bannerObject = new GameObject(
                "RankResultBanner",
                typeof(RectTransform));
            bannerObject.transform.SetParent(panel.transform, false);
            Stretch(
                bannerObject.GetComponent<RectTransform>(),
                new Vector2(0.20f, 0.77f),
                new Vector2(0.80f, 0.98f));
            Text result = CreateText(
                bannerObject.transform,
                "Outcome",
                font,
                43,
                FontStyle.Bold,
                new Vector2(0f, 0.42f),
                new Vector2(1f, 1f));
            Text delta = CreateText(
                bannerObject.transform,
                "Delta",
                font,
                29,
                FontStyle.Bold,
                new Vector2(0f, 0.02f),
                new Vector2(1f, 0.48f));
            Text transition = CreateText(
                panel.transform,
                "Transition",
                font,
                18,
                FontStyle.Bold,
                new Vector2(0.25f, 0.73f),
                new Vector2(0.75f, 0.79f));

            Image currentMiniFrame = CreateImage(
                panel.transform,
                "CurrentRankMiniFrame",
                Color.clear,
                new Vector2(0.10f, 0.34f),
                new Vector2(0.47f, 0.75f));
            AddModernSurface(
                currentMiniFrame,
                "Moldura do elo atual",
                new Color(0.20f, 0.75f, 1f, 1f),
                0.38f,
                18f);

            Image currentMini = CreateImage(
                panel.transform,
                "CurrentRankMini",
                Color.white,
                new Vector2(0.15f, 0.385f),
                new Vector2(0.42f, 0.705f));
            currentMini.preserveAspect = true;
            Text currentMiniLabel = CreateText(
                panel.transform,
                "CurrentRankMiniLabel",
                font,
                19,
                FontStyle.Bold,
                new Vector2(0.10f, 0.255f),
                new Vector2(0.47f, 0.355f));
            currentMiniLabel.color = new Color(0.60f, 0.94f, 1f, 1f);

            Image nextMiniFrame = CreateImage(
                panel.transform,
                "NextRankMiniFrame",
                Color.clear,
                new Vector2(0.53f, 0.34f),
                new Vector2(0.90f, 0.75f));
            AddModernSurface(
                nextMiniFrame,
                "Moldura do próximo elo",
                new Color(0.48f, 0.62f, 1f, 1f),
                0.38f,
                18f);

            Image nextMini = CreateImage(
                panel.transform,
                "NextRankMini",
                Color.white,
                new Vector2(0.58f, 0.385f),
                new Vector2(0.85f, 0.705f));
            nextMini.preserveAspect = true;
            Text nextMiniLabel = CreateText(
                panel.transform,
                "NextRankMiniLabel",
                font,
                19,
                FontStyle.Bold,
                new Vector2(0.53f, 0.255f),
                new Vector2(0.90f, 0.355f));
            nextMiniLabel.color = new Color(0.78f, 0.85f, 1f, 1f);

            CreateText(
                panel.transform,
                "ProgressTitle",
                font,
                15,
                FontStyle.Bold,
                new Vector2(0.18f, 0.185f),
                new Vector2(0.82f, 0.235f)).text = "PROGRESSO DE ELO";
            GameObject progressObject = new GameObject(
                "RankBarBackground",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ArcaneRankProgressGraphic));
            progressObject.transform.SetParent(panel.transform, false);
            Stretch(
                progressObject.GetComponent<RectTransform>(),
                new Vector2(0.16f, 0.105f),
                new Vector2(0.84f, 0.18f));
            ArcaneRankProgressGraphic progressGraphic =
                progressObject.GetComponent<ArcaneRankProgressGraphic>();
            progressGraphic.SetProgress(
                0f,
                new Color(0.10f, 0.82f, 0.95f, 1f),
                new Color(0.92f, 0.65f, 0.24f, 1f));
            progressGraphic.raycastTarget = false;
            Image barEnergyFlow = CreateImage(
                progressObject.transform,
                "RankBarEnergyFlow",
                Color.clear,
                Vector2.zero,
                Vector2.one);
            barEnergyFlow.type = Image.Type.Filled;
            barEnergyFlow.fillMethod = Image.FillMethod.Horizontal;
            barEnergyFlow.fillOrigin = 0;
            barEnergyFlow.raycastTarget = false;
            Text barValue = CreateText(
                progressObject.transform,
                "RankBarValue",
                font,
                23,
                FontStyle.Bold,
                Vector2.zero,
                Vector2.one);
            Text remaining = CreateText(
                panel.transform,
                "RankRemaining",
                font,
                16,
                FontStyle.Bold,
                new Vector2(0.20f, 0.025f),
                new Vector2(0.80f, 0.10f));
            remaining.color = new Color(0.65f, 0.80f, 0.93f, 1f);

            Image cinematicBackdrop = CreateImage(
                fullscreenParent,
                "RankPromotionFullscreen",
                new Color(0.001f, 0.004f, 0.012f, 0.97f),
                Vector2.zero,
                Vector2.one);
            cinematicBackdrop.raycastTarget = false;
            CanvasGroup cinematicGroup =
                cinematicBackdrop.gameObject.AddComponent<CanvasGroup>();
            cinematicGroup.alpha = 0f;
            cinematicGroup.interactable = false;
            cinematicGroup.blocksRaycasts = false;

            GameObject burstObject = new GameObject(
                "Atmosfera da Promoção",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelRankMasteryBurstGraphic));
            burstObject.transform.SetParent(cinematicBackdrop.transform, false);
            Stretch(
                burstObject.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.one);
            DuelRankMasteryBurstGraphic cinematicBurst =
                burstObject.GetComponent<DuelRankMasteryBurstGraphic>();
            cinematicBurst.raycastTarget = false;

            RawImage cinematicViewport = CreateRawImage(
                cinematicBackdrop.transform,
                "RankPromotionCinematicViewport",
                Vector2.zero,
                Vector2.one);
            RectTransform cinematicViewportRect =
                cinematicViewport.rectTransform;
            cinematicViewportRect.pivot = new Vector2(0.5f, 0.5f);
            cinematicViewportRect.anchoredPosition = Vector2.zero;
            cinematicViewportRect.offsetMin = Vector2.zero;
            cinematicViewportRect.offsetMax = Vector2.zero;
            // A cena 3D é renderizada sobre uma textura transparente 16:9.
            // O RawImage precisa ocupar o Canvas inteiro: limitar a saída a
            // um quadrado recortava justamente as pontas dos emblemas mais
            // largos, embora a atmosfera atrás dele já fosse fullscreen.
            cinematicViewport.uvRect = new Rect(0f, 0f, 1f, 1f);
            Text cinematicTitle = CreateText(
                cinematicBackdrop.transform,
                "RankPromotionTitle",
                font,
                31,
                FontStyle.Bold,
                new Vector2(0.18f, 0.83f),
                new Vector2(0.82f, 0.94f));
            cinematicTitle.color = new Color(0.64f, 0.94f, 1f, 1f);
            Text cinematicSubtitle = CreateText(
                cinematicBackdrop.transform,
                "RankPromotionSubtitle",
                font,
                24,
                FontStyle.Bold,
                new Vector2(0.18f, 0.08f),
                new Vector2(0.82f, 0.18f));
            cinematicSubtitle.color = Color.white;

            Image skipImage = CreateImage(
                fullscreenParent,
                "SkipRankAnimation",
                Color.clear,
                new Vector2(0.72f, 0.025f),
                new Vector2(0.92f, 0.09f));
            skipButton = skipImage.gameObject.AddComponent<Button>();
            skipButton.targetGraphic = AddModernSurface(
                skipImage,
                "Superfície de Pular Animação",
                new Color(0.15f, 0.82f, 1f, 1f),
                0.90f,
                9f);
            Text skipText = CreateText(
                skipImage.transform,
                "Label",
                font,
                18,
                FontStyle.Bold,
                Vector2.zero,
                Vector2.one);
            skipText.text = "PULAR ANIMAÇÃO";

            RankPointsBarView barView =
                rankedRoot.AddComponent<RankPointsBarView>();
            barView.Initialize(
                progressGraphic,
                barEnergyFlow,
                barValue,
                remaining);
            RankEmblemView emblemView =
                rankedRoot.AddComponent<RankEmblemView>();
            emblemView.Initialize(currentMini, currentMiniLabel);
            RankSideSlotView sideView =
                rankedRoot.AddComponent<RankSideSlotView>();
            sideView.Initialize(
                currentMini,
                currentMiniLabel,
                nextMini,
                nextMiniLabel);
            RankResultBanner resultBanner =
                bannerObject.AddComponent<RankResultBanner>();
            resultBanner.Initialize(result, delta, transition);
            RankPromotionCinematic cinematic =
                rankedRoot.AddComponent<RankPromotionCinematic>();
            cinematic.Initialize(
                cinematicViewport,
                cinematicGroup,
                cinematicBurst,
                cinematicTitle,
                cinematicSubtitle);
            rankCinematic = cinematic;
            rankTransition =
                rankedRoot.AddComponent<RankTransitionAnimator>();
            rankTransition.Initialize(
                barView,
                emblemView,
                sideView,
                resultBanner,
                cinematic,
                returnButton,
                skipButton);
            rankedRoot.SetActive(false);
            skipButton.gameObject.SetActive(false);
        }

        private void ApplySafeArea()
        {
            Rect area = Screen.safeArea;
            if (area == lastSafeArea || Screen.width <= 0 || Screen.height <= 0)
                return;
            lastSafeArea = area;
            safeAreaPanel.anchorMin = new Vector2(
                area.xMin / Screen.width,
                area.yMin / Screen.height);
            safeAreaPanel.anchorMax = new Vector2(
                area.xMax / Screen.width,
                area.yMax / Screen.height);
            safeAreaPanel.offsetMin = Vector2.zero;
            safeAreaPanel.offsetMax = Vector2.zero;
        }

        private static string Title(OnlineDuelResultKind result)
        {
            return result switch
            {
                OnlineDuelResultKind.Victory => "VITÓRIA",
                OnlineDuelResultKind.Defeat => "DERROTA",
                OnlineDuelResultKind.Draw => "EMPATE",
                OnlineDuelResultKind.NoContest => "PARTIDA ENCERRADA",
                _ => "ERRO AO FINALIZAR PARTIDA"
            };
        }

        private static Color ColorFor(OnlineDuelResultKind result)
        {
            return result switch
            {
                OnlineDuelResultKind.Victory => new Color(0.66f, 1f, 0.08f, 1f),
                OnlineDuelResultKind.Defeat => new Color(1f, 0.28f, 0.36f, 1f),
                OnlineDuelResultKind.Draw => new Color(1f, 0.78f, 0.20f, 1f),
                _ => new Color(0.30f, 0.88f, 1f, 1f)
            };
        }

        private static Image CreateImage(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(Image));
            value.transform.SetParent(parent, false);
            Stretch(value.GetComponent<RectTransform>(), anchorMin, anchorMax);
            Image image = value.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static RawImage CreateRawImage(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(RawImage));
            value.transform.SetParent(parent, false);
            Stretch(value.GetComponent<RectTransform>(), anchorMin, anchorMax);
            RawImage image = value.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static DuelHudSurfaceGraphic AddModernSurface(
            Image legacyImage,
            string name,
            Color accent,
            float opacity,
            float chamfer)
        {
            legacyImage.color = Color.clear;
            GameObject surfaceObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelHudSurfaceGraphic));
            surfaceObject.transform.SetParent(legacyImage.transform, false);
            Stretch(
                surfaceObject.GetComponent<RectTransform>(),
                Vector2.zero,
                Vector2.one);
            DuelHudSurfaceGraphic surface =
                surfaceObject.GetComponent<DuelHudSurfaceGraphic>();
            surface.raycastTarget = false;
            surface.SetStyle(accent, true, opacity, false, chamfer);
            surfaceObject.transform.SetAsFirstSibling();
            return surface;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            int size,
            FontStyle style,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(Text));
            value.transform.SetParent(parent, false);
            Stretch(value.GetComponent<RectTransform>(), anchorMin, anchorMax);
            Text text = value.GetComponent<Text>();
            text.font = MasterDuelTypography.Resolve(style, size);
            text.fontSize = size;
            text.fontStyle = style == FontStyle.Italic ||
                             style == FontStyle.BoldAndItalic
                ? FontStyle.Italic
                : FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
