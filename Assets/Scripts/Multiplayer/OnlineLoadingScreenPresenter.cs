using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class OnlineLoadingScreenPresenter : MonoBehaviour
    {
        private const float FadeInSeconds = 0.28f;
        private const float FadeOutSeconds = 0.35f;
        private float minimumVisibleSeconds = 0.35f;

        private Canvas canvas;
        private CanvasGroup group;
        private RectTransform safeAreaPanel;
        private RectTransform spinner;
        private Text primaryLabel;
        private Text secondaryLabel;
        private Button backButton;
        private Action backAction;
        private float targetAlpha;
        private float shownAt;
        private bool hideRequested;
        private Rect lastSafeArea;

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public bool IsOpaque => IsVisible && group != null && group.alpha >= 0.995f;

        public void ConfigureMinimumVisible(float seconds)
        {
            minimumVisibleSeconds = Mathf.Clamp(seconds, 0.1f, 3f);
        }

        public void Show(string primary, string secondary = "")
        {
            EnsureView();
            if (!canvas.gameObject.activeSelf)
            {
                canvas.gameObject.SetActive(true);
                group.alpha = 0f;
                shownAt = Time.realtimeSinceStartup;
            }
            primaryLabel.text = string.IsNullOrWhiteSpace(primary)
                ? "Carregando duelo..."
                : primary;
            secondaryLabel.text = secondary ?? string.Empty;
            secondaryLabel.gameObject.SetActive(
                !string.IsNullOrWhiteSpace(secondaryLabel.text));
            backButton.gameObject.SetActive(false);
            backAction = null;
            hideRequested = false;
            targetAlpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        public void SetText(string primary, string secondary = "")
        {
            Show(primary, secondary);
            if (group != null)
                group.alpha = Mathf.Max(group.alpha, 0.001f);
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
            if (!IsVisible)
                return;
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

        private void Update()
        {
            if (!IsVisible || group == null)
                return;

            ApplySafeArea();
            if (spinner != null)
                spinner.Rotate(0f, 0f, -150f * Time.unscaledDeltaTime);

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
                Time.unscaledDeltaTime / Mathf.Max(0.01f, duration));
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

            GameObject safe = new GameObject("SafeArea", typeof(RectTransform));
            safe.transform.SetParent(black.transform, false);
            safeAreaPanel = safe.GetComponent<RectTransform>();
            Stretch(safeAreaPanel, Vector2.zero, Vector2.one);

            spinner = CreateImage(
                safe.transform,
                "Spinner",
                new Color(0.20f, 0.88f, 0.98f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f)).rectTransform;
            spinner.sizeDelta = new Vector2(54f, 8f);
            spinner.anchoredPosition = new Vector2(0f, 38f);

            primaryLabel = CreateText(
                safe.transform,
                "PrimaryLabel",
                font,
                32,
                FontStyle.Bold,
                new Vector2(0.15f, 0.43f),
                new Vector2(0.85f, 0.50f));
            primaryLabel.text = "Carregando duelo...";
            secondaryLabel = CreateText(
                safe.transform,
                "SecondaryLabel",
                font,
                21,
                FontStyle.Normal,
                new Vector2(0.12f, 0.34f),
                new Vector2(0.88f, 0.42f));
            secondaryLabel.color = new Color(0.70f, 0.78f, 0.84f, 1f);

            backButton = CreateButton(safe.transform, font);
            backButton.onClick.AddListener(() => backAction?.Invoke());
            backButton.gameObject.SetActive(false);
            group.alpha = 0f;
            canvasObject.SetActive(false);
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
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(Image));
            value.transform.SetParent(parent, false);
            RectTransform rect = value.GetComponent<RectTransform>();
            Stretch(rect, anchorMin, anchorMax);
            Image image = value.GetComponent<Image>();
            image.color = color;
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
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(Text));
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

        private static Button CreateButton(Transform parent, Font font)
        {
            Image image = CreateImage(
                parent,
                "ReturnButton",
                new Color(0.15f, 0.78f, 0.92f, 1f),
                new Vector2(0.34f, 0.20f),
                new Vector2(0.66f, 0.28f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Text label = CreateText(
                image.transform,
                "Label",
                font,
                24,
                FontStyle.Bold,
                Vector2.zero,
                Vector2.one);
            label.text = "VOLTAR AO MENU";
            label.color = new Color(0.01f, 0.06f, 0.09f, 1f);
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
