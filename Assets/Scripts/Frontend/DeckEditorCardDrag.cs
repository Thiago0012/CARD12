using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcaneArena.Frontend
{
    public sealed class DeckEditorCardDrag :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerEnterHandler,
        IPointerClickHandler
    {
        private GameFrontendBootstrap _frontend;
        private string _cardId;
        private Sprite _sprite;
        private bool _canUse;
        private bool _dragging;

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
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _frontend?.ShowDeckEditorCardDetails(_cardId);
            if (!_canUse)
            {
                _frontend?.NotifyLockedCatalogCard(_cardId);
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
            if (!_dragging)
                return;
            _frontend?.MoveCatalogCardDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
                return;
            _frontend?.EndCatalogCardDrag(
                _cardId,
                eventData.position);
            _dragging = false;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _frontend?.ShowDeckEditorCardDetails(_cardId);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dragging)
                return;

            _frontend?.ShowDeckEditorCardDetails(_cardId);
            if (eventData.clickCount >= 2)
            {
                if (_canUse)
                    _frontend?.QuickAddCatalogCard(_cardId);
                else
                    _frontend?.NotifyLockedCatalogCard(_cardId);
            }
        }
    }
}
