using System;
using System.Collections;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Multiplayer
{
    public sealed class RankPointsBarView : MonoBehaviour
    {
        private Image fill;
        private Text valueLabel;
        private Text remainingLabel;

        public void Initialize(Image fillImage, Text value, Text remaining)
        {
            fill = fillImage;
            valueLabel = value;
            remainingLabel = remaining;
        }

        public void SetState(int points)
        {
            RankTier tier = RankRules.ResolveTier(points);
            SetVisual(tier, RankRules.TierProgress01(points), points);
        }

        public void SetVisual(RankTier tier, float progress, int absolutePoints)
        {
            progress = Mathf.Clamp01(progress);
            if (fill != null)
                fill.fillAmount = progress;
            RankDefinition definition = RankRules.Definition(tier);
            int inside = Mathf.Clamp(
                absolutePoints - definition.Minimum,
                0,
                tier == RankTier.GrandMaster ? 25 : 24);
            if (valueLabel != null)
            {
                valueLabel.text = tier == RankTier.GrandMaster &&
                                  absolutePoints >= RankRules.MaximumPoints
                    ? "MAX · 200 PE"
                    : $"{inside}/25 · {absolutePoints} PE";
            }
            if (remainingLabel == null)
                return;
            if (tier == RankTier.GrandMaster)
            {
                remainingLabel.text = absolutePoints >= RankRules.MaximumPoints
                    ? "RANQUE MÁXIMO"
                    : $"{RankRules.MaximumPoints - absolutePoints} PE PARA MAX";
            }
            else
            {
                RankTier next = (RankTier)((int)tier + 1);
                int remaining = definition.Maximum + 1 - absolutePoints;
                remainingLabel.text =
                    $"{remaining} PE PARA {RankRules.DisplayName(next)}";
            }
        }
    }

    public sealed class RankEmblemView : MonoBehaviour
    {
        private Image emblem;
        private Text tierLabel;
        private CanvasGroup group;

        public void Initialize(Image image, Text label)
        {
            emblem = image;
            tierLabel = label;
            group = image != null
                ? image.GetComponent<CanvasGroup>() ??
                  image.gameObject.AddComponent<CanvasGroup>()
                : null;
        }

        public void SetTier(RankTier tier)
        {
            if (emblem != null)
            {
                emblem.sprite = RankBadgeCatalog.Get(tier);
                emblem.preserveAspect = true;
                emblem.color = Color.white;
            }
            if (tierLabel != null)
                tierLabel.text = RankRules.DisplayName(tier);
        }

        public IEnumerator SwapTo(RankTier tier, float duration)
        {
            if (emblem == null || group == null)
            {
                SetTier(tier);
                yield break;
            }
            RectTransform rect = emblem.rectTransform;
            Vector3 original = Vector3.one;
            float half = Mathf.Max(0.08f, duration * 0.5f);
            for (float elapsed = 0f; elapsed < half;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / half);
                group.alpha = 1f - t;
                rect.localScale = Vector3.Lerp(original, original * 0.82f, t);
                yield return null;
            }
            SetTier(tier);
            for (float elapsed = 0f; elapsed < half;
                 elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / half);
                group.alpha = t;
                rect.localScale = Vector3.Lerp(original * 1.18f, original, t);
                yield return null;
            }
            group.alpha = 1f;
            rect.localScale = original;
        }
    }

    public sealed class RankSideSlotView : MonoBehaviour
    {
        private Image current;
        private Image next;
        private Text currentLabel;
        private Text nextLabel;

        public void Initialize(
            Image currentImage,
            Text currentText,
            Image nextImage,
            Text nextText)
        {
            current = currentImage;
            currentLabel = currentText;
            next = nextImage;
            nextLabel = nextText;
        }

        public void SetTier(RankTier tier)
        {
            SetImage(current, tier);
            if (currentLabel != null)
                currentLabel.text = RankRules.DisplayName(tier);
            bool hasNext = tier < RankTier.GrandMaster;
            if (next != null)
                next.gameObject.SetActive(hasNext);
            if (nextLabel != null)
            {
                nextLabel.text = hasNext
                    ? RankRules.DisplayName((RankTier)((int)tier + 1))
                    : "MAX";
            }
            if (hasNext)
                SetImage(next, (RankTier)((int)tier + 1));
        }

        private static void SetImage(Image image, RankTier tier)
        {
            if (image == null)
                return;
            image.sprite = RankBadgeCatalog.Get(tier);
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, 0.82f);
        }
    }

    public sealed class RankResultBanner : MonoBehaviour
    {
        private Text result;
        private Text delta;
        private Text transition;
        private CanvasGroup resultGroup;
        private CanvasGroup deltaGroup;

        public void Initialize(Text resultText, Text deltaText, Text transitionText)
        {
            result = resultText;
            delta = deltaText;
            transition = transitionText;
            resultGroup = EnsureGroup(result);
            deltaGroup = EnsureGroup(delta);
        }

        public void Prepare(OnlineDuelResultKind kind, RankChangeReceipt receipt)
        {
            if (result != null)
            {
                result.text = kind == OnlineDuelResultKind.Victory
                    ? "VITÓRIA"
                    : kind == OnlineDuelResultKind.Defeat
                        ? "DERROTA"
                        : kind == OnlineDuelResultKind.Draw
                            ? "EMPATE"
                            : "PARTIDA ENCERRADA";
                result.color = kind == OnlineDuelResultKind.Victory
                    ? new Color(0.65f, 1f, 0.15f, 1f)
                    : kind == OnlineDuelResultKind.Defeat
                        ? new Color(1f, 0.30f, 0.38f, 1f)
                        : new Color(1f, 0.78f, 0.20f, 1f);
            }
            if (delta != null)
            {
                delta.text = receipt.delta > 0
                    ? $"+{receipt.delta} PE"
                    : $"{receipt.delta} PE";
                delta.color = receipt.delta > 0
                    ? new Color(0.65f, 1f, 0.15f, 1f)
                    : receipt.delta < 0
                        ? new Color(1f, 0.38f, 0.38f, 1f)
                        : new Color(0.70f, 0.82f, 0.92f, 1f);
            }
            SetTransition(string.Empty, Color.white);
            SetResultAlpha(0f);
            SetDeltaAlpha(0f);
        }

        public void SetResultAlpha(float alpha)
        {
            SetAlpha(resultGroup, result, alpha, 0.95f);
        }

        public void SetDeltaAlpha(float alpha)
        {
            SetAlpha(deltaGroup, delta, alpha, 0.88f);
        }

        public void SetTransition(string text, Color color)
        {
            if (transition == null)
                return;
            transition.text = text ?? string.Empty;
            transition.color = color;
        }

        private static CanvasGroup EnsureGroup(Graphic graphic)
        {
            if (graphic == null)
                return null;
            return graphic.GetComponent<CanvasGroup>() ??
                   graphic.gameObject.AddComponent<CanvasGroup>();
        }

        private static void SetAlpha(
            CanvasGroup group,
            Graphic graphic,
            float alpha,
            float initialScale)
        {
            float clamped = Mathf.Clamp01(alpha);
            if (group != null)
                group.alpha = clamped;
            if (graphic != null)
            {
                float eased = Mathf.SmoothStep(0f, 1f, clamped);
                graphic.rectTransform.localScale = Vector3.one *
                    Mathf.Lerp(initialScale, 1f, eased);
            }
        }
    }

    public sealed class RankTransitionAnimator : MonoBehaviour
    {
        private RankPointsBarView bar;
        private RankEmblemView emblem;
        private RankSideSlotView sides;
        private RankResultBanner banner;
        private Button leaveButton;
        private Button skipButton;
        private RankChangeReceipt receipt;
        private Action completed;
        private Coroutine routine;
        private bool skipRequested;

        public void Initialize(
            RankPointsBarView pointsBar,
            RankEmblemView emblemView,
            RankSideSlotView sideSlots,
            RankResultBanner resultBanner,
            Button returnButton,
            Button controlledSkipButton)
        {
            bar = pointsBar;
            emblem = emblemView;
            sides = sideSlots;
            banner = resultBanner;
            leaveButton = returnButton;
            skipButton = controlledSkipButton;
        }

        public void Play(
            OnlineDuelResultKind kind,
            RankChangeReceipt committedReceipt,
            Action onCompleted)
        {
            if (routine != null)
                StopCoroutine(routine);
            receipt = committedReceipt;
            completed = onCompleted;
            skipRequested = false;
            if (leaveButton != null)
                leaveButton.interactable = false;
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.interactable = true;
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(SkipToFinal);
            }
            emblem.SetTier(receipt.oldTier);
            sides.SetTier(receipt.oldTier);
            bar.SetState(receipt.oldPoints);
            banner.Prepare(kind, receipt);
            routine = StartCoroutine(Sequence());
        }

        public void SkipToFinal()
        {
            skipRequested = true;
        }

        private IEnumerator Sequence()
        {
            yield return FadeResult(0.32f);
            if (FinishIfSkipped())
                yield break;
            yield return Wait(0.12f);
            if (FinishIfSkipped())
                yield break;
            yield return FadeDelta(0.42f);
            if (FinishIfSkipped())
                yield break;
            yield return Wait(0.25f);
            if (FinishIfSkipped())
                yield break;

            if (receipt.delta > 0)
                yield return AnimateUp();
            else if (receipt.delta < 0)
                yield return AnimateDown();

            if (FinishIfSkipped())
                yield break;
            if (receipt.shieldPreventedDemotion)
            {
                banner.SetTransition(
                    "PROTEGIDO NESTA DERROTA",
                    new Color(0.35f, 0.90f, 1f, 1f));
                yield return Wait(0.75f);
            }
            ApplyFinalState();
        }

        private IEnumerator AnimateUp()
        {
            int cursor = receipt.oldPoints;
            RankTier visualTier = receipt.oldTier;
            while (cursor < receipt.newPoints)
            {
                RankDefinition definition = RankRules.Definition(visualTier);
                int nextFloor = definition.Maximum + 1;
                int segmentEnd = Mathf.Min(receipt.newPoints, nextFloor);
                float start = (cursor - definition.Minimum) / 25f;
                float end = segmentEnd == nextFloor
                    ? 1f
                    : (segmentEnd - definition.Minimum) / 25f;
                yield return AnimateBar(
                    visualTier,
                    cursor,
                    segmentEnd,
                    start,
                    end,
                    1.05f * Mathf.Max(0.2f, end - start));
                cursor = segmentEnd;
                if (cursor != nextFloor || visualTier >= receipt.newTier)
                    continue;

                RankTier promoted = (RankTier)((int)visualTier + 1);
                banner.SetTransition(
                    "PROMOÇÃO",
                    new Color(0.65f, 1f, 0.22f, 1f));
                yield return Wait(0.18f);
                yield return emblem.SwapTo(promoted, 0.58f);
                sides.SetTier(promoted);
                bar.SetVisual(promoted, 0f, cursor);
                visualTier = promoted;
                banner.SetTransition(string.Empty, Color.white);
            }
        }

        private IEnumerator AnimateDown()
        {
            int cursor = receipt.oldPoints;
            RankTier visualTier = receipt.oldTier;
            while (cursor > receipt.newPoints)
            {
                RankDefinition definition = RankRules.Definition(visualTier);
                int floor = definition.Minimum;
                int segmentEnd = Mathf.Max(receipt.newPoints, floor);
                float start = (cursor - floor) / 25f;
                float end = (segmentEnd - floor) / 25f;
                yield return AnimateBar(
                    visualTier,
                    cursor,
                    segmentEnd,
                    start,
                    end,
                    1.05f * Mathf.Max(0.2f, start - end));
                cursor = segmentEnd;
                if (receipt.newPoints >= floor || visualTier <= receipt.newTier)
                    break;

                RankTier demoted = (RankTier)((int)visualTier - 1);
                banner.SetTransition(
                    "REBAIXAMENTO",
                    new Color(1f, 0.38f, 0.38f, 1f));
                yield return Wait(0.18f);
                yield return emblem.SwapTo(demoted, 0.52f);
                sides.SetTier(demoted);
                cursor = floor - 1;
                bar.SetVisual(demoted, 1f, cursor);
                visualTier = demoted;
                banner.SetTransition(string.Empty, Color.white);
            }
        }

        private IEnumerator AnimateBar(
            RankTier tier,
            int fromPoints,
            int toPoints,
            float from,
            float to,
            float duration)
        {
            duration = Mathf.Clamp(duration, 0.20f, 1.25f);
            for (float elapsed = 0f; elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                if (skipRequested)
                    yield break;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                int points = Mathf.RoundToInt(Mathf.Lerp(
                    fromPoints,
                    toPoints,
                    t));
                bar.SetVisual(tier, Mathf.Lerp(from, to, t), points);
                yield return null;
            }
            bar.SetVisual(tier, to, toPoints);
        }

        private IEnumerator FadeResult(float duration)
        {
            for (float elapsed = 0f; elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                if (skipRequested)
                    yield break;
                banner.SetResultAlpha(elapsed / duration);
                yield return null;
            }
            banner.SetResultAlpha(1f);
        }

        private IEnumerator FadeDelta(float duration)
        {
            for (float elapsed = 0f; elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                if (skipRequested)
                    yield break;
                banner.SetDeltaAlpha(elapsed / duration);
                yield return null;
            }
            banner.SetDeltaAlpha(1f);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        private bool FinishIfSkipped()
        {
            if (!skipRequested)
                return false;
            ApplyFinalState();
            return true;
        }

        private void ApplyFinalState()
        {
            banner.SetResultAlpha(1f);
            banner.SetDeltaAlpha(1f);
            emblem.SetTier(receipt.newTier);
            sides.SetTier(receipt.newTier);
            bar.SetState(receipt.newPoints);
            if (receipt.shieldPreventedDemotion)
            {
                banner.SetTransition(
                    "PROTEGIDO · ESCUDO CONSUMIDO",
                    new Color(0.35f, 0.90f, 1f, 1f));
            }
            else if (receipt.promoted)
            {
                banner.SetTransition(
                    "PROMOÇÃO CONCLUÍDA",
                    new Color(0.65f, 1f, 0.22f, 1f));
            }
            else if (receipt.demoted)
            {
                banner.SetTransition(
                    "NOVO ELO",
                    new Color(1f, 0.55f, 0.28f, 1f));
            }
            if (skipButton != null)
                skipButton.gameObject.SetActive(false);
            if (leaveButton != null)
                leaveButton.interactable = true;
            routine = null;
            Action callback = completed;
            completed = null;
            callback?.Invoke();
        }
    }
}
