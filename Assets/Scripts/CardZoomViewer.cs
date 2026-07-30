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
        [SerializeField] private RectTransform artwork;
        private Vector2 dragOrigin;
        private Vector2 artOrigin;
        private float zoom = 1f;

        public void Setup(RectTransform target)
        {
            artwork = target;
            ResetView();
        }

        public void ResetView()
        {
            zoom = 1f;
            if (artwork == null) return;
            artwork.localScale = Vector3.one;
            artwork.anchoredPosition = Vector2.zero;
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (artwork == null) return;
            zoom = Mathf.Clamp(
                zoom + Mathf.Sign(eventData.scrollDelta.y) * 0.18f,
                1f,
                3f);
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
            if (eventData.clickCount > 1) ResetView();
        }
    }
}
