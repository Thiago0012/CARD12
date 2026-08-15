using System;
using System.Collections;
using System.Collections.Generic;
using ArcaneArena.Cards;
using ArcaneArena.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Multiplayer
{
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
        private float minimumVisibleSeconds = 0.35f;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform safeAreaPanel;
        private RectTransform spinner;
        private RectTransform progressRoot;
        private Image progressFill;
        private Text progressLabel;
        private Text primaryLabel;
        private Text secondaryLabel;
        private GameObject choicePanel;
        private GameObject resultPanel;
        private Text resultLabel;
        private readonly List<Button> choiceButtons = new();
        private readonly List<RectTransform> lightStreaks = new();
        private readonly List<RectTransform> floatingCards = new();
        private readonly List<Vector2> floatingCardOrigins = new();
        private readonly List<float> floatingCardDepths = new();
        private readonly List<float> floatingCardAngles = new();
        private readonly System.Random visualRandom =
            new System.Random(Environment.TickCount);
        private Button backButton;
        private Action backAction;
        private Action<DuelPreludeChoice> choiceAction;
        private Coroutine transitionRoutine;
        private float targetAlpha;
        private float shownAt;
        private bool hideRequested;
        private bool loadingMode;
        private float progressValue;
        private Rect lastSafeArea;

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public bool IsOpaque => IsVisible && group != null && group.alpha >= 0.995f;

        public void ConfigureMinimumVisible(float seconds)
        {
            minimumVisibleSeconds = Mathf.Clamp(seconds, 0.1f, 3f);
        }

        public void Show(string primary, string secondary = "")
        {
            loadingMode = false;
            PrepareVisibleSurface();
            SetPreludeMode(false, false);
            ApplyText(primary, secondary);
            spinner.gameObject.SetActive(true);
            progressRoot.gameObject.SetActive(false);
            backButton.gameObject.SetActive(false);
            backAction = null;
            RefreshFloatingCardArtwork();
        }

        public void ShowDuelLoading(
            string primary,
            string secondary = "",
            float initialProgress = 0.04f)
        {
            bool enteringLoading = !IsVisible || !loadingMode ||
                                   choicePanel?.activeSelf == true ||
                                   resultPanel?.activeSelf == true;
            loadingMode = true;
            PrepareVisibleSurface();
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
        }

        public void SetText(string primary, string secondary = "")
        {
            PrepareVisibleSurface();
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
            SetPreludeMode(true, false);
            choiceAction = onChoice;
            primaryLabel.text = "QUEM INICIA O DUELO?";
            secondaryLabel.text = string.IsNullOrWhiteSpace(opponentName)
                ? $"RODADA {Mathf.Max(1, round)} · ESCOLHA EM SEGREDO"
                : $"CONTRA {opponentName.ToUpperInvariant()} · RODADA {Mathf.Max(1, round)}";
            secondaryLabel.gameObject.SetActive(true);
            foreach (Button button in choiceButtons)
                button.interactable = true;
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
            loadingMode = false;
            PrepareVisibleSurface();
            SetPreludeMode(false, true);
            primaryLabel.text = tie
                ? "EMPATE"
                : localWon ? "VOCÊ COMEÇA" : "O RIVAL COMEÇA";
            secondaryLabel.text = tie
                ? "As escolhas foram iguais. Uma nova rodada será iniciada."
                : "Resultado confirmado · preparando os dois campos.";
            secondaryLabel.gameObject.SetActive(true);
            resultLabel.text =
                $"{DuelPreludeRules.Label(localChoice)}   ×   " +
                DuelPreludeRules.Label(opponentChoice);
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
            EnsureView();
            if (transitionRoutine != null)
                StopCoroutine(transitionRoutine);
            transitionRoutine = StartCoroutine(
                ShowSceneLoadingRoutine(primary, secondary, loadAction));
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
            Action loadAction)
        {
            Show(primary, secondary);
            float deadline = Time.realtimeSinceStartup + 1.2f;
            while (!IsOpaque && Time.realtimeSinceStartup < deadline)
                yield return null;

            loadAction?.Invoke();
            yield return new WaitForSecondsRealtime(0.18f);
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
            spinner.gameObject.SetActive(!choices && !result);
            progressRoot.gameObject.SetActive(
                !choices && !result && loadingMode);
            choiceAction = choices ? choiceAction : null;
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
            for (int index = 0; index < lightStreaks.Count; index++)
            {
                RectTransform streak = lightStreaks[index];
                if (streak == null)
                    continue;
                float pulse = 0.72f + 0.28f * Mathf.Sin(
                    now * (1.15f + index * 0.025f) + index * 0.71f);
                streak.localScale = new Vector3(pulse, 1f, 1f);
            }
            for (int index = 0; index < floatingCards.Count; index++)
            {
                RectTransform card = floatingCards[index];
                if (card == null)
                    continue;
                float depth = floatingCardDepths[index];
                float phase = index * 0.83f;
                Vector2 origin = floatingCardOrigins[index];
                Vector2 outward = origin.sqrMagnitude > 0.01f
                    ? origin.normalized
                    : Vector2.up;
                float travel = Mathf.Sin(
                    now * Mathf.Lerp(0.16f, 0.32f, depth) + phase) *
                    Mathf.Lerp(8f, 28f, depth);
                float vertical = Mathf.Sin(now * 0.23f + phase * 1.7f) *
                                 Mathf.Lerp(5f, 17f, depth);
                card.anchoredPosition = origin + outward * travel +
                                        Vector2.up * vertical;
                card.localEulerAngles = new Vector3(
                    0f,
                    Mathf.Sin(now * 0.21f + phase) * 8f * depth,
                    floatingCardAngles[index] +
                    Mathf.Sin(now * 0.18f + phase) * 4f);
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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
            Image voidLayer = CreateImage(
                black.transform,
                "Abismo Violeta",
                new Color(0.018f, 0.006f, 0.055f, 1f),
                Vector2.zero,
                Vector2.one);
            voidLayer.raycastTarget = false;
            BuildWarpBackdrop(voidLayer.transform);

            GameObject safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(black.transform, false);
            safeAreaPanel = safe.GetComponent<RectTransform>();
            Stretch(safeAreaPanel, Vector2.zero, Vector2.one);

            spinner = CreateImage(
                safe.transform,
                "Spinner",
                new Color(0.29f, 0.91f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f)).rectTransform;
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
            group.alpha = 0f;
            canvasObject.SetActive(false);
        }

        private void BuildWarpBackdrop(Transform parent)
        {
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
            }

            for (int index = 0; index < 10; index++)
            {
                Image card = CreateImage(
                    parent,
                    $"Carta no Vórtice {index + 1}",
                    new Color(0.07f, 0.12f, 0.22f, 0.66f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f));
                RectTransform rect = card.rectTransform;
                float angle = index * 36f + 14f;
                float radius = 260f + index % 4 * 150f;
                Vector2 origin = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius * 0.56f);
                float depth = 0.24f + (index % 5) * 0.17f;
                float height = Mathf.Lerp(92f, 224f, depth);
                rect.anchoredPosition = origin;
                rect.sizeDelta = new Vector2(height * 0.686f, height);
                float rotation = -angle + 90f + (index % 3 - 1) * 11f;
                rect.localEulerAngles = new Vector3(0f, 0f, rotation);
                card.preserveAspect = true;
                floatingCards.Add(rect);
                floatingCardOrigins.Add(origin);
                floatingCardDepths.Add(depth);
                floatingCardAngles.Add(rotation);
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

            var artwork = new List<Sprite>();
            CardCatalog[] catalogs =
                Resources.FindObjectsOfTypeAll<CardCatalog>();
            foreach (CardCatalog catalog in catalogs)
            {
                if (catalog == null)
                    continue;
                foreach (CardCatalogEntry entry in catalog.Entries)
                {
                    if (entry?.Artwork != null)
                        artwork.Add(entry.Artwork);
                }
                if (artwork.Count > 0)
                    break;
            }
            if (artwork.Count == 0)
                return;

            for (int index = 0; index < floatingCards.Count; index++)
            {
                Image image = floatingCards[index]
                    ?.GetComponent<Image>();
                if (image == null)
                    continue;
                image.sprite = artwork[visualRandom.Next(artwork.Count)];
                float depth = floatingCardDepths[index];
                image.color = new Color(
                    0.76f + depth * 0.24f,
                    0.82f + depth * 0.18f,
                    1f,
                    Mathf.Lerp(0.34f, 0.86f, depth));
            }
        }

        private void BuildChoicePanel(Transform parent, Font font)
        {
            Image panel = CreateImage(
                parent,
                "Pedra Papel Tesoura",
                new Color(0.025f, 0.045f, 0.095f, 0.94f),
                new Vector2(0.24f, 0.48f),
                new Vector2(0.76f, 0.73f));
            choicePanel = panel.gameObject;
            DuelPreludeChoice[] choices =
            {
                DuelPreludeChoice.Rock,
                DuelPreludeChoice.Paper,
                DuelPreludeChoice.Scissors
            };
            string[] glyphs = { "◆\nPEDRA", "▰\nPAPEL", "✦\nTESOURA" };
            for (int index = 0; index < choices.Length; index++)
            {
                float xMin = 0.035f + index * 0.325f;
                DuelPreludeChoice captured = choices[index];
                Button button = CreateButton(
                    panel.transform,
                    captured.ToString(),
                    glyphs[index],
                    font,
                    new Vector2(xMin, 0.12f),
                    new Vector2(xMin + 0.28f, 0.88f),
                    index == 1
                        ? new Color(0.20f, 0.62f, 0.94f, 0.90f)
                        : new Color(0.36f, 0.18f, 0.74f, 0.90f),
                    Color.white);
                button.onClick.AddListener(() =>
                {
                    foreach (Button item in choiceButtons)
                        item.interactable = false;
                    Action<DuelPreludeChoice> callback = choiceAction;
                    callback?.Invoke(captured);
                });
                choiceButtons.Add(button);
            }
            choicePanel.SetActive(false);
        }

        private void BuildResultPanel(Transform parent, Font font)
        {
            Image panel = CreateImage(
                parent,
                "Resultado da Escolha",
                new Color(0.025f, 0.045f, 0.095f, 0.94f),
                new Vector2(0.31f, 0.51f),
                new Vector2(0.69f, 0.68f));
            resultPanel = panel.gameObject;
            resultLabel = CreateText(
                panel.transform,
                "Resultado",
                font,
                34,
                FontStyle.Bold,
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f));
            resultLabel.color = new Color(0.66f, 0.93f, 1f, 1f);
            resultPanel.SetActive(false);
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
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
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
