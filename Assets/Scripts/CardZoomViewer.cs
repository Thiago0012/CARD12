using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcaneArena
{
    public sealed class CardZoomViewer :
        MonoBehaviour,
        IScrollHandler,
        IBeginDragHandler,
        IDragHandler,
        IPointerClickHandler
    {
        private const float ZoomStep = 0.12f;
        private const float MaximumZoom = 1.75f;
        [SerializeField] private RectTransform artwork;
        private Vector2 dragOrigin;
        private Vector2 artOrigin;
        private float zoom = 1f;
        private Coroutine zoomRoutine;

        public void Setup(RectTransform target)
        {
            artwork = target;
            ResetView();
        }

        public void ResetView()
        {
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }
            zoom = 1f;
            if (artwork == null) return;
            artwork.localScale = Vector3.one;
            artwork.anchoredPosition = Vector2.zero;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (artwork == null) return;
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }
            zoom = Mathf.Clamp(
                zoom + Mathf.Sign(eventData.scrollDelta.y) * ZoomStep,
                1f,
                MaximumZoom);
            artwork.localScale = Vector3.one * zoom;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragOrigin = eventData.position;
            artOrigin = artwork != null
                ? artwork.anchoredPosition
                : Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (artwork != null)
                artwork.anchoredPosition =
                    artOrigin + eventData.position - dragOrigin;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (artwork == null || eventData.dragging)
                return;
            float target = zoom > 1.05f ? 1f : MaximumZoom;
            if (zoomRoutine != null)
                StopCoroutine(zoomRoutine);
            zoomRoutine = StartCoroutine(AnimateZoom(target));
        }

        private IEnumerator AnimateZoom(float target)
        {
            float startZoom = zoom;
            Vector2 startPosition = artwork.anchoredPosition;
            Vector2 targetPosition = target <= 1.05f
                ? Vector2.zero
                : startPosition;
            const float duration = 0.18f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                zoom = Mathf.Lerp(startZoom, target, t);
                artwork.localScale = Vector3.one * zoom;
                artwork.anchoredPosition = Vector2.Lerp(
                    startPosition,
                    targetPosition,
                    t);
                yield return null;
            }
            zoom = target;
            artwork.localScale = Vector3.one * zoom;
            artwork.anchoredPosition = targetPosition;
            zoomRoutine = null;
        }
    }
}
