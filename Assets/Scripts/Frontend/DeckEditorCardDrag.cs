using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed class DeckEditorCardDrag :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IInitializePotentialDragHandler,
        IPointerDownHandler,
        IPointerClickHandler
    {
        private GameFrontendBootstrap _frontend;
        private string _cardId;
        private Sprite _sprite;
        private bool _canUse;
        private bool _dragging;
        private bool _scrolling;
        private bool _suppressClick;
        private ScrollRect _parentScroll;

        public void Setup(
            GameFrontendBootstrap frontend,
            string cardId,
            Sprite sprite,
            bool canUse)
        {
            _frontend = frontend;
            _cardId = cardId;
            _sprite = sprite;
            _canUse = canUse;
            _parentScroll = GetComponentInParent<ScrollRect>();
        }

        public static bool PrefersCatalogScroll(Vector2 gesture)
        {
            if (gesture.sqrMagnitude < 0.01f)
                return true;
            return Mathf.Abs(gesture.y) >= Mathf.Abs(gesture.x) * 0.8f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _suppressClick = false;
            _scrolling = false;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            _parentScroll?.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Vector2 gesture = eventData.position - eventData.pressPosition;
            if (gesture.sqrMagnitude < 0.01f)
                gesture = eventData.delta;
            if (_parentScroll != null &&
                (!_canUse || PrefersCatalogScroll(gesture)))
            {
                _scrolling = true;
                _dragging = false;
                _suppressClick = true;
                _parentScroll.OnBeginDrag(eventData);
                return;
            }

            _frontend?.ShowDeckEditorCardDetails(_cardId);
            if (!_canUse)
            {
                _dragging = false;
                return;
            }

            _dragging = true;
            _frontend?.BeginCatalogCardDrag(
                _cardId,
                _sprite,
                eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_scrolling)
            {
                _parentScroll?.OnDrag(eventData);
                return;
            }
            if (!_dragging)
                return;
            _frontend?.MoveCatalogCardDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_scrolling)
            {
                _parentScroll?.OnEndDrag(eventData);
                _scrolling = false;
                _suppressClick = true;
                return;
            }
            if (!_dragging)
                return;
            _frontend?.EndCatalogCardDrag(
                _cardId,
                eventData.position);
            _dragging = false;
            _suppressClick = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dragging || _scrolling || _suppressClick)
                return;

            _frontend?.ShowDeckEditorCardDetails(_cardId);
            if (eventData.clickCount >= 2)
            {
                if (_canUse)
                    _frontend?.QuickAddCatalogCard(
                        _cardId,
                        transform as RectTransform);
                else
                    _frontend?.NotifyLockedCatalogCard(_cardId);
            }
        }
    }
}
