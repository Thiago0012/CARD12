using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private GameObject _deckDeleteModal;

        private Image CreateDeckDeleteControl(
            Transform parent,
            DeckRecord deck)
        {
            Image control = CreatePanel(
                parent,
                $"Excluir {deck?.deckId}",
                new Vector2(0.82f, 0.79f),
                new Vector2(0.965f, 0.965f),
                new Color(0.19f, 0.025f, 0.045f, 0.98f));
            AddOutline(
                control.gameObject,
                new Color(Danger.r, Danger.g, Danger.b, 0.95f),
                new Vector2(2f, -2f));

            Image body = CreatePanel(
                control.transform,
                "Corpo da lixeira",
                new Vector2(0.31f, 0.20f),
                new Vector2(0.69f, 0.67f),
                Color.white);
            Image lid = CreatePanel(
                control.transform,
                "Tampa da lixeira",
                new Vector2(0.23f, 0.70f),
                new Vector2(0.77f, 0.82f),
                Color.white);
            Image handle = CreatePanel(
                control.transform,
                "Alça da lixeira",
                new Vector2(0.41f, 0.82f),
                new Vector2(0.59f, 0.91f),
                Color.white);
            Image slotLeft = CreatePanel(
                body.transform,
                "Ranhura esquerda",
                new Vector2(0.28f, 0.18f),
                new Vector2(0.39f, 0.82f),
                new Color(0.19f, 0.025f, 0.045f, 1f));
            Image slotRight = CreatePanel(
                body.transform,
                "Ranhura direita",
                new Vector2(0.61f, 0.18f),
                new Vector2(0.72f, 0.82f),
                new Color(0.19f, 0.025f, 0.045f, 1f));
            body.raycastTarget = false;
            lid.raycastTarget = false;
            handle.raycastTarget = false;
            slotLeft.raycastTarget = false;
            slotRight.raycastTarget = false;

            AddButtonBehaviour(
                control,
                () => ShowDeckDeleteConfirmation(deck));
            return control;
        }

        private void ShowDeckDeleteConfirmation(DeckRecord deck)
        {
            if (deck == null || _repository?.State?.decks == null)
                return;

            if (_deckDeleteModal != null)
                Destroy(_deckDeleteModal);

            Image veil = CreatePanel(
                _screenRoot,
                "Confirmação de exclusão do deck",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.84f));
            veil.raycastTarget = true;
            veil.transform.SetAsLastSibling();
            _deckDeleteModal = veil.gameObject;

            Image modal = CreatePanel(
                veil.transform,
                "Excluir deck",
                new Vector2(0.285f, 0.275f),
                new Vector2(0.715f, 0.715f),
                new Color(0.015f, 0.035f, 0.065f, 0.995f));
            AddOutline(
                modal.gameObject,
                new Color(Danger.r, Danger.g, Danger.b, 0.95f),
                new Vector2(3f, -3f));

            CreateText(
                modal.transform,
                "EXCLUIR DECK?",
                30,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.74f),
                new Vector2(0.92f, 0.91f),
                TextAnchor.MiddleCenter);
            string selectionNotice = _repository.IsSelected(deck)
                ? "\n\nEste é o deck ativo. Outro deck disponível será selecionado automaticamente."
                : string.Empty;
            CreateText(
                modal.transform,
                $"Deseja excluir \"{deck.displayName}\"?\nEsta ação não pode ser desfeita.{selectionNotice}",
                17,
                FontStyle.Bold,
                Muted,
                new Vector2(0.10f, 0.34f),
                new Vector2(0.90f, 0.73f),
                TextAnchor.MiddleCenter);
            Text feedback = CreateText(
                modal.transform,
                string.Empty,
                14,
                FontStyle.Bold,
                Danger,
                new Vector2(0.08f, 0.25f),
                new Vector2(0.92f, 0.34f),
                TextAnchor.MiddleCenter);

            CreateButton(
                modal.transform,
                "CANCELAR",
                new Vector2(0.08f, 0.08f),
                new Vector2(0.46f, 0.24f),
                Muted,
                CloseDeckDeleteModal);
            CreateButton(
                modal.transform,
                "EXCLUIR",
                new Vector2(0.54f, 0.08f),
                new Vector2(0.92f, 0.24f),
                Danger,
                () =>
                {
                    if (_repository.TryDeleteDeck(
                            deck.deckId,
                            out string rejection))
                    {
                        _deckDeleteModal = null;
                        _selectedDeck = null;
                        _editingDeck = null;
                        ShowDeckGallery();
                        return;
                    }

                    feedback.text = rejection;
                });
        }

        private void CloseDeckDeleteModal()
        {
            if (_deckDeleteModal != null)
                Destroy(_deckDeleteModal);
            _deckDeleteModal = null;
        }
    }
}
