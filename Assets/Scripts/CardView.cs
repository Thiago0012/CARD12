using System.Collections;
using ArcaneDuel.DuelEngine.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcaneArena
{
    [DisallowMultipleComponent]
    public sealed class CardView :
        MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        private RectTransform rect;
        private Outline outline;
        private CanvasGroup canvasGroup;
        private CardArenaBootstrap arena;
        private Vector2 restPosition;
        private float restAngle;
        private bool hovered;
        private bool selected;
        private bool dragging;
        private bool interactive = true;
        private bool entering;
        private bool presentationHidden;
        private bool legalGlowEnabled;
        private bool dualLegalGlow;
        private Color legalGlowPrimary;
        private Color legalGlowSecondary;
        private Coroutine poseRoutine;

        // Keep the authored prototype's restrained hand motion.  The larger
        // values previously used here pushed the selected card over the first
        // row of field zones and made placement needlessly difficult.
        private const float SelectedLift = 44f;
        private const float SelectedScale = 1.04f;
        private const float HoverLift = 56f;
        private const float HoverScale = 1.07f;

        public uint Code { get; private set; }
        public int HandIndex { get; private set; }
        public CardInstanceKey InstanceKey { get; private set; }
        public Sprite Artwork { get; private set; }
        public RectTransform Rect => rect;

        public void Setup(
            CardArenaBootstrap owner,
            uint code,
            Sprite artwork,
            int handIndex)
        {
            Setup(
                owner,
                new CardInstanceKey(
                    0,
                    code,
                    0,
                    0,
                    0x02,
                    (uint)Mathf.Max(0, handIndex),
                    0),
                artwork,
                handIndex);
        }

        public void Setup(
            CardArenaBootstrap owner,
            CardInstanceKey instanceKey,
            Sprite artwork,
            int handIndex)
        {
            arena = owner;
            InstanceKey = instanceKey;
            Code = instanceKey.DefinitionCode;
            Artwork = artwork;
            HandIndex = handIndex;
            rect = GetComponent<RectTransform>();
            outline = GetComponent<Outline>() ??
                      gameObject.AddComponent<Outline>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = Application.isPlaying ? 0f : 1f;
            canvasGroup.blocksRaycasts = true;
            entering = Application.isPlaying;
            if (entering && rect != null)
                rect.localScale = Vector3.one * 0.86f;
            outline.effectDistance = new Vector2(4f, -4f);
            outline.effectColor = new Color(0.1f, 0.95f, 1f, 0f);
            outline.useGraphicAlpha = true;
        }

        public void SetHandOrder(int index)
        {
            HandIndex = index;
        }

        public void Rebind(
            CardInstanceKey instanceKey,
            int handIndex)
        {
            InstanceKey = instanceKey;
            Code = instanceKey.DefinitionCode;
            HandIndex = handIndex;
        }

        public void SetRestPose(Vector2 position, float angle)
        {
            restPosition = position;
            restAngle = angle;
            ApplyPose();
        }

        public void SetSelected(bool value)
        {
            selected = value;
            ApplyOutline();
            ApplyPose();
        }

        public void SetLegalActionGlow(Color color, bool enabled)
        {
            SetLegalActionGlow(color, color, enabled);
        }

        public void SetLegalActionGlow(
            Color primary,
            Color secondary,
            bool enabled)
        {
            if (outline == null) return;
            legalGlowEnabled = enabled;
            legalGlowPrimary = primary;
            legalGlowSecondary = secondary;
            dualLegalGlow = enabled && primary != secondary;
            ApplyOutline();
        }

        private void Update()
        {
            if (!legalGlowEnabled || selected || outline == null)
                return;
            ApplyLegalGlow();
        }

        public void SetInteraction(bool enabled)
        {
            interactive = enabled;
            if (enabled)
            {
                SetDragVisual(false);
                return;
            }

            hovered = false;
            dragging = false;
            SetDragVisual(false);
            ApplyPose();
        }

        public void SetPresentationVisible(bool visible)
        {
            presentationHidden = !visible;
            if (canvasGroup == null)
                return;

            if (presentationHidden)
            {
                if (poseRoutine != null)
                {
                    StopCoroutine(poseRoutine);
                    poseRoutine = null;
                }
                entering = false;
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                return;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = interactive;
            if (isActiveAndEnabled && gameObject.activeInHierarchy)
                ApplyPose();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!interactive || dragging) return;
            hovered = true;
            transform.SetAsLastSibling();
            ApplyPose();
            arena?.NotifyHandHoverChanged(this, true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!interactive || dragging) return;
            hovered = false;
            ApplyPose();
            arena?.NotifyHandHoverChanged(this, false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!interactive || dragging) return;
            arena?.SelectCard(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!interactive) return;
            dragging = true;
            SetDragVisual(true);
            arena?.SelectCard(this);
            arena?.BeginCardDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!interactive || !dragging || rect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local);
            rect.anchoredPosition = local;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one * 1.03f;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!interactive || !dragging) return;
            dragging = false;
            SetDragVisual(false);
            arena?.EndCardDrag(eventData.position);
            ApplyPose();
        }

        private void SetDragVisual(bool active)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = presentationHidden
                ? 0f
                : active
                    ? 0.68f
                    : 1f;
            canvasGroup.blocksRaycasts =
                !presentationHidden && interactive && !active;
        }

        private void ApplyOutline()
        {
            if (outline == null) return;
            if (legalGlowEnabled)
            {
                ApplyLegalGlow();
                return;
            }
            outline.effectDistance = new Vector2(4f, -4f);
            outline.effectColor = selected
                ? new Color(0.90f, 0.96f, 1f, 1f)
                : new Color(0f, 0f, 0f, 0f);
        }

        private void ApplyLegalGlow()
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime * 6.4f + HandIndex * 0.47f);
            float colorBlend = dualLegalGlow
                ? 0.5f + 0.5f * Mathf.Sin(
                    Time.unscaledTime * 2.8f + HandIndex * 0.31f)
                : 0f;
            Color color = Color.Lerp(
                legalGlowPrimary,
                legalGlowSecondary,
                colorBlend);
            outline.effectDistance = new Vector2(
                Mathf.Lerp(3.5f, 7f, pulse),
                -Mathf.Lerp(3.5f, 7f, pulse));
            outline.effectColor = new Color(
                color.r,
                color.g,
                color.b,
                Mathf.Lerp(0.58f, 1f, pulse));
        }

        private void ApplyPose()
        {
            if (rect == null || dragging) return;
            Vector2 target = restPosition;
            float scale = 1f;
            float angle = restAngle;
            if (hovered)
            {
                target += new Vector2(0f, HoverLift);
                scale = HoverScale;
                angle = 0f;
            }
            if (selected)
            {
                target += new Vector2(0f, SelectedLift);
                scale = SelectedScale;
                angle = 0f;
            }
            if (poseRoutine != null) StopCoroutine(poseRoutine);
            poseRoutine = StartCoroutine(AnimatePose(target, scale, angle));
        }

        private IEnumerator AnimatePose(
            Vector2 position,
            float scale,
            float angle)
        {
            Vector2 fromPosition = rect.anchoredPosition;
            Vector3 fromScale = rect.localScale;
            Quaternion fromRotation = rect.localRotation;
            float fromAlpha =
                canvasGroup != null ? canvasGroup.alpha : 1f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            const float duration = 0.14f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                rect.anchoredPosition =
                    Vector2.Lerp(fromPosition, position, t);
                rect.localScale =
                    Vector3.Lerp(fromScale, Vector3.one * scale, t);
                rect.localRotation =
                    Quaternion.Slerp(fromRotation, targetRotation, t);
                if (entering && canvasGroup != null &&
                    !presentationHidden)
                    canvasGroup.alpha = Mathf.Lerp(fromAlpha, 1f, t);
                yield return null;
            }
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one * scale;
            rect.localRotation = targetRotation;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = presentationHidden ? 0f : 1f;
                canvasGroup.blocksRaycasts =
                    !presentationHidden && interactive;
            }
            entering = false;
            poseRoutine = null;
        }
    }
}
