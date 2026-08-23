using UnityEngine;
using UnityEngine.EventSystems;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Interação das cartas já colocadas no deck. Um clique seleciona,
    /// duplo clique remove e arrastar de volta ao catálogo também remove.
    /// </summary>
    public sealed class DeckEditorDeckCardDrag :
        MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler
    {
        private GameFrontendBootstrap _frontend;
        private string _cardId;
        private Sprite _sprite;
        private bool _extraDeck;
        private int _index;
        private bool _dragging;

        public void Setup(
            GameFrontendBootstrap frontend,
            string cardId,
            Sprite sprite,
            bool extraDeck,
            int index)
        {
            _frontend = frontend;
            _cardId = cardId;
            _sprite = sprite;
            _extraDeck = extraDeck;
            _index = index;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _frontend?.ShowDeckEditorCardDetails(_cardId);
            _frontend?.BeginDeckCardDrag(_sprite, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging)
                _frontend?.MoveCatalogCardDrag(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
                return;

            _frontend?.EndDeckCardDrag(
                _extraDeck,
                _index,
                _sprite,
                eventData.position);
            _dragging = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_dragging)
                return;

            _frontend?.ShowDeckEditorCardDetails(_cardId);
            if (eventData.clickCount >= 2)
            {
                _frontend?.QuickRemoveDeckCard(
                    _extraDeck,
                    _index,
                    _sprite,
                    transform as RectTransform);
            }
        }
    }
}
