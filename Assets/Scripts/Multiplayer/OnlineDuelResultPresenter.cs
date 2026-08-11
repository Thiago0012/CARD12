using System;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Multiplayer
{
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
        private Action returnAction;
        private Rect lastSafeArea;

        public bool IsVisible => canvas != null && canvas.gameObject.activeSelf;
        public bool ReturnButtonInteractable =>
            returnButton != null && returnButton.interactable;

        public void Show(
            OnlineDuelResultKind result,
            string detail,
            Action onReturn)
        {
            EnsureView();
            rankedRoot.SetActive(false);
            skipButton.gameObject.SetActive(false);
            titleLabel.gameObject.SetActive(true);
            detailLabel.gameObject.SetActive(true);
            titleLabel.text = Title(result);
            titleLabel.color = ColorFor(result);
            detailLabel.text = detail ?? string.Empty;
            returnAction = onReturn;
            returnButton.interactable = true;
            canvas.gameObject.SetActive(true);
            ApplySafeArea();
        }

        public void ShowRanked(
            OnlineDuelResultKind result,
            string detail,
            RankChangeReceipt committedReceipt,
            Action onReturn)
        {
            if (committedReceipt == null ||
                committedReceipt.status != RankReceiptStatus.Applied)
            {
                Show(result, detail, onReturn);
                return;
            }

            EnsureView();
            titleLabel.gameObject.SetActive(false);
            detailLabel.gameObject.SetActive(false);
            returnAction = onReturn;
            rankedRoot.SetActive(true);
            canvas.gameObject.SetActive(true);
            ApplySafeArea();
            rankTransition.Play(
                result,
                committedReceipt,
                () => { });
        }

        public void SetReturnButtonInteractable(bool interactable)
        {
            if (returnButton != null)
                returnButton.interactable = interactable;
        }

        public void Hide()
        {
            returnAction = null;
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
            if (canvas != null)
                canvas.gameObject.SetActive(false);
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

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
                new Color(0.005f, 0.015f, 0.03f, 0.94f),
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
                new Vector2(0.08f, 0.53f),
                new Vector2(0.92f, 0.72f));
            detailLabel = CreateText(
                safe.transform,
                "ResultDetail",
                font,
                25,
                FontStyle.Normal,
                new Vector2(0.14f, 0.39f),
                new Vector2(0.86f, 0.53f));
            detailLabel.color = new Color(0.78f, 0.84f, 0.90f, 1f);

            Image buttonImage = CreateImage(
                safe.transform,
                "ReturnToMenuButton",
                new Color(0.17f, 0.88f, 0.98f, 1f),
                new Vector2(0.34f, 0.20f),
                new Vector2(0.66f, 0.29f));
            returnButton = buttonImage.gameObject.AddComponent<Button>();
            returnButton.targetGraphic = buttonImage;
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
            buttonText.color = new Color(0.01f, 0.06f, 0.09f, 1f);
            BuildRankedView(safe.transform, font);
            canvasObject.SetActive(false);
        }

        private void BuildRankedView(Transform parent, Font font)
        {
            rankedRoot = new GameObject(
                "RankedResult",
                typeof(RectTransform));
            rankedRoot.transform.SetParent(parent, false);
            Stretch(
                rankedRoot.GetComponent<RectTransform>(),
                new Vector2(0.05f, 0.28f),
                new Vector2(0.95f, 0.94f));

            Image panel = CreateImage(
                rankedRoot.transform,
                "RankPanel",
                new Color(0.015f, 0.035f, 0.075f, 0.96f),
                new Vector2(0.05f, 0.02f),
                new Vector2(0.95f, 0.98f));
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.80f, 0.58f, 0.18f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

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
                21,
                FontStyle.Bold,
                new Vector2(0.30f, 0.19f),
                new Vector2(0.70f, 0.27f));

            Image currentMini = CreateImage(
                panel.transform,
                "CurrentRankMini",
                Color.white,
                new Vector2(0.08f, 0.35f),
                new Vector2(0.27f, 0.69f));
            currentMini.preserveAspect = true;
            Text currentMiniLabel = CreateText(
                panel.transform,
                "CurrentRankMiniLabel",
                font,
                17,
                FontStyle.Bold,
                new Vector2(0.06f, 0.27f),
                new Vector2(0.29f, 0.35f));

            Image center = CreateImage(
                panel.transform,
                "CurrentRankLarge",
                Color.white,
                new Vector2(0.34f, 0.34f),
                new Vector2(0.66f, 0.75f));
            center.preserveAspect = true;
            Text centerLabel = CreateText(
                panel.transform,
                "CurrentRankLabel",
                font,
                28,
                FontStyle.Bold,
                new Vector2(0.28f, 0.27f),
                new Vector2(0.72f, 0.36f));
            centerLabel.color = new Color(0.95f, 0.78f, 0.30f, 1f);

            Image nextMini = CreateImage(
                panel.transform,
                "NextRankMini",
                Color.white,
                new Vector2(0.73f, 0.35f),
                new Vector2(0.92f, 0.69f));
            nextMini.preserveAspect = true;
            Text nextMiniLabel = CreateText(
                panel.transform,
                "NextRankMiniLabel",
                font,
                17,
                FontStyle.Bold,
                new Vector2(0.71f, 0.27f),
                new Vector2(0.94f, 0.35f));

            Image barBackground = CreateImage(
                panel.transform,
                "RankBarBackground",
                new Color(0.02f, 0.02f, 0.03f, 1f),
                new Vector2(0.22f, 0.11f),
                new Vector2(0.78f, 0.17f));
            Image barFill = CreateImage(
                barBackground.transform,
                "RankBarFill",
                new Color(0.15f, 0.82f, 1f, 1f),
                Vector2.zero,
                Vector2.one);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = 0;
            Text barValue = CreateText(
                barBackground.transform,
                "RankBarValue",
                font,
                16,
                FontStyle.Bold,
                Vector2.zero,
                Vector2.one);
            Text remaining = CreateText(
                panel.transform,
                "RankRemaining",
                font,
                16,
                FontStyle.Bold,
                new Vector2(0.20f, 0.035f),
                new Vector2(0.80f, 0.105f));
            remaining.color = new Color(0.65f, 0.80f, 0.93f, 1f);

            Image skipImage = CreateImage(
                parent,
                "SkipRankAnimation",
                new Color(0.08f, 0.16f, 0.24f, 0.96f),
                new Vector2(0.70f, 0.20f),
                new Vector2(0.86f, 0.27f));
            skipButton = skipImage.gameObject.AddComponent<Button>();
            skipButton.targetGraphic = skipImage;
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
            barView.Initialize(barFill, barValue, remaining);
            RankEmblemView emblemView =
                rankedRoot.AddComponent<RankEmblemView>();
            emblemView.Initialize(center, centerLabel);
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
            rankTransition =
                rankedRoot.AddComponent<RankTransitionAnimator>();
            rankTransition.Initialize(
                barView,
                emblemView,
                sideView,
                resultBanner,
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
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
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
