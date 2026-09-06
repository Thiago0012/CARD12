using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneArena.Frontend;
using ArcaneArena.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArcaneArena.Multiplayer
{
    public enum LoadingCardMotionStyle
    {
        ArcaneBurst,
        DuelCharge,
        MultiplayerCrossflow,
        DeckFan,
        ShopSpiral
    }

    /// <summary>
    /// Persistent, unscaled pre-duel surface shared by scene transitions,
    /// online synchronization and the rock-paper-scissors prelude.
    /// It never reads or mutates duel rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnlineLoadingScreenPresenter : MonoBehaviour
    {
        private const float FadeInSeconds = 0.46f;
        private const float FadeOutSeconds = 0.42f;
        private const float SceneCardBurstSeconds = 1.55f;
        public const float PreludeTiePresentationSeconds = 1.62f;
        public const float PreludeWinPresentationSeconds = 2.10f;
        private float minimumVisibleSeconds = 0.35f;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform safeAreaPanel;
        private RectTransform spinner;
        private Image spinnerImage;
        private RectTransform progressRoot;
        private Image progressFill;
        private Text progressLabel;
        private Text primaryLabel;
        private Text secondaryLabel;
        private GameObject choicePanel;
        private GameObject resultPanel;
        private GameObject startingPlayerPanel;
        private Text resultLabel;
        private Image resultLocalChoiceIcon;
        private Image resultOpponentChoiceIcon;
        private Text resultVersusLabel;
        private readonly List<Button> choiceButtons = new();
        private readonly List<Button> startingPlayerButtons = new();
        private readonly Dictionary<DuelPreludeChoice, Sprite>
            preludeChoiceIcons = new();
        private readonly List<RectTransform> lightStreaks = new();
        private readonly List<Image> lightStreakImages = new();
        private readonly List<Color> lightStreakBaseColors = new();
        private readonly List<RectTransform> floatingCards = new();
        private readonly List<Image> floatingCardImages = new();
        private readonly List<RectTransform> floatingCardTrails = new();
        private readonly List<Image> floatingCardTrailImages = new();
        private readonly List<Vector2> floatingCardStarts = new();
        private readonly List<Vector2> floatingCardOrigins = new();
        private readonly List<float> floatingCardDepths = new();
        private readonly List<float> floatingCardAngles = new();
        private readonly List<Vector2> floatingCardControls = new();
        private readonly List<float> floatingCardDelays = new();
        private readonly List<float> floatingCardDurations = new();
        private readonly List<float> floatingCardScales = new();
        private readonly List<float> floatingCardSpins = new();
        private readonly List<Color> floatingCardBaseColors = new();
        private readonly List<float> lightStreakAngles = new();
        private readonly List<Sprite> cachedCardArtwork = new();
        private readonly List<string> pinnedCardArtworkIds = new();
        private readonly List<Image> burstRings = new();
        private readonly List<Image> perimeterGlows = new();
        private readonly List<float> perimeterGlowPhases = new();
        private readonly List<RectTransform> burstSparks = new();
        private readonly List<Image> burstSparkImages = new();
        private readonly List<Vector2> burstSparkDirections = new();
        private readonly List<float> burstSparkSpeeds = new();
        private readonly List<float> burstSparkDelays = new();
        private readonly List<float> burstSparkSizes = new();
        private readonly List<float> burstSparkSpins = new();
        private readonly System.Random visualRandom =
            new System.Random(Environment.TickCount);
        private Button backButton;
        private Action backAction;
        private Action<DuelPreludeChoice> choiceAction;
        private Action<bool> startingPlayerChoiceAction;
        private Coroutine transitionRoutine;
        private float targetAlpha;
        private float shownAt;
        private bool hideRequested;
        private bool loadingMode;
        private float progressValue;
        private Rect lastSafeArea;
        private float cardBurstStartedAt = -100f;
        private LoadingCardMotionStyle motionStyle =
            LoadingCardMotionStyle.ArcaneBurst;
        private Image burstGlowOuter;
        private Image burstGlowInner;
        private Image burstFlashHorizontal;
        private Image burstFlashVertical;
        private Image transitionVoidLayer;
        private GameObject preludeBackdrop;
        private Image preludeChoiceArenaImage;
        private CanvasGroup preludeChoiceArenaGroup;
        private Image preludeClashArenaImage;
        private CanvasGroup preludeClashArenaGroup;
        private readonly List<RectTransform> preludeMistLayers = new();
        private readonly List<Image> preludeMistImages = new();
        private readonly List<Vector2> preludeMistOrigins = new();
        private readonly List<float> preludeMistSpeeds = new();
        private readonly List<float> preludeMistPhases = new();
        private readonly List<Color> preludeMistBaseColors = new();
        private readonly List<GameObject> preludeResultEffects = new();
        private Coroutine preludeResultAnimationRoutine;
        private Coroutine preludeModeTransitionRoutine;
        private Sprite preludeImpactSprite;
        private CanvasGroup choicePanelGroup;
        private CanvasGroup resultPanelGroup;
        private Image preludeCinematicVeil;
        private Image preludeCinematicRay;
        private Color motionAccentA = new Color(0.12f, 0.76f, 1f, 1f);
        private Color motionAccentB = new Color(0.50f, 0.18f, 0.98f, 1f);

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public bool IsOpaque => IsVisible && group != null && group.alpha >= 0.995f;

        private void OnDestroy()
        {
            ReleaseFloatingCardArtwork();
        }

        public void ConfigureMinimumVisible(float seconds)
        {
            minimumVisibleSeconds = Mathf.Clamp(seconds, 0.1f, 3f);
        }

        public void Show(string primary, string secondary = "")
        {
            bool enteringSurface = !IsVisible;
            loadingMode = false;
            PrepareVisibleSurface();
            CancelPreludeModeTransition();
            SetPreludeMode(false, false);
            ApplyText(primary, secondary);
            spinner.gameObject.SetActive(true);
            progressRoot.gameObject.SetActive(false);
            backButton.gameObject.SetActive(false);
            backAction = null;
            RefreshFloatingCardArtwork();
            if (enteringSurface)
                RestartFloatingCardBurst();
        }

        public void ShowDuelLoading(
            string primary,
            string secondary = "",
            float initialProgress = 0.04f)
        {
            bool enteringLoading = !IsVisible || !loadingMode ||
                                   choicePanel?.activeSelf == true ||
                                   resultPanel?.activeSelf == true;
            bool exitingPrelude = IsPreludeActive();
            loadingMode = true;
            PrepareVisibleSurface();
            if (exitingPrelude)
                PrimePreludeExitTransition();
            SetPreludeMode(false, false);
            ApplyText(primary, secondary);
            spinner.gameObject.SetActive(true);
            progressRoot.gameObject.SetActive(true);
            backButton.gameObject.SetActive(false);
            backAction = null;
            if (enteringLoading)
                SetProgress(initialProgress);
            else if (initialProgress > progressValue)
                SetProgress(initialProgress);
            RefreshFloatingCardArtwork();
            if (enteringLoading)
                RestartFloatingCardBurst();
            if (exitingPrelude)
                BeginPreludeExitTransition();
        }

        public void SetText(string primary, string secondary = "")
        {
            PrepareVisibleSurface();
            CancelPreludeModeTransition();
            SetPreludeMode(false, false);
            ApplyText(primary, secondary);
            spinner.gameObject.SetActive(true);
            progressRoot.gameObject.SetActive(loadingMode);
            if (group != null)
                group.alpha = Mathf.Max(group.alpha, 0.001f);
        }

        public void SetProgress(float value)
        {
            EnsureView();
            value = Mathf.Clamp01(value);
            progressValue = value;
            progressRoot.gameObject.SetActive(loadingMode);
            progressFill.rectTransform.anchorMax = new Vector2(value, 1f);
            progressFill.rectTransform.offsetMax = Vector2.zero;
            progressLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }

        public void ShowRockPaperScissors(
            string opponentName,
            int round,
            Action<DuelPreludeChoice> onChoice)
        {
            loadingMode = false;
            PrepareVisibleSurface();
            CancelPreludeModeTransition();
            SetPreludeMode(true, false);
            RestorePreludePanelPresentation();
            ResetPreludeResultVisuals();
            ResetPreludeBackdropFraming();
            choiceAction = onChoice;
            primaryLabel.text = "QUEM INICIA O DUELO?";
            secondaryLabel.text = string.IsNullOrWhiteSpace(opponentName)
                ? $"RODADA {Mathf.Max(1, round)} · ESCOLHA EM SEGREDO"
                : $"CONTRA {opponentName.ToUpperInvariant()} · RODADA {Mathf.Max(1, round)}";
            secondaryLabel.gameObject.SetActive(true);
            foreach (Button button in choiceButtons)
            {
                button.interactable = true;
                SetChoiceButtonSelected(button, false);
            }
        }

        public void ShowRockPaperScissorsWaiting(string message)
        {
            if (!IsVisible)
                return;
            foreach (Button button in choiceButtons)
                button.interactable = false;
            secondaryLabel.text = string.IsNullOrWhiteSpace(message)
                ? "Escolha confirmada · aguardando o rival..."
                : message;
            secondaryLabel.gameObject.SetActive(true);
        }

        public void ShowRockPaperScissorsResult(
            DuelPreludeChoice localChoice,
            DuelPreludeChoice opponentChoice,
            bool localWon,
            bool tie)
        {
            bool revealFromChoices = choicePanel != null &&
                                     choicePanel.activeSelf;
            loadingMode = false;
            PrepareVisibleSurface();
            CancelPreludeModeTransition();
            SetPreludeMode(false, true);
            if (!revealFromChoices)
                SetPreludeArenaBlend(1f);
            if (revealFromChoices && choicePanel != null)
            {
                choicePanel.SetActive(true);
                SetPreludePanelOpacity(choicePanelGroup, 1f);
            }
            SetPreludePanelOpacity(resultPanelGroup,
                revealFromChoices ? 0f : 1f);
            primaryLabel.text = tie
                ? "EMPATE"
                : localWon ? "VOCÊ VENCEU" : "O RIVAL VENCEU";
            secondaryLabel.text = tie
                ? "As escolhas foram iguais. Uma nova rodada será iniciada."
                : "Resultado confirmado · preparando os dois campos.";
            secondaryLabel.gameObject.SetActive(true);
            ResetPreludeResultVisuals();
            if (resultLocalChoiceIcon != null)
                resultLocalChoiceIcon.sprite = PreludeChoiceIcon(localChoice);
            if (resultOpponentChoiceIcon != null)
                resultOpponentChoiceIcon.sprite = PreludeChoiceIcon(opponentChoice);
            if (resultVersusLabel != null)
                resultVersusLabel.text = tie ? "=" : "VERSUS";
            preludeResultAnimationRoutine = StartCoroutine(
                AnimatePreludeResult(
                    localChoice,
                    opponentChoice,
                    localWon,
                    tie,
                    revealFromChoices));
        }

        /// <summary>
        /// Shows the first-turn decision after a player wins the pre-duel
        /// round. The callback receives true when the local winner chooses
        /// to start and false when they choose to play second.
        /// </summary>
        public void ShowStartingPlayerChoice(Action<bool> onChoice)
        {
            PresentStartingPlayerPanel(
                "VOCÊ VENCEU A ESCOLHA!",
                "DEFINA QUEM INICIA O DUELO.",
                onChoice,
                true);
        }

        public void ShowStartingPlayerWaiting(string message)
        {
            loadingMode = false;
            PrepareVisibleSurface();
            CancelPreludeModeTransition();
            ResetPreludeResultVisuals();
            SetPreludeMode(false, false);
            RestorePreludePanelPresentation();
            SetPreludeBackdropVisible(true);
            ApplyPreludeTypographyLayout(true);
            spinner.gameObject.SetActive(false);
            progressRoot.gameObject.SetActive(false);
            primaryLabel.text = "AGUARDANDO O VENCEDOR";
            secondaryLabel.text = string.IsNullOrWhiteSpace(message)
                ? "O VENCEDOR ESTÁ DEFININDO QUEM INICIA."
                : message;
            secondaryLabel.gameObject.SetActive(true);
            startingPlayerChoiceAction = null;
            if (startingPlayerPanel != null)
                startingPlayerPanel.SetActive(false);
        }

        private sealed class PreludeResultFragment
        {
            public Image image;
            public Vector2 velocity;
            public float spin;
            public Color color;
        }

        private void ResetPreludeResultVisuals()
        {
            if (preludeResultAnimationRoutine != null)
            {
                StopCoroutine(preludeResultAnimationRoutine);
                preludeResultAnimationRoutine = null;
            }
            ClearPreludeResultEffects();
            ResetPreludeResultIcon(resultLocalChoiceIcon);
            ResetPreludeResultIcon(resultOpponentChoiceIcon);
            if (resultVersusLabel != null)
            {
                resultVersusLabel.gameObject.SetActive(true);
                resultVersusLabel.rectTransform.localScale = Vector3.one;
                resultVersusLabel.color = new Color(0.66f, 0.93f, 1f, 1f);
            }
        }

        private static void ResetPreludeResultIcon(Image icon)
        {
            if (icon == null)
                return;
            icon.gameObject.SetActive(true);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.rectTransform.localScale = Vector3.one;
            icon.rectTransform.localEulerAngles = Vector3.zero;
            icon.color = Color.white;
        }

        private IEnumerator AnimatePreludeResult(
            DuelPreludeChoice localChoice,
            DuelPreludeChoice opponentChoice,
            bool localWon,
            bool tie,
            bool revealFromChoices)
        {
            if (revealFromChoices)
                yield return RevealPreludeResultFromChoices();
            yield return new WaitForSecondsRealtime(0.10f);
            if (resultLocalChoiceIcon == null || resultOpponentChoiceIcon == null)
            {
                preludeResultAnimationRoutine = null;
                yield break;
            }

            if (tie)
            {
                const float tieDuration = 0.48f;
                float elapsed = 0f;
                while (elapsed < tieDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / tieDuration);
                    float pulse = Mathf.Sin(progress * Mathf.PI);
                    SetPreludeBackdropFocus(pulse * 0.18f);
                    resultLocalChoiceIcon.rectTransform.anchoredPosition =
                        new Vector2(18f * pulse, 0f);
                    resultOpponentChoiceIcon.rectTransform.anchoredPosition =
                        new Vector2(-18f * pulse, 0f);
                    float scale = 1f + pulse * 0.08f;
                    resultLocalChoiceIcon.rectTransform.localScale =
                        new Vector3(scale, scale, 1f);
                    resultOpponentChoiceIcon.rectTransform.localScale =
                        new Vector3(scale, scale, 1f);
                    yield return null;
                }
                ResetPreludeResultIcon(resultLocalChoiceIcon);
                ResetPreludeResultIcon(resultOpponentChoiceIcon);
                ResetPreludeBackdropFraming();
                preludeResultAnimationRoutine = null;
                yield break;
            }

            Image winner = localWon
                ? resultLocalChoiceIcon
                : resultOpponentChoiceIcon;
            Image loser = localWon
                ? resultOpponentChoiceIcon
                : resultLocalChoiceIcon;
            DuelPreludeChoice winnerChoice = localWon
                ? localChoice
                : opponentChoice;
            DuelPreludeChoice loserChoice = localWon
                ? opponentChoice
                : localChoice;
            Vector2 winnerOrigin = GetResultPanelPosition(
                winner.rectTransform);
            Vector2 loserOrigin = GetResultPanelPosition(
                loser.rectTransform);
            Vector2 impactPosition = Vector2.Lerp(
                winnerOrigin,
                loserOrigin,
                0.50f);
            Vector2 attackOffset = impactPosition - winnerOrigin;
            Vector2 loserApproachOffset = (impactPosition - loserOrigin) *
                                           0.17f;
            float direction = Mathf.Sign(attackOffset.x);
            float attackRotation = winnerChoice switch
            {
                DuelPreludeChoice.Scissors => -direction * 26f,
                DuelPreludeChoice.Paper => direction * 12f,
                _ => -direction * 7f
            };

            const float approachDuration = 0.30f;
            float approachElapsed = 0f;
            while (approachElapsed < approachDuration)
            {
                approachElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(approachElapsed / approachDuration);
                float eased = EaseOutCubic(progress);
                SetPreludeBackdropFocus(Mathf.Lerp(0.20f, 1f, eased));
                winner.rectTransform.anchoredPosition = attackOffset * eased;
                loser.rectTransform.anchoredPosition = loserApproachOffset *
                                                       eased;
                float winnerScale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.10f;
                winner.rectTransform.localScale =
                    new Vector3(winnerScale, winnerScale, 1f);
                winner.rectTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    attackRotation * Mathf.Sin(progress * Mathf.PI));
                yield return null;
            }

            List<PreludeResultFragment> fragments = SpawnPreludeImpact(
                impactPosition,
                winnerChoice,
                loserChoice,
                new Vector2(direction, 0f));
            const float destructionDuration = 0.36f;
            float destructionElapsed = 0f;
            while (destructionElapsed < destructionDuration)
            {
                destructionElapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(
                    destructionElapsed / destructionDuration);
                float eased = EaseOutCubic(progress);
                SetPreludeBackdropFocus(1f);
                winner.rectTransform.anchoredPosition = Vector2.Lerp(
                    attackOffset,
                    attackOffset * 0.16f,
                    eased);
                winner.rectTransform.localScale = Vector3.one *
                    (1.06f - 0.04f * eased);
                loser.rectTransform.anchoredPosition = Vector2.Lerp(
                    loserApproachOffset,
                    (loserOrigin - impactPosition) * 0.32f,
                    eased) +
                    Vector2.down * (28f * eased * eased);
                loser.rectTransform.localScale = Vector3.one *
                    Mathf.Lerp(1f, 0.16f, eased);
                Color loserColor = Color.white;
                loserColor.a = 1f - eased;
                loser.color = loserColor;
                if (resultVersusLabel != null)
                {
                    Color versusColor = resultVersusLabel.color;
                    versusColor.a = 1f - eased;
                    resultVersusLabel.color = versusColor;
                }
                UpdatePreludeImpactFragments(
                    fragments,
                    impactPosition,
                    eased);
                yield return null;
            }

            loser.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.18f);
            ClearPreludeResultEffects();
            preludeResultAnimationRoutine = null;
        }

        private Vector2 GetResultPanelPosition(RectTransform target)
        {
            if (target == null || resultPanel == null)
                return Vector2.zero;
            RectTransform panel = resultPanel.GetComponent<RectTransform>();
            return panel.InverseTransformPoint(target.position);
        }

        private List<PreludeResultFragment> SpawnPreludeImpact(
            Vector2 position,
            DuelPreludeChoice winnerChoice,
            DuelPreludeChoice loserChoice,
            Vector2 direction)
        {
            var fragments = new List<PreludeResultFragment>();
            if (resultPanel == null || preludeImpactSprite == null)
                return fragments;

            Color baseColor = Color.Lerp(
                PreludeChoiceAccent(winnerChoice),
                PreludeChoiceAccent(loserChoice),
                0.38f);
            Image flash = CreateImage(
                resultPanel.transform,
                "Impacto da escolha",
                new Color(baseColor.r, baseColor.g, baseColor.b, 0.58f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            flash.sprite = preludeImpactSprite;
            flash.rectTransform.sizeDelta = new Vector2(236f, 236f);
            flash.rectTransform.anchoredPosition = position;
            preludeResultEffects.Add(flash.gameObject);

            int fragmentCount = loserChoice == DuelPreludeChoice.Paper
                ? 14
                : loserChoice == DuelPreludeChoice.Scissors ? 11 : 9;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            for (int index = 0; index < fragmentCount; index++)
            {
                float random = (float)visualRandom.NextDouble();
                float side = Mathf.Lerp(-1f, 1f, random);
                float forward = Mathf.Lerp(90f, 230f,
                    (float)visualRandom.NextDouble());
                float sideways = Mathf.Lerp(-130f, 130f,
                    (float)visualRandom.NextDouble());
                Image fragment = CreateImage(
                    resultPanel.transform,
                    $"Fragmento da escolha {index + 1}",
                    Color.Lerp(baseColor, Color.white,
                        (float)visualRandom.NextDouble() * 0.55f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                fragment.sprite = preludeImpactSprite;
                float size = loserChoice == DuelPreludeChoice.Paper
                    ? Mathf.Lerp(14f, 34f, random)
                    : Mathf.Lerp(18f, 42f, random);
                fragment.rectTransform.sizeDelta = new Vector2(size, size);
                fragment.rectTransform.anchoredPosition = position;
                preludeResultEffects.Add(fragment.gameObject);
                fragments.Add(new PreludeResultFragment
                {
                    image = fragment,
                    velocity = direction * forward + perpendicular *
                               (sideways + side * 22f),
                    spin = Mathf.Lerp(-440f, 440f,
                        (float)visualRandom.NextDouble()),
                    color = fragment.color
                });
            }
            return fragments;
        }

        private static void UpdatePreludeImpactFragments(
            IReadOnlyList<PreludeResultFragment> fragments,
            Vector2 origin,
            float progress)
        {
            if (fragments == null)
                return;
            for (int index = 0; index < fragments.Count; index++)
            {
                PreludeResultFragment fragment = fragments[index];
                if (fragment?.image == null)
                    continue;
                fragment.image.rectTransform.anchoredPosition = origin +
                    fragment.velocity * progress +
                    Vector2.down * (70f * progress * progress);
                fragment.image.rectTransform.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    fragment.spin * progress);
                Color color = fragment.color;
                color.a *= 1f - progress;
                fragment.image.color = color;
            }
        }

        private void ClearPreludeResultEffects()
        {
            foreach (GameObject effect in preludeResultEffects)
            {
                if (effect != null)
                    Destroy(effect);
            }
            preludeResultEffects.Clear();
        }

        public void FadeThroughBlack(Action action)
        {
            EnsureView();
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(FadeThroughBlackRoutine(action));
        }

        public void ShowSceneLoading(
            string primary,
            string secondary,
            Action loadAction)
        {
            ShowSceneLoading(
                primary,
                secondary,
                LoadingCardMotionStyle.ArcaneBurst,
                loadAction);
        }

        public void ShowSceneLoading(
            string primary,
            string secondary,
            LoadingCardMotionStyle style,
            Action loadAction)
        {
            EnsureView();
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(
                ShowSceneLoadingRoutine(
                    primary,
                    secondary,
                    style,
                    loadAction));
        }

        public void ShowSceneLoading(
            string primary,
            string secondary,
            LoadingCardMotionStyle style,
            string sceneName)
        {
            EnsureView();
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(
                ShowSceneLoadingRoutine(
                    primary,
                    secondary,
                    style,
                    sceneName));
        }

        public void ShowFeatureTransition(
            string primary,
            string secondary,
            LoadingCardMotionStyle style,
            Action action)
        {
            EnsureView();
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(
                ShowFeatureTransitionRoutine(
                    primary,
                    secondary,
                    style,
                    action));
        }

        public void ShowError(string message, Action returnAction)
        {
            Show("Partida cancelada", message);
            backAction = returnAction;
            backButton.gameObject.SetActive(true);
            backButton.interactable = true;
        }

        public void Hide()
        {
            if (IsVisible)
                hideRequested = true;
        }

        public void HideImmediately()
        {
            if (canvas == null)
                return;
            CancelPreludeModeTransition();
            if (preludeResultAnimationRoutine != null)
            {
                StopCoroutine(preludeResultAnimationRoutine);
                preludeResultAnimationRoutine = null;
            }
            ClearPreludeResultEffects();
            ResetPreludeBackdropFraming();
            targetAlpha = 0f;
            hideRequested = false;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            canvas.gameObject.SetActive(false);
        }

        private IEnumerator FadeThroughBlackRoutine(Action action)
        {
            PrepareVisibleSurface();
            SetPreludeMode(false, false);
            loadingMode = false;
            spinner.gameObject.SetActive(false);
            progressRoot.gameObject.SetActive(false);
            primaryLabel.gameObject.SetActive(false);
            secondaryLabel.gameObject.SetActive(false);
            float deadline = Time.realtimeSinceStartup + 1.2f;
            while (!IsOpaque && Time.realtimeSinceStartup < deadline)
                yield return null;
            action?.Invoke();
            yield return new WaitForSecondsRealtime(0.12f);
            shownAt = Time.realtimeSinceStartup - minimumVisibleSeconds;
            Hide();
            transitionRoutine = null;
        }

        private IEnumerator ShowSceneLoadingRoutine(
            string primary,
            string secondary,
            LoadingCardMotionStyle style,
            Action loadAction)
        {
            bool wasVisible = IsVisible;
            motionStyle = style;
            Show(primary, secondary);
            if (wasVisible)
                RestartFloatingCardBurst();
            float visualDeadline = cardBurstStartedAt +
                                   SceneCardBurstSeconds;
            while (Time.unscaledTime < visualDeadline)
                yield return null;

            loadAction?.Invoke();
            yield return null;
            shownAt = Time.realtimeSinceStartup - minimumVisibleSeconds;
            Hide();
            transitionRoutine = null;
        }

        private IEnumerator ShowSceneLoadingRoutine(
            string primary,
            string secondary,
            LoadingCardMotionStyle style,
            string sceneName)
        {
            bool wasVisible = IsVisible;
            motionStyle = style;
            Show(primary, secondary);
            if (wasVisible)
                RestartFloatingCardBurst();
            float visualDeadline = cardBurstStartedAt +
                                   SceneCardBurstSeconds;
            while (Time.unscaledTime < visualDeadline)
                yield return null;

            AsyncOperation load = string.IsNullOrWhiteSpace(sceneName)
                ? null
                : SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (load != null)
            {
                while (!load.isDone)
                    yield return null;
            }
            shownAt = Time.realtimeSinceStartup - minimumVisibleSeconds;
            Hide();
            transitionRoutine = null;
        }

        private IEnumerator ShowFeatureTransitionRoutine(
            string primary,
            string secondary,
            LoadingCardMotionStyle style,
            Action action)
        {
            bool wasVisible = IsVisible;
            motionStyle = style;
            Show(primary, secondary);
            if (wasVisible)
                RestartFloatingCardBurst();
            float visualDeadline = cardBurstStartedAt +
                                   SceneCardBurstSeconds;
            while (Time.unscaledTime < visualDeadline)
                yield return null;

            action?.Invoke();
            yield return null;
            shownAt = Time.realtimeSinceStartup - minimumVisibleSeconds;
            Hide();
            transitionRoutine = null;
        }

        private void PrepareVisibleSurface()
        {
            EnsureView();
            if (!canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(true);
                group.alpha = 0f;
                shownAt = Time.realtimeSinceStartup;
            }
            primaryLabel.gameObject.SetActive(true);
            secondaryLabel.gameObject.SetActive(true);
            hideRequested = false;
            targetAlpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            backButton.gameObject.SetActive(false);
        }

        private void SetPreludeMode(bool choices, bool result)
        {
            choicePanel.SetActive(choices);
            resultPanel.SetActive(result);
            if (startingPlayerPanel != null)
                startingPlayerPanel.SetActive(false);
            SetPreludeBackdropVisible(choices || result);
            ApplyPreludeTypographyLayout(choices || result);
            spinner.gameObject.SetActive(!choices && !result);
            progressRoot.gameObject.SetActive(
                !choices && !result && loadingMode);
            choiceAction = choices ? choiceAction : null;
        }

        private bool IsPreludeActive()
        {
            return choicePanel?.activeSelf == true ||
                   resultPanel?.activeSelf == true ||
                   startingPlayerPanel?.activeSelf == true ||
                   preludeBackdrop?.activeSelf == true;
        }

        private void PresentStartingPlayerPanel(
            string title,
            string subtitle,
            Action<bool> onChoice,
            bool localCanChoose)
        {
            loadingMode = false;
            PrepareVisibleSurface();
            CancelPreludeModeTransition();
            ResetPreludeResultVisuals();
            SetPreludeMode(false, false);
            RestorePreludePanelPresentation();
            SetPreludeBackdropVisible(true);
            ApplyPreludeTypographyLayout(true);
            spinner.gameObject.SetActive(false);
            progressRoot.gameObject.SetActive(false);
            primaryLabel.text = title;
            secondaryLabel.text = subtitle;
            secondaryLabel.gameObject.SetActive(true);
            startingPlayerChoiceAction = onChoice;
            if (startingPlayerPanel != null)
                startingPlayerPanel.SetActive(true);
            foreach (Button button in startingPlayerButtons)
                button.interactable = localCanChoose;
        }

        private void ApplyPreludeTypographyLayout(bool preludeActive)
        {
            if (primaryLabel == null || secondaryLabel == null)
                return;

            if (preludeActive)
            {
                Stretch(
                    primaryLabel.rectTransform,
                    new Vector2(0.16f, 0.76f),
                    new Vector2(0.84f, 0.85f));
                Stretch(
                    secondaryLabel.rectTransform,
                    new Vector2(0.16f, 0.70f),
                    new Vector2(0.84f, 0.76f));
                return;
            }

            Stretch(
                primaryLabel.rectTransform,
                new Vector2(0.12f, 0.40f),
                new Vector2(0.88f, 0.49f));
            Stretch(
                secondaryLabel.rectTransform,
                new Vector2(0.12f, 0.33f),
                new Vector2(0.88f, 0.41f));
        }

        private static void SetPreludePanelOpacity(
            CanvasGroup panelGroup,
            float alpha)
        {
            if (panelGroup == null)
                return;
            panelGroup.alpha = Mathf.Clamp01(alpha);
            panelGroup.blocksRaycasts = alpha > 0.98f;
            panelGroup.interactable = alpha > 0.98f;
        }

        private void RestorePreludePanelPresentation()
        {
            SetPreludePanelOpacity(choicePanelGroup, 1f);
            SetPreludePanelOpacity(resultPanelGroup, 1f);
            if (choicePanel != null)
                choicePanel.GetComponent<RectTransform>().localScale = Vector3.one;
            if (resultPanel != null)
                resultPanel.GetComponent<RectTransform>().localScale = Vector3.one;
        }

        private void CancelPreludeModeTransition()
        {
            if (preludeModeTransitionRoutine != null)
            {
                StopCoroutine(preludeModeTransitionRoutine);
                preludeModeTransitionRoutine = null;
            }
            ResetPreludeCinematicVeil();
        }

        private void PrimePreludeExitTransition()
        {
            if (preludeCinematicVeil == null)
                return;

            CancelPreludeModeTransition();
            preludeCinematicVeil.gameObject.SetActive(true);
            preludeCinematicVeil.color = new Color(
                0.004f,
                0.014f,
                0.046f,
                0.97f);
            if (preludeCinematicRay != null)
            {
                preludeCinematicRay.gameObject.SetActive(true);
                preludeCinematicRay.color = new Color(
                    0.13f,
                    0.56f,
                    0.86f,
                    0.42f);
                preludeCinematicRay.rectTransform.localScale =
                    new Vector3(0.46f, 0.46f, 1f);
            }
        }

        private void BeginPreludeExitTransition()
        {
            if (preludeCinematicVeil == null ||
                !preludeCinematicVeil.gameObject.activeSelf)
            {
                return;
            }
            if (preludeModeTransitionRoutine != null)
                StopCoroutine(preludeModeTransitionRoutine);
            preludeModeTransitionRoutine = StartCoroutine(
                AnimatePreludeExitTransition());
        }

        private IEnumerator AnimatePreludeExitTransition()
        {
            const float duration = 0.58f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                Color veil = preludeCinematicVeil.color;
                veil.a = Mathf.Lerp(0.97f, 0f, eased);
                preludeCinematicVeil.color = veil;
                if (preludeCinematicRay != null)
                {
                    float flare = Mathf.Pow(
                        Mathf.Sin(progress * Mathf.PI),
                        0.72f);
                    Color ray = preludeCinematicRay.color;
                    ray.a = flare * 0.46f;
                    preludeCinematicRay.color = ray;
                    preludeCinematicRay.rectTransform.localScale =
                        new Vector3(
                            Mathf.Lerp(0.46f, 1.36f, EaseOutCubic(progress)),
                            Mathf.Lerp(0.46f, 1.36f, EaseOutCubic(progress)),
                            1f);
                }
                yield return null;
            }
            preludeModeTransitionRoutine = null;
            ResetPreludeCinematicVeil();
        }

        private IEnumerator RevealPreludeResultFromChoices()
        {
            // The result needs to read like a tiny scene change instead of
            // appearing abruptly on top of the three-choice layout:
            // confirmation -> original seals fade -> two raised plinths ->
            // selected pieces settle into the confrontation frame.
            const float duration = 0.82f;
            float elapsed = 0f;
            if (preludeCinematicVeil != null)
            {
                preludeCinematicVeil.gameObject.SetActive(true);
                preludeCinematicVeil.color = Color.clear;
            }
            if (preludeCinematicRay != null)
            {
                preludeCinematicRay.gameObject.SetActive(true);
                preludeCinematicRay.color = Color.clear;
            }
            if (resultVersusLabel != null)
            {
                resultVersusLabel.color = Color.clear;
                resultVersusLabel.rectTransform.localScale =
                    Vector3.one * 0.72f;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float choiceFade = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.10f, 0.38f, progress));
                float clashBlend = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.24f, 0.66f, progress));
                float iconRise = EaseOutBack(Mathf.InverseLerp(
                    0.28f,
                    0.88f,
                    progress));
                float resultReveal = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.34f, 0.72f, progress));
                SetPreludePanelOpacity(choicePanelGroup, 1f - choiceFade);
                SetPreludePanelOpacity(resultPanelGroup, resultReveal);
                if (choicePanel != null)
                {
                    choicePanel.GetComponent<RectTransform>().localScale =
                        Vector3.one * Mathf.Lerp(1f, 0.90f, choiceFade);
                }
                AnimatePreludeResultRevealIcon(
                    resultLocalChoiceIcon,
                    iconRise,
                    -1f);
                AnimatePreludeResultRevealIcon(
                    resultOpponentChoiceIcon,
                    iconRise,
                    1f);
                SetPreludeArenaBlend(clashBlend);
                // Keep the three-pedestal scene at its original framing.
                // The camera only starts moving once the two-pedestal
                // confrontation art has taken over the screen.
                float clashFocus = Mathf.InverseLerp(
                    0.66f,
                    1f,
                    clashBlend);
                SetPreludeBackdropFocus(clashFocus * 0.28f);
                if (preludeCinematicVeil != null)
                {
                    Color veil = new Color(0.018f, 0.08f, 0.20f, 1f);
                    veil.a = Mathf.Sin(progress * Mathf.PI) * 0.38f;
                    preludeCinematicVeil.color = veil;
                }
                if (preludeCinematicRay != null)
                {
                    Color ray = new Color(0.10f, 0.47f, 0.78f, 1f);
                    ray.a = Mathf.Sin(progress * Mathf.PI) * 0.48f;
                    preludeCinematicRay.color = ray;
                    preludeCinematicRay.rectTransform.localScale =
                        new Vector3(
                            Mathf.Lerp(0.38f, 1.20f, clashBlend),
                            Mathf.Lerp(0.38f, 1.20f, clashBlend),
                            1f);
                }
                if (resultVersusLabel != null)
                {
                    float titleReveal = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.34f, 0.56f, progress));
                    Color title = new Color(0.79f, 0.96f, 1f,
                        titleReveal);
                    resultVersusLabel.color = title;
                    float scale = Mathf.Lerp(0.72f, 1.12f,
                        EaseOutBack(titleReveal));
                    resultVersusLabel.rectTransform.localScale =
                        Vector3.one * scale;
                }
                yield return null;
            }

            if (choicePanel != null)
            {
                choicePanel.SetActive(false);
                choicePanel.GetComponent<RectTransform>().localScale =
                    Vector3.one;
            }
            SetPreludePanelOpacity(choicePanelGroup, 1f);
            SetPreludePanelOpacity(resultPanelGroup, 1f);
            ResetPreludeResultIcon(resultLocalChoiceIcon);
            ResetPreludeResultIcon(resultOpponentChoiceIcon);
            ResetPreludeCinematicVeil();
        }

        private static void AnimatePreludeResultRevealIcon(
            Image icon,
            float progress,
            float horizontalDirection)
        {
            if (icon == null)
                return;
            icon.rectTransform.localScale = Vector3.one * Mathf.Lerp(
                0.43f,
                1f,
                progress);
            icon.rectTransform.anchoredPosition = new Vector2(
                horizontalDirection * Mathf.Lerp(64f, 0f, progress),
                Mathf.Lerp(-158f, 0f, progress));
        }

        private void ResetPreludeCinematicVeil()
        {
            if (preludeCinematicRay != null)
            {
                preludeCinematicRay.color = Color.clear;
                preludeCinematicRay.rectTransform.localScale = Vector3.one;
                preludeCinematicRay.gameObject.SetActive(false);
            }
            if (preludeCinematicVeil != null)
            {
                preludeCinematicVeil.color = Color.clear;
                preludeCinematicVeil.gameObject.SetActive(false);
            }
        }

        private void SetPreludeBackdropVisible(bool visible)
        {
            if (preludeBackdrop != null)
            {
                preludeBackdrop.SetActive(visible);
                if (!visible)
                    ResetPreludeBackdropFraming();
            }

            bool showCards = !visible;
            foreach (Image card in floatingCardImages)
            {
                if (card != null)
                    card.enabled = showCards;
            }
            foreach (Image trail in floatingCardTrailImages)
            {
                if (trail != null)
                    trail.enabled = showCards;
            }
        }

        private void SetPreludeBackdropFocus(float amount)
        {
            if (preludeBackdrop == null)
                return;
            RectTransform backdrop = preludeBackdrop
                .GetComponent<RectTransform>();
            if (backdrop == null)
                return;
            float focus = Mathf.Clamp01(amount);
            float scale = Mathf.Lerp(1f, 1.115f, focus);
            backdrop.localScale = new Vector3(scale, scale, 1f);
            backdrop.anchoredPosition = new Vector2(
                0f,
                Mathf.Lerp(0f, -32f, focus));
        }

        private void SetPreludeArenaBlend(float clashBlend)
        {
            float blend = Mathf.Clamp01(clashBlend);
            if (preludeClashArenaGroup == null ||
                preludeClashArenaImage == null)
            {
                blend = 0f;
            }
            else if (preludeChoiceArenaGroup == null ||
                     preludeChoiceArenaImage == null)
            {
                blend = 1f;
            }
            if (preludeChoiceArenaGroup != null)
                preludeChoiceArenaGroup.alpha = 1f - blend;
            if (preludeClashArenaGroup != null)
                preludeClashArenaGroup.alpha = blend;
            if (preludeChoiceArenaImage != null)
            {
                float scale = Mathf.Lerp(1f, 0.965f, blend);
                preludeChoiceArenaImage.rectTransform.localScale =
                    new Vector3(scale, scale, 1f);
            }
            if (preludeClashArenaImage != null)
            {
                float scale = Mathf.Lerp(1.085f, 1f, blend);
                preludeClashArenaImage.rectTransform.localScale =
                    new Vector3(scale, scale, 1f);
            }
        }

        private void ResetPreludeBackdropFraming()
        {
            if (preludeBackdrop == null)
                return;
            RectTransform backdrop = preludeBackdrop
                .GetComponent<RectTransform>();
            if (backdrop == null)
                return;
            backdrop.localScale = Vector3.one;
            backdrop.anchoredPosition = Vector2.zero;
            SetPreludeArenaBlend(0f);
        }

        private void ApplyText(string primary, string secondary)
        {
            primaryLabel.text = string.IsNullOrWhiteSpace(primary)
                ? "Carregando..."
                : primary;
            secondaryLabel.text = secondary ?? string.Empty;
            secondaryLabel.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(secondaryLabel.text));
        }

        private void Update()
        {
            if (!IsVisible || group == null)
                return;

            ApplySafeArea();
            float delta = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            if (spinner != null && spinner.gameObject.activeSelf)
                spinner.Rotate(0f, 0f, -150f * delta);
            float burstElapsed = Mathf.Max(0f, now - cardBurstStartedAt);
            UpdateBurstEnergyField(burstElapsed);
            UpdateBackdropColorGrade(burstElapsed, now);
            UpdatePreludeMist(now);
            UpdateTypographyReveal(burstElapsed);
            for (int index = 0; index < lightStreaks.Count; index++)
            {
                RectTransform streak = lightStreaks[index];
                if (streak == null)
                    continue;
                float reveal = EaseOutCubic(Mathf.Clamp01(
                    (burstElapsed - index * 0.009f) / 0.42f));
                float pulse = 0.72f + 0.28f * Mathf.Sin(
                    now * (1.15f + index * 0.025f) + index * 0.71f);
                float rayFade = Mathf.Lerp(
                    1f,
                    0.24f,
                    Mathf.SmoothStep(0.52f, 1.65f, burstElapsed));
                streak.localScale = new Vector3(
                    reveal * pulse,
                    Mathf.Lerp(0.35f, 1f, reveal),
                    1f);
                if (index < lightStreakImages.Count &&
                    index < lightStreakBaseColors.Count)
                {
                    Color streakColor = lightStreakBaseColors[index];
                    streakColor.a *= reveal * rayFade *
                                     Mathf.Lerp(0.72f, 1f, pulse);
                    lightStreakImages[index].color = streakColor;
                }
                if (index < lightStreakAngles.Count)
                {
                    float directionalSpin = motionStyle switch
                    {
                        LoadingCardMotionStyle.ShopSpiral =>
                            burstElapsed * 8.5f,
                        LoadingCardMotionStyle.MultiplayerCrossflow =>
                            (index % 2 == 0 ? 1f : -1f) *
                            burstElapsed * 1.8f,
                        _ => 0f
                    };
                    streak.localEulerAngles = new Vector3(
                        0f,
                        0f,
                        lightStreakAngles[index] +
                        directionalSpin +
                        Mathf.Sin(now * 0.34f + index) * 1.6f);
                }
            }
            for (int index = 0; index < floatingCards.Count; index++)
            {
                RectTransform card = floatingCards[index];
                if (card == null)
                    continue;
                Image cardImage = index < floatingCardImages.Count
                    ? floatingCardImages[index]
                    : null;
                float depth = floatingCardDepths[index];
                float phase = index * 0.83f;
                Vector2 start = floatingCardStarts[index];
                Vector2 origin = floatingCardOrigins[index];
                Vector2 outward = origin.sqrMagnitude > 0.01f
                    ? origin.normalized
                    : Vector2.up;
                Vector2 tangent = new Vector2(-outward.y, outward.x);
                float localTime = burstElapsed - floatingCardDelays[index];
                float burstDuration = floatingCardDurations[index];
                float targetScale = floatingCardScales[index];

                if (localTime < 0f)
                {
                    card.anchoredPosition = start;
                    card.localScale = Vector3.zero;
                    ApplyFloatingCardAlpha(cardImage, index, 0f);
                    ResetFloatingCardTrail(index, start);
                    continue;
                }

                float normalized = Mathf.Clamp01(localTime / burstDuration);
                if (normalized < 1f)
                {
                    float eased = EaseOutQuart(normalized);
                    Vector2 secondControl = Vector2.Lerp(
                        floatingCardControls[index],
                        origin,
                        0.72f);
                    Vector2 position = CubicBezier(
                        start,
                        floatingCardControls[index],
                        secondControl,
                        origin,
                        eased);
                    float gustEnvelope = Mathf.Pow(
                        Mathf.Sin(normalized * Mathf.PI),
                        1.18f);
                    float gust = gustEnvelope * Mathf.Lerp(14f, 46f, depth);
                    card.anchoredPosition = position + tangent * gust;

                    float scaleReveal = EaseOutBack(Mathf.Clamp01(
                        normalized / 0.88f));
                    float arrival = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01((normalized - 0.72f) / 0.28f));
                    float arrivalSettle = 1f +
                        Mathf.Sin(arrival * Mathf.PI) * 0.035f;
                    float scale = Mathf.Max(
                        0.02f,
                        scaleReveal * targetScale * arrivalSettle);
                    card.localScale = new Vector3(scale, scale, 1f);
                    card.localEulerAngles = new Vector3(
                        0f,
                        Mathf.Sin(normalized * Mathf.PI) * 24f * depth,
                        floatingCardAngles[index] +
                        floatingCardSpins[index] * (1f - eased));
                    ApplyFloatingCardAlpha(
                        cardImage,
                        index,
                        Mathf.SmoothStep(
                            0f,
                            1f,
                            Mathf.Clamp01(normalized / 0.16f)) *
                        Mathf.Lerp(0.74f, 1f, depth));
                    UpdateFloatingCardTrail(
                        index,
                        card,
                        Mathf.Sin(normalized * Mathf.PI) * 0.24f,
                        8.5f);
                    continue;
                }

                float settledTime = localTime - burstDuration;
                float radialTravel = Mathf.Sin(
                    settledTime * Mathf.Lerp(0.48f, 0.84f, depth) +
                    phase) * Mathf.Lerp(16f, 44f, depth);
                float tangentTravel = Mathf.Cos(
                    settledTime * Mathf.Lerp(0.35f, 0.62f, depth) +
                    phase * 1.31f) * Mathf.Lerp(10f, 32f, depth);
                float vertical = Mathf.Sin(
                    settledTime * 0.74f + phase * 1.7f) *
                    Mathf.Lerp(8f, 24f, depth);
                card.anchoredPosition = origin + outward * radialTravel +
                                        tangent * tangentTravel +
                                        Vector2.up * vertical;
                float breathing = 1f + Mathf.Sin(
                    settledTime * 0.92f + phase) * 0.035f;
                card.localScale = Vector3.one * targetScale * breathing;
                card.localEulerAngles = new Vector3(
                    0f,
                    Mathf.Sin(settledTime * 0.78f + phase) * 13f * depth,
                    floatingCardAngles[index] +
                    Mathf.Sin(settledTime * 0.58f + phase) * 7f);
                ApplyFloatingCardAlpha(
                    cardImage,
                    index,
                    Mathf.Lerp(0.74f, 1f, depth));
                UpdateFloatingCardTrail(index, card, 0.045f, 13f);
            }

            if (hideRequested &&
                Time.realtimeSinceStartup - shownAt >= minimumVisibleSeconds)
            {
                targetAlpha = 0f;
                hideRequested = false;
            }

            float duration = targetAlpha > group.alpha
                ? FadeInSeconds
                : FadeOutSeconds;
            group.alpha = Mathf.MoveTowards(
                group.alpha,
                targetAlpha,
                delta / Mathf.Max(0.01f, duration));
            if (targetAlpha <= 0f && group.alpha <= 0.001f)
                HideImmediately();
        }

        private void EnsureView()
        {
            if (canvas != null)
                return;

            Font font = MasterDuelTypography.Resolve(FontStyle.Normal, 17);
            GameObject canvasObject = new GameObject(
                "OnlineTransitionCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32760;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasObject.GetComponent<CanvasGroup>();

            Image black = CreateImage(
                canvasObject.transform,
                "BlackBlocker",
                Color.black,
                Vector2.zero,
                Vector2.one);
            black.raycastTarget = true;
            transitionVoidLayer = CreateImage(
                black.transform,
                "Abismo Violeta",
                new Color(0.018f, 0.006f, 0.055f, 1f),
                Vector2.zero,
                Vector2.one);
            transitionVoidLayer.raycastTarget = false;
            BuildWarpBackdrop(transitionVoidLayer.transform);
            BuildPreludeBackdrop(black.transform);

            GameObject safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(black.transform, false);
            safeAreaPanel = safe.GetComponent<RectTransform>();
            Stretch(safeAreaPanel, Vector2.zero, Vector2.one);

            spinnerImage = CreateImage(
                safe.transform,
                "Spinner",
                new Color(0.29f, 0.91f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            spinner = spinnerImage.rectTransform;
            spinner.sizeDelta = new Vector2(76f, 7f);
            spinner.anchoredPosition = new Vector2(0f, 72f);

            primaryLabel = CreateText(
                safe.transform,
                "PrimaryLabel",
                font,
                38,
                FontStyle.Bold,
                new Vector2(0.12f, 0.40f),
                new Vector2(0.88f, 0.49f));
            primaryLabel.text = "Carregando duelo...";
            secondaryLabel = CreateText(
                safe.transform,
                "SecondaryLabel",
                font,
                20,
                FontStyle.Normal,
                new Vector2(0.12f, 0.33f),
                new Vector2(0.88f, 0.41f));
            secondaryLabel.color = new Color(0.72f, 0.82f, 0.92f, 1f);

            BuildProgress(safe.transform, font);
            BuildChoicePanel(safe.transform, font);
            BuildResultPanel(safe.transform, font);
            BuildStartingPlayerPanel(safe.transform, font);
            backButton = CreateButton(
                safe.transform,
                "ReturnButton",
                "VOLTAR AO MENU",
                font,
                new Vector2(0.34f, 0.18f),
                new Vector2(0.66f, 0.26f),
                new Color(0.15f, 0.78f, 0.92f, 1f),
                new Color(0.01f, 0.06f, 0.09f, 1f));
            backButton.onClick.AddListener(() => backAction?.Invoke());
            backButton.gameObject.SetActive(false);
            BuildPreludeCinematicOverlay(safe.transform);
            group.alpha = 0f;
            canvasObject.SetActive(false);
        }

        private void BuildPreludeCinematicOverlay(Transform parent)
        {
            preludeCinematicVeil = CreateImage(
                parent,
                "Fenda entre a decisão e o duelo",
                Color.clear,
                Vector2.zero,
                Vector2.one);
            preludeCinematicVeil.raycastTarget = false;
            preludeCinematicRay = CreateImage(
                preludeCinematicVeil.transform,
                "Névoa do confronto",
                Color.clear,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            preludeCinematicRay.raycastTarget = false;
            preludeCinematicRay.sprite = CreateProceduralBurstSprite(false);
            preludeCinematicRay.preserveAspect = true;
            preludeCinematicRay.rectTransform.sizeDelta =
                new Vector2(920f, 920f);
            preludeCinematicVeil.gameObject.SetActive(false);
        }

        private void BuildPreludeBackdrop(Transform parent)
        {
            Image stage = CreateImage(
                parent,
                "Arena da decisão",
                Color.clear,
                Vector2.zero,
                Vector2.one);
            stage.raycastTarget = false;
            preludeBackdrop = stage.gameObject;

            preludeChoiceArenaImage = CreatePreludeArenaArtwork(
                stage.transform,
                "Arena dos três selos",
                "PreludeArena/decision_pedestal_arena");
            if (preludeChoiceArenaImage != null)
            {
                preludeChoiceArenaGroup = preludeChoiceArenaImage.gameObject
                    .AddComponent<CanvasGroup>();
            }

            preludeClashArenaImage = CreatePreludeArenaArtwork(
                stage.transform,
                "Arena do confronto",
                "PreludeArena/decision_clash_arena");
            if (preludeClashArenaImage != null)
            {
                preludeClashArenaGroup = preludeClashArenaImage.gameObject
                    .AddComponent<CanvasGroup>();
            }
            SetPreludeArenaBlend(0f);

            Image grading = CreateImage(
                stage.transform,
                "Véu da arena de decisão",
                new Color(0.001f, 0.007f, 0.022f, 0.30f),
                Vector2.zero,
                Vector2.one);
            grading.raycastTarget = false;

            Texture2D mistTexture = Resources.Load<Texture2D>(
                "PreludeArena/decision_mist_layer");
            if (mistTexture == null)
            {
                Debug.LogError(
                    "A névoa da arena de decisão não foi encontrada em " +
                    "Resources/PreludeArena/decision_mist_layer.");
            }
            else
            {
                Sprite mistSprite = Sprite.Create(
                    mistTexture,
                    new Rect(0f, 0f, mistTexture.width, mistTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                Vector2[] origins =
                {
                    new Vector2(-170f, -240f),
                    new Vector2(210f, -92f),
                    new Vector2(-95f, 92f)
                };
                Vector2[] sizes =
                {
                    new Vector2(1720f, 573f),
                    new Vector2(1940f, 647f),
                    new Vector2(1480f, 493f)
                };
                Color[] colors =
                {
                    new Color(0.48f, 0.78f, 1f, 0.18f),
                    new Color(0.42f, 0.56f, 0.92f, 0.13f),
                    new Color(0.70f, 0.50f, 1f, 0.09f)
                };
                float[] speeds = { 0.09f, -0.065f, 0.045f };
                for (int index = 0; index < origins.Length; index++)
                {
                    Image mist = CreateImage(
                        stage.transform,
                        $"Névoa móvel {index + 1}",
                        colors[index],
                        new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f));
                    mist.sprite = mistSprite;
                    mist.preserveAspect = true;
                    mist.rectTransform.sizeDelta = sizes[index];
                    mist.rectTransform.anchoredPosition = origins[index];
                    preludeMistLayers.Add(mist.rectTransform);
                    preludeMistImages.Add(mist);
                    preludeMistOrigins.Add(origins[index]);
                    preludeMistSpeeds.Add(speeds[index]);
                    preludeMistPhases.Add(index * 1.79f);
                    preludeMistBaseColors.Add(colors[index]);
                }
            }
            preludeBackdrop.SetActive(false);
        }

        private static Image CreatePreludeArenaArtwork(
            Transform parent,
            string objectName,
            string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                Debug.LogError(
                    "A arte da arena de decisão não foi encontrada em " +
                    "Resources/" + resourcePath + ".");
                return null;
            }

            Image image = CreateImage(
                parent,
                objectName,
                Color.white,
                Vector2.zero,
                Vector2.one);
            image.sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            AspectRatioFitter fit = image.gameObject
                .AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = texture.width / (float)texture.height;
            return image;
        }

        private void UpdatePreludeMist(float now)
        {
            if (preludeBackdrop == null || !preludeBackdrop.activeInHierarchy)
                return;
            for (int index = 0; index < preludeMistLayers.Count; index++)
            {
                RectTransform mist = preludeMistLayers[index];
                Image mistImage = index < preludeMistImages.Count
                    ? preludeMistImages[index]
                    : null;
                if (mist == null || mistImage == null)
                    continue;
                float speed = index < preludeMistSpeeds.Count
                    ? preludeMistSpeeds[index]
                    : 0.05f;
                float phase = index < preludeMistPhases.Count
                    ? preludeMistPhases[index]
                    : index;
                Vector2 origin = index < preludeMistOrigins.Count
                    ? preludeMistOrigins[index]
                    : Vector2.zero;
                float horizontal = Mathf.Sin(now * speed + phase) * 180f;
                float vertical = Mathf.Sin(now * (Mathf.Abs(speed) * 1.7f) +
                                           phase * 1.37f) * 16f;
                mist.anchoredPosition = origin +
                                        new Vector2(horizontal, vertical);
                float pulse = 0.82f + 0.18f * Mathf.Sin(
                    now * 0.42f + phase);
                mist.localScale = Vector3.one *
                                  (0.98f + 0.025f * pulse);
                Color color = index < preludeMistBaseColors.Count
                    ? preludeMistBaseColors[index]
                    : Color.white;
                color.a *= pulse;
                mistImage.color = color;
            }
        }

        private void BuildWarpBackdrop(Transform parent)
        {
            Sprite glowSprite = CreateProceduralBurstSprite(false);
            Sprite ringSprite = CreateProceduralBurstSprite(true);
            burstGlowOuter = CreateImage(
                parent,
                "Brilho Externo da Rajada",
                new Color(0.12f, 0.76f, 1f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            burstGlowOuter.sprite = glowSprite;
            burstGlowOuter.raycastTarget = false;
            burstGlowOuter.rectTransform.sizeDelta = new Vector2(620f, 620f);
            burstGlowInner = CreateImage(
                parent,
                "Núcleo da Rajada",
                new Color(0.50f, 0.18f, 0.98f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            burstGlowInner.sprite = glowSprite;
            burstGlowInner.raycastTarget = false;
            burstGlowInner.rectTransform.sizeDelta = new Vector2(250f, 250f);
            Vector2[] perimeterAnchors =
            {
                new Vector2(0.08f, 0.12f),
                new Vector2(0.50f, 0.06f),
                new Vector2(0.92f, 0.12f),
                new Vector2(0.035f, 0.52f),
                new Vector2(0.965f, 0.52f)
            };
            Vector2[] perimeterSizes =
            {
                new Vector2(460f, 460f),
                new Vector2(620f, 430f),
                new Vector2(460f, 460f),
                new Vector2(360f, 580f),
                new Vector2(360f, 580f)
            };
            for (int index = 0; index < perimeterAnchors.Length; index++)
            {
                Image glow = CreateImage(
                    parent,
                    $"Brilho Periférico {index + 1}",
                    Color.clear,
                    perimeterAnchors[index],
                    perimeterAnchors[index]);
                glow.sprite = glowSprite;
                glow.raycastTarget = false;
                glow.rectTransform.sizeDelta = perimeterSizes[index];
                perimeterGlows.Add(glow);
                perimeterGlowPhases.Add(index * 1.31f);
            }
            for (int index = 0; index < 3; index++)
            {
                Image ring = CreateImage(
                    parent,
                    $"Onda de Vento {index + 1}",
                    Color.clear,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                ring.sprite = ringSprite;
                ring.raycastTarget = false;
                ring.rectTransform.sizeDelta = new Vector2(430f, 430f);
                burstRings.Add(ring);
            }

            burstFlashHorizontal = CreateImage(
                parent,
                "Clarão Horizontal",
                Color.clear,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            burstFlashHorizontal.sprite = glowSprite;
            burstFlashHorizontal.raycastTarget = false;
            burstFlashHorizontal.rectTransform.sizeDelta =
                new Vector2(1540f, 22f);
            burstFlashHorizontal.rectTransform.localScale =
                new Vector3(0.06f, 1f, 1f);
            burstFlashVertical = CreateImage(
                parent,
                "Clarão Vertical",
                Color.clear,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            burstFlashVertical.sprite = glowSprite;
            burstFlashVertical.raycastTarget = false;
            burstFlashVertical.rectTransform.sizeDelta =
                new Vector2(14f, 660f);
            burstFlashVertical.rectTransform.localScale =
                new Vector3(1f, 0.11f, 1f);

            const int sparkCount = 22;
            for (int index = 0; index < sparkCount; index++)
            {
                float size = Mathf.Lerp(
                    4f,
                    11f,
                    (float)visualRandom.NextDouble());
                Image spark = CreateImage(
                    parent,
                    $"Partícula Arcana {index + 1}",
                    Color.clear,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                spark.raycastTarget = false;
                spark.rectTransform.sizeDelta = new Vector2(size, size);
                spark.rectTransform.localEulerAngles =
                    new Vector3(0f, 0f, 45f);
                burstSparks.Add(spark.rectTransform);
                burstSparkImages.Add(spark);
                burstSparkDirections.Add(Vector2.up);
                burstSparkSpeeds.Add(520f);
                burstSparkDelays.Add(0f);
                burstSparkSizes.Add(size);
                burstSparkSpins.Add(180f);
            }

            for (int index = 0; index < 26; index++)
            {
                Color color = index % 3 == 0
                    ? new Color(0.12f, 0.76f, 1f, 0.18f)
                    : new Color(0.50f, 0.18f, 0.98f, 0.16f);
                Image streak = CreateImage(
                    parent,
                    $"Rastro {index + 1}",
                    color,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                RectTransform rect = streak.rectTransform;
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(1100f + index * 19f, 2f + index % 4);
                rect.localEulerAngles = new Vector3(0f, 0f, index * (360f / 26f));
                lightStreaks.Add(rect);
                lightStreakImages.Add(streak);
                lightStreakBaseColors.Add(color);
                lightStreakAngles.Add(index * (360f / 26f));
            }

            const int cardCount = 16;
            for (int index = 0; index < cardCount; index++)
            {
                Image card = CreateImage(
                    parent,
                    $"Carta no Vórtice {index + 1}",
                    new Color(0.07f, 0.12f, 0.22f, 0.66f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                RectTransform rect = card.rectTransform;
                float sector = 360f / cardCount;
                float jitter = Mathf.Lerp(
                    -8f,
                    8f,
                    (float)visualRandom.NextDouble());
                float angle = index * sector + 10f + jitter;
                float radius = Mathf.Lerp(
                    315f,
                    790f,
                    (float)visualRandom.NextDouble());
                Vector2 origin = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius * 0.60f);
                float depth = Mathf.Lerp(
                    0.22f,
                    1f,
                    (float)visualRandom.NextDouble());
                float height = Mathf.Lerp(104f, 230f, depth);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(height * 0.686f, height);
                float rotation = -angle + 90f +
                                 Mathf.Lerp(
                                     -18f,
                                     18f,
                                     (float)visualRandom.NextDouble());
                rect.localEulerAngles = new Vector3(0f, 0f, rotation);
                rect.localScale = Vector3.zero;
                card.preserveAspect = true;
                card.raycastTarget = false;
                Shadow shadow = card.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0.02f, 0.68f);
                shadow.effectDistance = new Vector2(9f, -9f);
                shadow.useGraphicAlpha = true;
                floatingCards.Add(rect);
                floatingCardImages.Add(card);
                floatingCardStarts.Add(Vector2.zero);
                floatingCardOrigins.Add(origin);
                floatingCardDepths.Add(depth);
                floatingCardAngles.Add(rotation);
                Vector2 direction = origin.normalized;
                Vector2 tangent = new Vector2(-direction.y, direction.x);
                float curl = Mathf.Lerp(
                    150f,
                    370f,
                    (float)visualRandom.NextDouble()) *
                    (index % 2 == 0 ? 1f : -1f);
                floatingCardControls.Add(origin * 0.34f + tangent * curl);
                floatingCardDelays.Add(
                    0.08f + (index % 8) * 0.045f +
                    (index / 8) * 0.028f);
                floatingCardDurations.Add(
                    Mathf.Lerp(
                        0.68f,
                        0.94f,
                        (float)visualRandom.NextDouble()));
                floatingCardScales.Add(Mathf.Lerp(0.82f, 1.08f, depth));
                float spin = Mathf.Lerp(
                    210f,
                    430f,
                    (float)visualRandom.NextDouble());
                floatingCardSpins.Add(index % 2 == 0 ? spin : -spin);
                floatingCardBaseColors.Add(Color.clear);

                Image trail = CreateImage(
                    parent,
                    $"Rastro da Carta {index + 1}",
                    Color.clear,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                trail.transform.SetSiblingIndex(card.transform.GetSiblingIndex());
                trail.preserveAspect = true;
                trail.raycastTarget = false;
                trail.rectTransform.sizeDelta = rect.sizeDelta;
                trail.rectTransform.anchoredPosition = Vector2.zero;
                trail.rectTransform.localScale = Vector3.zero;
                floatingCardTrails.Add(trail.rectTransform);
                floatingCardTrailImages.Add(trail);
            }

            RefreshFloatingCardArtwork();
        }

        private void BuildProgress(Transform parent, Font font)
        {
            Image container = CreateImage(
                parent,
                "Carregamento do Duelo",
                new Color(0.015f, 0.035f, 0.075f, 0.90f),
                new Vector2(0.62f, 0.085f),
                new Vector2(0.955f, 0.155f));
            progressRoot = container.rectTransform;
            Image track = CreateImage(
                container.transform,
                "Barra de Carregamento",
                new Color(0.10f, 0.16f, 0.27f, 0.92f),
                new Vector2(0.055f, 0.35f),
                new Vector2(0.82f, 0.65f));
            progressFill = CreateImage(
                track.transform,
                "Progresso",
                new Color(0.18f, 0.86f, 1f, 1f),
                Vector2.zero,
                new Vector2(0f, 1f));
            progressLabel = CreateText(
                container.transform,
                "Porcentagem",
                font,
                18,
                FontStyle.Bold,
                new Vector2(0.835f, 0.08f),
                new Vector2(0.98f, 0.92f));
            progressLabel.alignment = TextAnchor.MiddleCenter;
            SetProgress(0f);
        }

        private void RefreshFloatingCardArtwork()
        {
            if (floatingCards.Count == 0)
                return;

            // The presenter survives scene changes, but dynamically imported
            // card sprites do not necessarily do so. Rebuild this small visual
            // sample on every transition instead of retaining destroyed Sprite
            // references, which Unity renders as plain white rectangles.
            ReleaseFloatingCardArtwork();
            CardCatalog[] catalogs =
                Resources.FindObjectsOfTypeAll<CardCatalog>();
            foreach (CardCatalog catalog in catalogs)
            {
                if (catalog == null)
                    continue;
                CardCatalogEntry[] candidates = catalog.Entries
                    .Where(entry => entry != null &&
                                    entry.IsCollectible &&
                                    entry.HasArtwork)
                    .OrderBy(entry => entry.OfficialCardId,
                        StringComparer.Ordinal)
                    .ToArray();
                int stride = Mathf.Max(1, candidates.Length / 48);
                for (int offset = 0;
                     offset < stride && cachedCardArtwork.Count < 48;
                     offset++)
                {
                    for (int index = 0;
                         index + offset < candidates.Length &&
                         cachedCardArtwork.Count < 48;
                         index += stride)
                    {
                        CardCatalogEntry candidate = candidates[index + offset];
                        Sprite artwork = candidate.AuthoredArtwork;
                        if (artwork == null)
                        {
                            artwork = RuntimeCardArtworkCache.Acquire(
                                candidate.OfficialCardId);
                            if (artwork != null)
                            {
                                pinnedCardArtworkIds.Add(
                                    candidate.OfficialCardId);
                            }
                        }
                        if (artwork != null && artwork.texture != null &&
                            !cachedCardArtwork.Contains(artwork))
                        {
                            cachedCardArtwork.Add(artwork);
                        }
                    }
                }
                if (cachedCardArtwork.Count >= 48)
                    break;
            }

            for (int index = 0; index < floatingCards.Count; index++)
            {
                Image image = index < floatingCardImages.Count
                    ? floatingCardImages[index]
                    : null;
                if (image == null)
                    continue;
                Image trailImage = index < floatingCardTrailImages.Count
                    ? floatingCardTrailImages[index]
                    : null;
                bool hasArtwork = cachedCardArtwork.Count > 0;
                image.enabled = hasArtwork;
                if (trailImage != null)
                    trailImage.enabled = hasArtwork;
                if (!hasArtwork)
                {
                    image.sprite = null;
                    if (trailImage != null)
                        trailImage.sprite = null;
                    continue;
                }

                Sprite selectedArtwork = cachedCardArtwork[
                    visualRandom.Next(cachedCardArtwork.Count)];
                image.sprite = selectedArtwork;
                if (trailImage != null)
                    trailImage.sprite = selectedArtwork;
                float depth = floatingCardDepths[index];
                Color baseColor = new Color(
                    0.76f + depth * 0.24f,
                    0.82f + depth * 0.18f,
                    1f,
                    Mathf.Lerp(0.34f, 0.86f, depth));
                image.color = baseColor;
                if (index < floatingCardBaseColors.Count)
                    floatingCardBaseColors[index] = baseColor;
                if (trailImage != null)
                {
                    Color trailColor = baseColor;
                    trailColor.a = 0f;
                    trailImage.color = trailColor;
                }
            }
        }

        private void ReleaseFloatingCardArtwork()
        {
            foreach (string officialCardId in pinnedCardArtworkIds)
                RuntimeCardArtworkCache.Release(officialCardId);
            pinnedCardArtworkIds.Clear();
            cachedCardArtwork.Clear();
        }

        private void RestartFloatingCardBurst()
        {
            ConfigureFloatingCardMotion();
            cardBurstStartedAt = Time.unscaledTime;
            if (burstFlashHorizontal != null)
                burstFlashHorizontal.color = Color.clear;
            if (burstFlashVertical != null)
                burstFlashVertical.color = Color.clear;
            for (int index = 0; index < burstSparks.Count; index++)
            {
                RectTransform spark = burstSparks[index];
                if (spark != null)
                {
                    spark.anchoredPosition = Vector2.zero;
                    spark.localScale = Vector3.zero;
                }
                if (index < burstSparkImages.Count)
                    burstSparkImages[index].color = Color.clear;
            }
            for (int index = 0; index < lightStreaks.Count; index++)
            {
                RectTransform streak = lightStreaks[index];
                if (streak != null)
                    streak.localScale = new Vector3(0f, 0.35f, 1f);
            }

            for (int index = 0; index < floatingCards.Count; index++)
            {
                RectTransform card = floatingCards[index];
                if (card == null)
                    continue;
                card.anchoredPosition = index < floatingCardStarts.Count
                    ? floatingCardStarts[index]
                    : Vector2.zero;
                card.localScale = Vector3.zero;
                ApplyFloatingCardAlpha(
                    index < floatingCardImages.Count
                        ? floatingCardImages[index]
                        : null,
                    index,
                    0f);
            }
        }

        private void ApplyMotionPalette()
        {
            switch (motionStyle)
            {
                case LoadingCardMotionStyle.DuelCharge:
                    motionAccentA = new Color(0.16f, 0.87f, 1f, 1f);
                    motionAccentB = new Color(1f, 0.66f, 0.16f, 1f);
                    break;
                case LoadingCardMotionStyle.MultiplayerCrossflow:
                    motionAccentA = new Color(1f, 0.20f, 0.34f, 1f);
                    motionAccentB = new Color(0.10f, 0.84f, 1f, 1f);
                    break;
                case LoadingCardMotionStyle.DeckFan:
                    motionAccentA = new Color(0.28f, 1f, 0.58f, 1f);
                    motionAccentB = new Color(0.10f, 0.82f, 1f, 1f);
                    break;
                case LoadingCardMotionStyle.ShopSpiral:
                    motionAccentA = new Color(1f, 0.72f, 0.18f, 1f);
                    motionAccentB = new Color(0.67f, 0.28f, 1f, 1f);
                    break;
                default:
                    motionAccentA = new Color(0.12f, 0.76f, 1f, 1f);
                    motionAccentB = new Color(0.50f, 0.18f, 0.98f, 1f);
                    break;
            }

            for (int index = 0; index < lightStreakImages.Count; index++)
            {
                Color color = index % 3 == 0
                    ? motionAccentA
                    : motionAccentB;
                color.a = index % 4 == 0 ? 0.20f : 0.13f;
                lightStreakBaseColors[index] = color;
                lightStreakImages[index].color = color;
            }
        }

        private void UpdateBurstEnergyField(float burstElapsed)
        {
            if (burstGlowOuter != null)
            {
                float normalized = Mathf.Clamp01(burstElapsed / 1.08f);
                float envelope = Mathf.Sin(normalized * Mathf.PI);
                burstGlowOuter.rectTransform.localScale = Vector3.one *
                    Mathf.Lerp(0.10f, 1.62f, EaseOutCubic(normalized));
                Color color = motionAccentA;
                color.a = envelope * 0.25f;
                burstGlowOuter.color = color;
            }
            if (burstGlowInner != null)
            {
                float normalized = Mathf.Clamp01(burstElapsed / 0.78f);
                float envelope = Mathf.Sin(normalized * Mathf.PI);
                float pulse = 1f + Mathf.Sin(
                    burstElapsed * 14f) * 0.08f * envelope;
                burstGlowInner.rectTransform.localScale = Vector3.one *
                    Mathf.Lerp(0.18f, 1.12f, EaseOutBack(normalized)) * pulse;
                Color color = motionAccentB;
                color.a = envelope * 0.42f;
                burstGlowInner.color = color;
            }

            float perimeterReveal = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(burstElapsed / 0.72f));
            float now = Time.unscaledTime;
            for (int index = 0; index < perimeterGlows.Count; index++)
            {
                Image glow = perimeterGlows[index];
                if (glow == null)
                    continue;
                float phase = index < perimeterGlowPhases.Count
                    ? perimeterGlowPhases[index]
                    : index;
                float pulse = 0.5f + 0.5f * Mathf.Sin(
                    now * 0.72f + phase);
                Color color = index % 2 == 0
                    ? motionAccentA
                    : motionAccentB;
                color.a = perimeterReveal * Mathf.Lerp(
                    0.055f,
                    0.115f,
                    pulse);
                glow.color = color;
                float scale = Mathf.Lerp(0.92f, 1.12f, pulse);
                glow.rectTransform.localScale = new Vector3(
                    scale,
                    scale,
                    1f);
            }

            for (int index = 0; index < burstRings.Count; index++)
            {
                Image ring = burstRings[index];
                if (ring == null)
                    continue;
                float localTime = burstElapsed - 0.07f - index * 0.13f;
                float normalized = Mathf.Clamp01(localTime / 0.82f);
                bool active = localTime >= 0f && localTime <= 0.82f;
                ring.rectTransform.localScale = Vector3.one *
                    Mathf.Lerp(0.14f, 2.42f, EaseOutCubic(normalized));
                Color color = index % 2 == 0
                    ? motionAccentA
                    : motionAccentB;
                color.a = active
                    ? Mathf.Sin(normalized * Mathf.PI) * 0.32f
                    : 0f;
                ring.color = color;
            }

            UpdateBurstLensFlash(burstElapsed);
            UpdateBurstSparks(burstElapsed);
        }

        private void UpdateBurstLensFlash(float burstElapsed)
        {
            float localTime = burstElapsed - 0.035f;
            float normalized = Mathf.Clamp01(localTime / 0.56f);
            bool active = localTime >= 0f && localTime <= 0.56f;
            float envelope = active
                ? Mathf.Pow(Mathf.Sin(normalized * Mathf.PI), 1.35f)
                : 0f;
            float expansion = EaseOutCubic(normalized);

            if (burstFlashHorizontal != null)
            {
                RectTransform rect = burstFlashHorizontal.rectTransform;
                rect.localScale = new Vector3(
                    Mathf.Lerp(0.06f, 1f, expansion),
                    Mathf.Lerp(1f, 0.23f, expansion),
                    1f);
                Color color = Color.Lerp(motionAccentA, Color.white, 0.34f);
                color.a = envelope * 0.48f;
                burstFlashHorizontal.color = color;
            }

            if (burstFlashVertical != null)
            {
                RectTransform rect = burstFlashVertical.rectTransform;
                rect.localScale = new Vector3(
                    Mathf.Lerp(1f, 0.22f, expansion),
                    Mathf.Lerp(0.11f, 1f, expansion),
                    1f);
                Color color = Color.Lerp(motionAccentB, Color.white, 0.22f);
                color.a = envelope * 0.25f;
                burstFlashVertical.color = color;
            }
        }

        private void UpdateBackdropColorGrade(float burstElapsed, float now)
        {
            if (transitionVoidLayer == null)
                return;

            Color neutral = new Color(0.012f, 0.006f, 0.038f, 1f);
            Color accent = Color.Lerp(motionAccentA, motionAccentB, 0.42f);
            float intro = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(burstElapsed / 0.72f));
            float breathing = 0.5f + 0.5f * Mathf.Sin(now * 0.43f);
            float influence = Mathf.Lerp(0.028f, 0.064f, intro) +
                              breathing * 0.012f;
            Color graded = Color.Lerp(neutral, accent, influence);
            graded.a = 1f;
            transitionVoidLayer.color = graded;
        }

        private void UpdateTypographyReveal(float burstElapsed)
        {
            float primaryTime = Mathf.Clamp01((burstElapsed - 0.08f) / 0.46f);
            float primaryEase = EaseOutQuart(primaryTime);
            if (primaryLabel != null && primaryLabel.gameObject.activeSelf)
            {
                Color color = Color.white;
                color.a = Mathf.SmoothStep(0f, 1f, primaryTime);
                primaryLabel.color = color;
                float scale = Mathf.Lerp(0.945f, 1f, primaryEase);
                primaryLabel.rectTransform.localScale =
                    new Vector3(scale, scale, 1f);
            }

            float secondaryTime = Mathf.Clamp01((burstElapsed - 0.19f) / 0.50f);
            if (secondaryLabel != null && secondaryLabel.gameObject.activeSelf)
            {
                Color baseColor = Color.Lerp(
                    new Color(0.72f, 0.82f, 0.92f, 1f),
                    motionAccentA,
                    0.10f);
                baseColor.a = Mathf.SmoothStep(0f, 1f, secondaryTime);
                secondaryLabel.color = baseColor;
            }

            if (spinnerImage != null && spinnerImage.gameObject.activeSelf)
            {
                float spinnerTime = Mathf.Clamp01(
                    (burstElapsed - 0.14f) / 0.40f);
                Color color = Color.Lerp(motionAccentA, Color.white, 0.20f);
                color.a = Mathf.SmoothStep(0f, 1f, spinnerTime);
                spinnerImage.color = color;
                float scale = Mathf.Lerp(0.42f, 1f, EaseOutBack(spinnerTime));
                spinner.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void UpdateBurstSparks(float burstElapsed)
        {
            for (int index = 0; index < burstSparks.Count; index++)
            {
                RectTransform spark = burstSparks[index];
                if (spark == null)
                    continue;

                float localTime = burstElapsed - burstSparkDelays[index];
                float duration = Mathf.Lerp(0.62f, 0.94f, index % 5 / 4f);
                float normalized = Mathf.Clamp01(localTime / duration);
                bool active = localTime >= 0f && localTime <= duration;
                Vector2 direction = burstSparkDirections[index];
                Vector2 tangent = new Vector2(-direction.y, direction.x);
                float distance = EaseOutCubic(normalized) *
                                 burstSparkSpeeds[index];
                float curl = Mathf.Sin(normalized * Mathf.PI) *
                             Mathf.Lerp(12f, 46f, index % 4 / 3f) *
                             (index % 2 == 0 ? 1f : -1f);
                spark.anchoredPosition = direction * distance + tangent * curl;

                float sizeEnvelope = Mathf.Sin(normalized * Mathf.PI);
                float scale = Mathf.Lerp(0.30f, 1f, sizeEnvelope);
                spark.localScale = Vector3.one * scale;
                spark.localEulerAngles = new Vector3(
                    0f,
                    0f,
                    45f + burstSparkSpins[index] * normalized);

                if (index < burstSparkImages.Count)
                {
                    Color color = index % 3 == 0
                        ? motionAccentB
                        : motionAccentA;
                    color = Color.Lerp(color, Color.white, 0.18f);
                    color.a = active
                        ? Mathf.Pow(sizeEnvelope, 1.45f) *
                          Mathf.Lerp(0.34f, 0.78f, index % 5 / 4f)
                        : 0f;
                    burstSparkImages[index].color = color;
                }
            }
        }

        private void ResetFloatingCardTrail(int index, Vector2 position)
        {
            if (index >= floatingCardTrails.Count)
                return;
            RectTransform trail = floatingCardTrails[index];
            if (trail == null)
                return;
            trail.anchoredPosition = position;
            trail.localScale = Vector3.zero;
            if (index < floatingCardTrailImages.Count)
            {
                Color color = index < floatingCardBaseColors.Count
                    ? floatingCardBaseColors[index]
                    : Color.white;
                color.a = 0f;
                floatingCardTrailImages[index].color = color;
            }
        }

        private void UpdateFloatingCardTrail(
            int index,
            RectTransform card,
            float alpha,
            float response)
        {
            if (card == null || index >= floatingCardTrails.Count)
                return;
            RectTransform trail = floatingCardTrails[index];
            if (trail == null)
                return;
            float follow = 1f - Mathf.Exp(
                -Mathf.Max(0.01f, Time.unscaledDeltaTime) * response);
            trail.anchoredPosition = Vector2.Lerp(
                trail.anchoredPosition,
                card.anchoredPosition,
                follow);
            Vector3 cardAngles = card.localEulerAngles;
            Vector3 trailAngles = trail.localEulerAngles;
            trail.localEulerAngles = new Vector3(
                0f,
                Mathf.LerpAngle(trailAngles.y, cardAngles.y, follow),
                Mathf.LerpAngle(trailAngles.z, cardAngles.z, follow));
            trail.localScale = Vector3.Lerp(
                trail.localScale,
                card.localScale * 0.94f,
                follow);
            if (index < floatingCardTrailImages.Count)
            {
                Color color = index < floatingCardBaseColors.Count
                    ? floatingCardBaseColors[index]
                    : Color.white;
                color = Color.Lerp(color, motionAccentA, 0.18f);
                color.a *= Mathf.Clamp01(alpha);
                floatingCardTrailImages[index].color = color;
            }
        }

        private void ConfigureFloatingCardMotion()
        {
            ApplyMotionPalette();
            ConfigureBurstSparks();
            int streakCount = lightStreakAngles.Count;
            for (int index = 0; index < streakCount; index++)
            {
                float normalized = streakCount > 1
                    ? index / (float)(streakCount - 1)
                    : 0f;
                lightStreakAngles[index] = motionStyle switch
                {
                    LoadingCardMotionStyle.DuelCharge =>
                        MirroredFanAngle(
                            index,
                            streakCount,
                            28f,
                            152f),
                    LoadingCardMotionStyle.MultiplayerCrossflow =>
                        (index % 4) * 90f +
                        Mathf.Lerp(-18f, 18f, normalized),
                    LoadingCardMotionStyle.DeckFan =>
                        Mathf.Lerp(-162f, 162f, normalized),
                    LoadingCardMotionStyle.ShopSpiral =>
                        index * (360f / Mathf.Max(1, streakCount)) + 14f,
                    _ => index * (360f / Mathf.Max(1, streakCount))
                };
            }

            int count = floatingCards.Count;
            for (int index = 0; index < count; index++)
            {
                float depth = NextVisual(0.28f, 1f);
                float height = Mathf.Lerp(112f, 224f, depth);
                RectTransform card = floatingCards[index];
                if (card != null)
                    card.sizeDelta = new Vector2(height * 0.686f, height);
                if (index < floatingCardTrails.Count &&
                    floatingCardTrails[index] != null)
                {
                    floatingCardTrails[index].sizeDelta =
                        new Vector2(height * 0.686f, height);
                }

                Vector2 start;
                Vector2 end;
                Vector2 control;
                float delay;
                float travelDuration;
                float rotation;
                float spin;
                float targetScale;

                switch (motionStyle)
                {
                    case LoadingCardMotionStyle.DuelCharge:
                    {
                        int lane = index % 8;
                        float laneT = lane / 7f;
                        float side = laneT * 2f - 1f;
                        start = new Vector2(side * 105f, -535f);
                        end = new Vector2(
                            side * NextVisual(420f, 820f),
                            NextVisual(-260f, 430f));
                        control = new Vector2(
                            side * NextVisual(180f, 390f),
                            NextVisual(-90f, 150f));
                        delay = 0.045f + (index / 8) * 0.075f +
                                lane * 0.026f;
                        travelDuration = NextVisual(0.58f, 0.76f);
                        rotation = NextVisual(-13f, 13f);
                        spin = NextVisual(170f, 330f) *
                               (index % 2 == 0 ? 1f : -1f);
                        targetScale = NextVisual(0.82f, 1.08f);
                        break;
                    }
                    case LoadingCardMotionStyle.MultiplayerCrossflow:
                    {
                        bool fromLeft = index % 2 == 0;
                        int band = index / 2;
                        float bandT = band / 7f;
                        float vertical = Mathf.Lerp(-390f, 390f, bandT);
                        float direction = fromLeft ? 1f : -1f;
                        start = new Vector2(-direction * 910f, vertical);
                        end = new Vector2(
                            direction * NextVisual(360f, 760f),
                            -vertical * NextVisual(0.32f, 0.82f));
                        control = new Vector2(
                            direction * NextVisual(-80f, 130f),
                            vertical + direction * NextVisual(180f, 360f));
                        delay = 0.035f + (band % 4) * 0.055f +
                                (fromLeft ? 0f : 0.025f);
                        travelDuration = NextVisual(0.72f, 0.94f);
                        rotation = direction * NextVisual(9f, 24f);
                        spin = direction * NextVisual(260f, 470f);
                        targetScale = NextVisual(0.76f, 1.02f);
                        break;
                    }
                    case LoadingCardMotionStyle.DeckFan:
                    {
                        float fanT = (index + 0.5f) / count;
                        float angle = Mathf.Lerp(-166f, 166f, fanT) +
                                      NextVisual(-5f, 5f);
                        float radians = angle * Mathf.Deg2Rad;
                        float radius = NextVisual(390f, 790f);
                        start = new Vector2(0f, -42f);
                        end = new Vector2(
                            Mathf.Cos(radians) * radius,
                            Mathf.Sin(radians) * radius * 0.60f);
                        Vector2 direction = (end - start).normalized;
                        Vector2 tangent = new Vector2(
                            -direction.y,
                            direction.x);
                        control = Vector2.Lerp(start, end, 0.29f) +
                                  tangent * NextVisual(95f, 250f) *
                                  (index % 2 == 0 ? 1f : -1f);
                        delay = 0.045f + (index % 8) * 0.038f +
                                (index / 8) * 0.024f;
                        travelDuration = NextVisual(0.62f, 0.82f);
                        rotation = -angle + 90f + NextVisual(-12f, 12f);
                        spin = NextVisual(230f, 420f) *
                               (index % 2 == 0 ? 1f : -1f);
                        targetScale = Mathf.Lerp(0.80f, 1.08f, depth);
                        break;
                    }
                    case LoadingCardMotionStyle.ShopSpiral:
                    {
                        float angle = index * (360f / count) +
                                      NextVisual(-7f, 7f);
                        float radians = angle * Mathf.Deg2Rad;
                        float radius = NextVisual(350f, 740f);
                        start = Vector2.zero;
                        end = new Vector2(
                            Mathf.Cos(radians) * radius,
                            Mathf.Sin(radians) * radius * 0.62f);
                        Vector2 direction = end.normalized;
                        Vector2 tangent = new Vector2(
                            -direction.y,
                            direction.x);
                        control = end * 0.18f + tangent *
                                  NextVisual(330f, 560f);
                        delay = 0.035f + index * 0.024f;
                        travelDuration = NextVisual(0.82f, 1.02f);
                        rotation = -angle + 90f + NextVisual(-9f, 9f);
                        spin = NextVisual(430f, 690f);
                        targetScale = Mathf.Lerp(0.74f, 1.02f, depth);
                        break;
                    }
                    default:
                    {
                        float angle = index * (360f / count) +
                                      NextVisual(-8f, 8f);
                        float radians = angle * Mathf.Deg2Rad;
                        float radius = NextVisual(335f, 780f);
                        start = Vector2.zero;
                        end = new Vector2(
                            Mathf.Cos(radians) * radius,
                            Mathf.Sin(radians) * radius * 0.60f);
                        Vector2 direction = end.normalized;
                        Vector2 tangent = new Vector2(
                            -direction.y,
                            direction.x);
                        control = end * 0.34f + tangent *
                                  NextVisual(150f, 370f) *
                                  (index % 2 == 0 ? 1f : -1f);
                        delay = 0.055f + (index % 8) * 0.042f +
                                (index / 8) * 0.026f;
                        travelDuration = NextVisual(0.66f, 0.88f);
                        rotation = -angle + 90f + NextVisual(-16f, 16f);
                        spin = NextVisual(220f, 440f) *
                               (index % 2 == 0 ? 1f : -1f);
                        targetScale = Mathf.Lerp(0.78f, 1.06f, depth);
                        break;
                    }
                }

                floatingCardStarts[index] = start;
                floatingCardOrigins[index] = end;
                floatingCardControls[index] = control;
                floatingCardDelays[index] = delay;
                floatingCardDurations[index] = travelDuration;
                floatingCardAngles[index] = rotation;
                floatingCardSpins[index] = spin;
                floatingCardScales[index] = targetScale;
                floatingCardDepths[index] = depth;
            }
        }

        private static float MirroredFanAngle(
            int index,
            int count,
            float upperMinimum,
            float upperMaximum)
        {
            int lane = index / 2;
            int laneCount = Mathf.Max(1, (count + 1) / 2);
            float laneT = laneCount > 1
                ? lane / (float)(laneCount - 1)
                : 0.5f;
            float upperAngle = Mathf.Lerp(
                upperMinimum,
                upperMaximum,
                laneT);
            return index % 2 == 0
                ? upperAngle
                : upperAngle + 180f;
        }

        private void ConfigureBurstSparks()
        {
            int count = burstSparks.Count;
            for (int index = 0; index < count; index++)
            {
                float normalized = (index + 0.5f) / Mathf.Max(1, count);
                float angle;
                float speed;
                float delay;

                switch (motionStyle)
                {
                    case LoadingCardMotionStyle.DuelCharge:
                        angle = MirroredFanAngle(
                                    index,
                                    count,
                                    34f,
                                    146f) +
                                NextVisual(-5f, 5f);
                        speed = NextVisual(540f, 940f);
                        delay = 0.055f + (index % 7) * 0.018f;
                        break;
                    case LoadingCardMotionStyle.MultiplayerCrossflow:
                    {
                        bool fromLeft = index % 2 == 0;
                        angle = fromLeft
                            ? NextVisual(-24f, 24f)
                            : NextVisual(156f, 204f);
                        speed = NextVisual(620f, 1080f);
                        delay = 0.035f + (index % 6) * 0.022f;
                        break;
                    }
                    case LoadingCardMotionStyle.DeckFan:
                        angle = Mathf.Lerp(-164f, 164f, normalized) +
                                NextVisual(-4f, 4f);
                        speed = NextVisual(520f, 980f);
                        delay = 0.045f + (index % 8) * 0.017f;
                        break;
                    case LoadingCardMotionStyle.ShopSpiral:
                        angle = index * 137.50776f + NextVisual(-6f, 6f);
                        speed = NextVisual(460f, 880f);
                        delay = 0.040f + index * 0.011f;
                        break;
                    default:
                        angle = index * (360f / Mathf.Max(1, count)) +
                                NextVisual(-6f, 6f);
                        speed = NextVisual(500f, 920f);
                        delay = 0.045f + (index % 8) * 0.016f;
                        break;
                }

                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(
                    Mathf.Cos(radians),
                    Mathf.Sin(radians) * 0.68f).normalized;
                burstSparkDirections[index] = direction;
                burstSparkSpeeds[index] = speed;
                burstSparkDelays[index] = delay;
                burstSparkSpins[index] = NextVisual(120f, 420f) *
                    (index % 2 == 0 ? 1f : -1f);

                RectTransform spark = burstSparks[index];
                if (spark != null)
                {
                    float size = burstSparkSizes[index] *
                                 Mathf.Lerp(0.78f, 1.24f, normalized);
                    spark.sizeDelta = new Vector2(size, size);
                }
            }
        }

        private float NextVisual(float minimum, float maximum)
        {
            return Mathf.Lerp(
                minimum,
                maximum,
                (float)visualRandom.NextDouble());
        }

        private void ApplyFloatingCardAlpha(
            Image image,
            int index,
            float multiplier)
        {
            if (image == null)
                return;
            Color baseColor = index < floatingCardBaseColors.Count
                ? floatingCardBaseColors[index]
                : Color.white;
            baseColor.a *= Mathf.Clamp01(multiplier);
            image.color = baseColor;
        }

        private static Vector2 CubicBezier(
            Vector2 start,
            Vector2 firstControl,
            Vector2 secondControl,
            Vector2 end,
            float t)
        {
            float inverse = 1f - t;
            float inverseSquared = inverse * inverse;
            float tSquared = t * t;
            return inverseSquared * inverse * start +
                   3f * inverseSquared * t * firstControl +
                   3f * inverse * tSquared * secondControl +
                   tSquared * t * end;
        }

        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseOutQuart(float value)
        {
            float inverse = 1f - Mathf.Clamp01(value);
            float squared = inverse * inverse;
            return 1f - squared * squared;
        }

        private static float EaseOutBack(float value)
        {
            const float overshoot = 1.70158f;
            float shifted = Mathf.Clamp01(value) - 1f;
            return 1f + (overshoot + 1f) * shifted * shifted * shifted +
                   overshoot * shifted * shifted;
        }

        private static Sprite CreateProceduralBurstSprite(bool ring)
        {
            const int size = 128;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = ring
                    ? "Onda de Vento Procedural"
                    : "Brilho de Rajada Procedural",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        center) / radius;
                    float alpha;
                    if (ring)
                    {
                        float band = (distance - 0.73f) / 0.055f;
                        alpha = Mathf.Exp(-band * band) *
                                Mathf.Clamp01((1f - distance) * 7f);
                    }
                    else
                    {
                        float falloff = 1f - Mathf.SmoothStep(
                            0.02f,
                            1f,
                            distance);
                        alpha = falloff * falloff;
                    }
                    pixels[y * size + x] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void BuildChoicePanel(Transform parent, Font font)
        {
            Image panel = CreateImage(
                parent,
                "Escolha de Símbolo",
                Color.clear,
                Vector2.zero,
                Vector2.one);
            choicePanel = panel.gameObject;
            choicePanelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            Text instruction = CreateText(
                panel.transform,
                "Selecione um símbolo",
                font,
                24,
                FontStyle.Bold,
                new Vector2(0.28f, 0.635f),
                new Vector2(0.72f, 0.685f));
            instruction.text = "ESCOLHA SEU SÍMBOLO";
            instruction.color = new Color(0.66f, 0.93f, 1f, 0.92f);
            DuelPreludeChoice[] choices =
            {
                DuelPreludeChoice.Rock,
                DuelPreludeChoice.Paper,
                DuelPreludeChoice.Scissors
            };
            for (int index = 0; index < choices.Length; index++)
            {
                float center = 0.22f + index * 0.28f;
                DuelPreludeChoice captured = choices[index];
                Color accent = PreludeChoiceAccent(captured);
                Button button = CreateButton(
                    panel.transform,
                    captured.ToString(),
                    string.Empty,
                    font,
                    new Vector2(center - 0.11f, 0.255f),
                    new Vector2(center + 0.11f, 0.575f),
                    new Color(0.005f, 0.012f, 0.028f, 0.012f),
                    Color.white);
                DecoratePreludeButton(button, accent, 16f);
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.gameObject.SetActive(false);
                Image selectionMark = CreateImage(
                    button.transform,
                    "Marca da escolha confirmada",
                    new Color(accent.r, accent.g, accent.b, 0.90f),
                    new Vector2(0.40f, 0.055f),
                    new Vector2(0.60f, 0.078f));
                selectionMark.gameObject.SetActive(false);
                Image icon = CreateImage(
                    button.transform,
                    $"Símbolo {captured}",
                    Color.white,
                    new Vector2(0.08f, 0.20f),
                    new Vector2(0.92f, 0.90f));
                icon.sprite = PreludeChoiceIcon(captured);
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                Shadow iconShadow = icon.gameObject.AddComponent<Shadow>();
                iconShadow.effectColor = new Color(0f, 0f, 0.02f, 0.92f);
                iconShadow.effectDistance = new Vector2(7f, -8f);
                Text choiceCaption = CreateText(
                    button.transform,
                    $"Rótulo {captured}",
                    font,
                    18,
                    FontStyle.Bold,
                    new Vector2(0.08f, 0.075f),
                    new Vector2(0.92f, 0.19f));
                choiceCaption.text = DuelPreludeRules.Label(captured);
                choiceCaption.color = new Color(
                    accent.r,
                    accent.g,
                    accent.b,
                    1f);
                button.onClick.AddListener(() =>
                {
                    foreach (Button item in choiceButtons)
                    {
                        item.interactable = false;
                        SetChoiceButtonSelected(item, item == button);
                    }
                    Action<DuelPreludeChoice> callback = choiceAction;
                    callback?.Invoke(captured);
                });
                choiceButtons.Add(button);
            }
            choicePanel.SetActive(false);
        }

        private static void SetChoiceButtonSelected(
            Button button,
            bool selected)
        {
            if (button == null)
                return;
            Transform mark = button.transform.Find("Marca da escolha confirmada");
            if (mark != null)
                mark.gameObject.SetActive(selected);
            button.transform.localScale = selected
                ? new Vector3(1.06f, 1.06f, 1f)
                : Vector3.one;
        }

        private Sprite PreludeChoiceIcon(DuelPreludeChoice choice)
        {
            if (!preludeChoiceIcons.TryGetValue(choice, out Sprite icon) ||
                icon == null)
            {
                string assetPath = choice switch
                {
                    DuelPreludeChoice.Rock => "PreludeSymbols/prelude_rock_3d",
                    DuelPreludeChoice.Paper => "PreludeSymbols/prelude_paper_3d",
                    _ => "PreludeSymbols/prelude_scissors_3d"
                };
                Texture2D texture = Resources.Load<Texture2D>(assetPath);
                if (texture == null)
                {
                    Debug.LogError(
                        $"Não foi possível carregar a ilustração 3D de {choice} " +
                        $"em Resources/{assetPath}.");
                    return null;
                }

                icon = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                icon.name = $"Ilustração 3D {choice}";
                preludeChoiceIcons[choice] = icon;
            }
            return icon;
        }

        private static Color PreludeChoiceAccent(DuelPreludeChoice choice)
        {
            return choice switch
            {
                DuelPreludeChoice.Rock => new Color(0.14f, 0.80f, 1f, 1f),
                DuelPreludeChoice.Paper => new Color(1f, 0.78f, 0.30f, 1f),
                _ => new Color(0.78f, 0.40f, 1f, 1f)
            };
        }

        private static Sprite CreatePreludeChoiceIconSprite(
            DuelPreludeChoice choice)
        {
            const int size = 256;
            var pixels = new Color32[size * size];
            Color32 cyan = new Color32(112, 226, 255, 255);
            Color32 pale = new Color32(235, 247, 255, 255);
            Color32 steel = new Color32(116, 153, 177, 255);
            Color32 blue = new Color32(37, 111, 196, 255);
            Color32 violet = new Color32(165, 89, 238, 255);
            Color32 gold = new Color32(255, 202, 89, 255);
            Color32 parchment = new Color32(241, 231, 186, 255);
            Color32 dark = new Color32(11, 21, 39, 255);

            switch (choice)
            {
                case DuelPreludeChoice.Rock:
                    FillIconPolygon(pixels, size, new[]
                    {
                        new Vector2Int(30, 95), new Vector2Int(55, 50),
                        new Vector2Int(112, 28), new Vector2Int(174, 45),
                        new Vector2Int(222, 98), new Vector2Int(201, 165),
                        new Vector2Int(139, 220), new Vector2Int(67, 197),
                        new Vector2Int(28, 149)
                    }, dark);
                    FillIconPolygon(pixels, size, new[]
                    {
                        new Vector2Int(40, 101), new Vector2Int(63, 61),
                        new Vector2Int(113, 41), new Vector2Int(166, 57),
                        new Vector2Int(208, 103), new Vector2Int(189, 157),
                        new Vector2Int(136, 204), new Vector2Int(75, 184),
                        new Vector2Int(43, 146)
                    }, steel);
                    FillIconTriangle(pixels, size,
                        new Vector2Int(63, 61), new Vector2Int(113, 41),
                        new Vector2Int(105, 123), cyan);
                    FillIconTriangle(pixels, size,
                        new Vector2Int(113, 41), new Vector2Int(166, 57),
                        new Vector2Int(105, 123), pale);
                    FillIconTriangle(pixels, size,
                        new Vector2Int(166, 57), new Vector2Int(208, 103),
                        new Vector2Int(105, 123), blue);
                    FillIconTriangle(pixels, size,
                        new Vector2Int(43, 146), new Vector2Int(105, 123),
                        new Vector2Int(75, 184), blue);
                    FillIconTriangle(pixels, size,
                        new Vector2Int(105, 123), new Vector2Int(189, 157),
                        new Vector2Int(136, 204), dark);
                    DrawIconLine(pixels, size, 105, 123, 189, 157, 4, cyan);
                    DrawIconLine(pixels, size, 105, 123, 75, 184, 4, pale);
                    break;
                case DuelPreludeChoice.Paper:
                    FillIconPolygon(pixels, size, new[]
                    {
                        new Vector2Int(57, 30), new Vector2Int(171, 30),
                        new Vector2Int(211, 70), new Vector2Int(211, 220),
                        new Vector2Int(57, 220)
                    }, dark);
                    FillIconPolygon(pixels, size, new[]
                    {
                        new Vector2Int(66, 39), new Vector2Int(167, 39),
                        new Vector2Int(201, 73), new Vector2Int(201, 211),
                        new Vector2Int(66, 211)
                    }, parchment);
                    FillIconTriangle(pixels, size,
                        new Vector2Int(167, 39), new Vector2Int(201, 73),
                        new Vector2Int(167, 73), gold);
                    DrawIconLine(pixels, size, 91, 158, 176, 158, 6, blue);
                    DrawIconLine(pixels, size, 91, 128, 176, 128, 6, blue);
                    DrawIconLine(pixels, size, 91, 98, 152, 98, 6, blue);
                    DrawIconLine(pixels, size, 91, 68, 128, 68, 5, cyan);
                    break;
                default:
                    // As lâminas são desenhadas antes dos aros para que a
                    // tesoura tenha silhueta clara mesmo em tela pequena.
                    DrawIconLine(pixels, size, 103, 119, 207, 204, 18, dark);
                    DrawIconLine(pixels, size, 103, 119, 207, 204, 11, pale);
                    DrawIconLine(pixels, size, 142, 114, 207, 47, 18, dark);
                    DrawIconLine(pixels, size, 142, 114, 207, 47, 11, pale);
                    DrawIconLine(pixels, size, 112, 123, 199, 196, 4, cyan);
                    DrawIconLine(pixels, size, 148, 108, 199, 55, 4, cyan);
                    FillIconRing(pixels, size, 78, 82, 39, 20, dark);
                    FillIconRing(pixels, size, 78, 82, 31, 15, violet);
                    FillIconEllipse(pixels, size, 78, 82, 14, 8, dark);
                    FillIconRing(pixels, size, 83, 164, 39, 20, dark);
                    FillIconRing(pixels, size, 83, 164, 31, 15, violet);
                    FillIconEllipse(pixels, size, 83, 164, 14, 8, dark);
                    FillIconEllipse(pixels, size, 122, 119, 12, 12, gold);
                    FillIconEllipse(pixels, size, 122, 119, 5, 5, pale);
                    break;
            }

            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = $"Ícone procedural {choice}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static void FillIconPolygon(
            Color32[] pixels,
            int size,
            IReadOnlyList<Vector2Int> vertices,
            Color32 color)
        {
            if (vertices == null || vertices.Count < 3)
                return;
            int minX = Mathf.Max(0, vertices.Min(vertex => vertex.x));
            int maxX = Mathf.Min(size - 1, vertices.Max(vertex => vertex.x));
            int minY = Mathf.Max(0, vertices.Min(vertex => vertex.y));
            int maxY = Mathf.Min(size - 1, vertices.Max(vertex => vertex.y));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                bool inside = false;
                for (int current = 0, previous = vertices.Count - 1;
                     current < vertices.Count;
                     previous = current++)
                {
                    Vector2Int a = vertices[current];
                    Vector2Int b = vertices[previous];
                    bool crosses = (a.y > y) != (b.y > y) &&
                        x < (b.x - a.x) * (y - a.y) /
                        (float)(b.y - a.y) + a.x;
                    if (crosses)
                        inside = !inside;
                }
                if (inside)
                    pixels[y * size + x] = color;
            }
        }

        private static void FillIconRect(
            Color32[] pixels,
            int size,
            int minX,
            int minY,
            int maxX,
            int maxY,
            Color32 color)
        {
            for (int y = Mathf.Max(0, minY); y <= Mathf.Min(size - 1, maxY); y++)
            for (int x = Mathf.Max(0, minX); x <= Mathf.Min(size - 1, maxX); x++)
                pixels[y * size + x] = color;
        }

        private static void FillIconEllipse(
            Color32[] pixels,
            int size,
            int centerX,
            int centerY,
            int radiusX,
            int radiusY,
            Color32 color)
        {
            for (int y = centerY - radiusY; y <= centerY + radiusY; y++)
            for (int x = centerX - radiusX; x <= centerX + radiusX; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size)
                    continue;
                float dx = (x - centerX) / (float)radiusX;
                float dy = (y - centerY) / (float)radiusY;
                if (dx * dx + dy * dy <= 1f)
                    pixels[y * size + x] = color;
            }
        }

        private static void FillIconRing(
            Color32[] pixels,
            int size,
            int centerX,
            int centerY,
            int outerRadius,
            int innerRadius,
            Color32 color)
        {
            int outerSquared = outerRadius * outerRadius;
            int innerSquared = innerRadius * innerRadius;
            for (int y = centerY - outerRadius; y <= centerY + outerRadius; y++)
            for (int x = centerX - outerRadius; x <= centerX + outerRadius; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size)
                    continue;
                int dx = x - centerX;
                int dy = y - centerY;
                int distance = dx * dx + dy * dy;
                if (distance <= outerSquared && distance >= innerSquared)
                    pixels[y * size + x] = color;
            }
        }

        private static void DrawIconLine(
            Color32[] pixels,
            int size,
            int startX,
            int startY,
            int endX,
            int endY,
            int thickness,
            Color32 color)
        {
            Vector2 start = new Vector2(startX, startY);
            Vector2 end = new Vector2(endX, endY);
            Vector2 segment = end - start;
            float lengthSquared = Mathf.Max(0.001f, segment.sqrMagnitude);
            int padding = thickness + 1;
            for (int y = Mathf.Max(0, Mathf.Min(startY, endY) - padding);
                 y <= Mathf.Min(size - 1, Mathf.Max(startY, endY) + padding);
                 y++)
            for (int x = Mathf.Max(0, Mathf.Min(startX, endX) - padding);
                 x <= Mathf.Min(size - 1, Mathf.Max(startX, endX) + padding);
                 x++)
            {
                Vector2 point = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) /
                                        lengthSquared);
                if (Vector2.Distance(point, start + segment * t) <= thickness)
                    pixels[y * size + x] = color;
            }
        }

        private static void FillIconTriangle(
            Color32[] pixels,
            int size,
            Vector2Int a,
            Vector2Int b,
            Vector2Int c,
            Color32 color)
        {
            int minX = Mathf.Max(0, Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
            int maxX = Mathf.Min(size - 1, Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int minY = Mathf.Max(0, Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
            int maxY = Mathf.Min(size - 1, Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
            float area = (b.x - a.x) * (c.y - a.y) -
                         (b.y - a.y) * (c.x - a.x);
            if (Mathf.Abs(area) < 0.001f)
                return;
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float w0 = ((b.x - a.x) * (y - a.y) -
                            (b.y - a.y) * (x - a.x)) / area;
                float w1 = ((c.x - b.x) * (y - b.y) -
                            (c.y - b.y) * (x - b.x)) / area;
                float w2 = ((a.x - c.x) * (y - c.y) -
                            (a.y - c.y) * (x - c.x)) / area;
                if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) ||
                    (w0 <= 0f && w1 <= 0f && w2 <= 0f))
                {
                    pixels[y * size + x] = color;
                }
            }
        }

        private void BuildResultPanel(Transform parent, Font font)
        {
            Image panel = CreateImage(
                parent,
                "Resultado da Escolha",
                Color.clear,
                Vector2.zero,
                Vector2.one);
            resultPanel = panel.gameObject;
            resultPanelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            preludeImpactSprite = CreateProceduralBurstSprite(false);
            resultLabel = CreateText(
                panel.transform,
                "Resultado",
                font,
                34,
                FontStyle.Bold,
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f));
            resultLabel.color = new Color(0.66f, 0.93f, 1f, 1f);
            resultLabel.gameObject.SetActive(false);
            CreatePreludeResultCaption(
                panel.transform, "VOCÊ", new Vector2(0.13f, 0.585f),
                new Vector2(0.31f, 0.635f), font, 16);
            CreatePreludeResultCaption(
                panel.transform, "RIVAL", new Vector2(0.69f, 0.585f),
                new Vector2(0.87f, 0.635f), font, 16);
            resultLocalChoiceIcon = CreateImage(
                panel.transform, "Seu símbolo", Color.white,
                new Vector2(0.13f, 0.275f), new Vector2(0.31f, 0.575f));
            resultLocalChoiceIcon.preserveAspect = true;
            Shadow localShadow = resultLocalChoiceIcon.gameObject
                .AddComponent<Shadow>();
            localShadow.effectColor = new Color(0f, 0f, 0.02f, 0.92f);
            localShadow.effectDistance = new Vector2(8f, -9f);
            resultOpponentChoiceIcon = CreateImage(
                panel.transform, "Símbolo rival", Color.white,
                new Vector2(0.69f, 0.275f), new Vector2(0.87f, 0.575f));
            resultOpponentChoiceIcon.preserveAspect = true;
            Shadow opponentShadow = resultOpponentChoiceIcon.gameObject
                .AddComponent<Shadow>();
            opponentShadow.effectColor = new Color(0f, 0f, 0.02f, 0.92f);
            opponentShadow.effectDistance = new Vector2(8f, -9f);
            resultVersusLabel = CreateText(
                panel.transform, "Versus", font, 62, FontStyle.Bold,
                new Vector2(0.34f, 0.355f), new Vector2(0.66f, 0.535f));
            resultVersusLabel.text = "VERSUS";
            resultVersusLabel.color = new Color(0.66f, 0.93f, 1f, 1f);
            Outline versusOutline = resultVersusLabel.gameObject
                .AddComponent<Outline>();
            versusOutline.effectColor = new Color(0.005f, 0.025f, 0.075f, 0.94f);
            versusOutline.effectDistance = new Vector2(2f, -2f);
            resultPanel.SetActive(false);
        }

        private void BuildStartingPlayerPanel(Transform parent, Font font)
        {
            Image panel = CreateImage(
                parent,
                "Decisão de Primeiro Turno",
                new Color(0f, 0f, 0f, 0.015f),
                new Vector2(0.18f, 0.26f),
                new Vector2(0.82f, 0.61f));
            startingPlayerPanel = panel.gameObject;
            DecoratePreludeSurface(
                panel,
                new Color(0.18f, 0.88f, 1f, 1f),
                true,
                18f);

            Text heading = CreateText(
                panel.transform,
                "Instrução de Primeiro Turno",
                font,
                19,
                FontStyle.Bold,
                new Vector2(0.08f, 0.72f),
                new Vector2(0.92f, 0.90f));
            heading.text = "ESCOLHA O PRIMEIRO TURNO";
            heading.color = new Color(0.66f, 0.93f, 1f, 1f);

            CreateStartingPlayerButton(
                panel.transform,
                font,
                "Você inicia o duelo!",
                true,
                new Vector2(0.08f, 0.22f),
                new Vector2(0.47f, 0.60f),
                new Color(0.03f, 0.37f, 0.56f, 0.98f),
                new Color(0.70f, 0.96f, 1f, 1f));
            CreateStartingPlayerButton(
                panel.transform,
                font,
                "Seu oponente inicia o duelo!",
                false,
                new Vector2(0.53f, 0.22f),
                new Vector2(0.92f, 0.60f),
                new Color(0.28f, 0.12f, 0.48f, 0.98f),
                new Color(0.94f, 0.86f, 1f, 1f));
            startingPlayerPanel.SetActive(false);
        }

        private void CreateStartingPlayerButton(
            Transform parent,
            Font font,
            string label,
            bool localStarts,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color background,
            Color foreground)
        {
            Button button = CreateButton(
                parent,
                label,
                label,
                font,
                anchorMin,
                anchorMax,
                background,
                foreground);
            Text buttonLabel = button.GetComponentInChildren<Text>(true);
            if (buttonLabel != null)
                buttonLabel.fontSize = 17;
            DecoratePreludeButton(button, foreground, 11f);
            button.onClick.AddListener(() =>
            {
                foreach (Button item in startingPlayerButtons)
                    item.interactable = false;
                startingPlayerChoiceAction?.Invoke(localStarts);
            });
            startingPlayerButtons.Add(button);
        }

        private static ArcaneShopSurfaceGraphic DecoratePreludeSurface(
            Image target,
            Color accent,
            bool raised,
            float chamfer)
        {
            if (target == null)
                return null;
            target.color = new Color(0f, 0f, 0f, 0.015f);
            Transform existing = target.transform.Find(
                "Superfície Arcane do Prelúdio");
            ArcaneShopSurfaceGraphic surface = existing != null
                ? existing.GetComponent<ArcaneShopSurfaceGraphic>()
                : null;
            if (surface == null)
            {
                var item = new GameObject(
                    "Superfície Arcane do Prelúdio",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(ArcaneShopSurfaceGraphic));
                item.transform.SetParent(target.transform, false);
                item.transform.SetAsFirstSibling();
                RectTransform rect = item.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                surface = item.GetComponent<ArcaneShopSurfaceGraphic>();
            }
            surface.SetStyle(accent, raised, 1f, chamfer);
            surface.raycastTarget = false;
            return surface;
        }

        private static void DecoratePreludeButton(
            Button button,
            Color accent,
            float chamfer)
        {
            if (button == null)
                return;
            Image image = button.GetComponent<Image>();
            ArcaneShopSurfaceGraphic surface = DecoratePreludeSurface(
                image,
                accent,
                true,
                chamfer);
            if (surface == null)
                return;
            surface.raycastTarget = true;
            button.targetGraphic = surface;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.Lerp(Color.white, accent, 0.16f);
            colors.pressedColor = Color.Lerp(Color.white, accent, 0.42f);
            colors.selectedColor = Color.Lerp(Color.white, accent, 0.22f);
            colors.disabledColor = new Color(0.42f, 0.48f, 0.54f, 0.72f);
            colors.fadeDuration = 0.10f;
            button.colors = colors;
        }

        private static void CreatePreludeResultCaption(
            Transform parent,
            string value,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Font font,
            int fontSize = 15)
        {
            Text caption = CreateText(
                parent,
                value,
                font,
                fontSize,
                FontStyle.Bold,
                anchorMin,
                anchorMax);
            caption.text = value;
            caption.color = new Color(0.66f, 0.93f, 1f, 0.92f);
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

        private static Image CreateImage(
            Transform parent,
            string name,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            value.transform.SetParent(parent, false);
            RectTransform rect = value.GetComponent<RectTransform>();
            Stretch(rect, anchorMin, anchorMax);
            Image image = value.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.canvasRenderer.cullTransparentMesh = true;
            return image;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            Font font,
            int fontSize,
            FontStyle style,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject value = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            value.transform.SetParent(parent, false);
            Stretch(value.GetComponent<RectTransform>(), anchorMin, anchorMax);
            Text text = value.GetComponent<Text>();
            text.font = MasterDuelTypography.Resolve(style, fontSize);
            text.fontSize = fontSize;
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

        private static Button CreateButton(
            Transform parent,
            string name,
            string labelText,
            Font font,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color background,
            Color foreground)
        {
            Image image = CreateImage(
                parent,
                name,
                background,
                anchorMin,
                anchorMax);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(background, Color.white, 0.20f);
            colors.pressedColor = Color.Lerp(background, Color.black, 0.18f);
            button.colors = colors;
            Text label = CreateText(
                image.transform,
                "Label",
                font,
                23,
                FontStyle.Bold,
                Vector2.zero,
                Vector2.one);
            label.text = labelText;
            label.color = foreground;
            return button;
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
