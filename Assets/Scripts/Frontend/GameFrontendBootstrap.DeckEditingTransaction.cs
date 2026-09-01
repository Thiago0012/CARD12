using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private DeckRecord _editingDeckSource;
        private bool _editingDeckDirty;
        private GameObject _deckEditorUnsavedModal;

        private void BeginDeckEditingSession(DeckRecord source)
        {
            if (source == null)
                return;

            if (ReferenceEquals(source, _editingDeck) &&
                _editingDeckSource != null)
            {
                return;
            }

            _editingDeckSource = source;
            _editingDeck = CloneDeckRecord(source);
            _editingDeckDirty = false;
        }

        private void ResetDeckEditingSession()
        {
            CloseDeckEditorUnsavedModal();
            _editingDeck = null;
            _editingDeckSource = null;
            _editingDeckDirty = false;
        }

        private void MarkDeckEditorDirty()
        {
            _editingDeckDirty = true;
        }

        private bool TrySaveDeckEditorChanges(bool showStatus)
        {
            if (_editingDeck == null || _editingDeckSource == null)
                return false;

            _editingDeck.Normalize();
            if (!DeckRepository.TryValidateForDuel(
                    _editingDeck,
                    _catalog,
                    out string rejection))
            {
                if (showStatus)
                {
                    SetEditorStatus(
                        $"Não foi possível salvar: {rejection}",
                        Danger);
                }
                return false;
            }

            CopyDeckRecord(_editingDeck, _editingDeckSource);
            _editingDeckSource.RefreshFeaturedCards();
            _repository.Save();
            _editingDeckDirty = false;

            if (showStatus)
                SetEditorStatus("Alterações salvas.", Lime);
            return true;
        }

        private void RequestCloseDeckEditor()
        {
            if (_editingDeckSource == null)
            {
                ShowDeckGallery();
                return;
            }

            if (!_editingDeckDirty)
            {
                DeckRecord source = _editingDeckSource;
                ResetDeckEditingSession();
                ShowDeckDetails(source);
                return;
            }

            ShowDeckEditorUnsavedModal();
        }

        private void ShowDeckEditorUnsavedModal()
        {
            if (_deckEditorUnsavedModal != null || _screenRoot == null)
                return;

            Image veil = CreatePanel(
                _screenRoot,
                "Alterações não salvas",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.86f));
            veil.raycastTarget = true;
            veil.transform.SetAsLastSibling();
            _deckEditorUnsavedModal = veil.gameObject;

            Image modal = CreatePanel(
                veil.transform,
                "Salvar alterações do deck",
                new Vector2(0.27f, 0.29f),
                new Vector2(0.73f, 0.71f),
                new Color(0.012f, 0.035f, 0.065f, 0.995f));
            AddOutline(
                modal.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f),
                new Vector2(3f, -3f));

            CreateText(
                modal.transform,
                "ALTERAÇÕES NÃO SALVAS",
                27,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.74f),
                new Vector2(0.93f, 0.91f),
                TextAnchor.MiddleCenter);
            Text feedback = CreateText(
                modal.transform,
                "Quer salvar as alterações que você fez?\nO Deck Principal precisa ter entre 40 e 60 cartas.",
                16,
                FontStyle.Bold,
                Muted,
                new Vector2(0.09f, 0.39f),
                new Vector2(0.91f, 0.72f),
                TextAnchor.MiddleCenter);

            CreateButton(
                modal.transform,
                "CANCELAR",
                new Vector2(0.05f, 0.09f),
                new Vector2(0.32f, 0.26f),
                Muted,
                CloseDeckEditorUnsavedModal);
            CreateButton(
                modal.transform,
                "DESCARTAR",
                new Vector2(0.365f, 0.09f),
                new Vector2(0.635f, 0.26f),
                Danger,
                () =>
                {
                    DeckRecord source = _editingDeckSource;
                    ResetDeckEditingSession();
                    ShowDeckDetails(source);
                });
            CreateButton(
                modal.transform,
                "SALVAR",
                new Vector2(0.68f, 0.09f),
                new Vector2(0.95f, 0.26f),
                Lime,
                () =>
                {
                    if (!TrySaveDeckEditorChanges(false))
                    {
                        DeckRepository.TryValidateForDuel(
                            _editingDeck,
                            _catalog,
                            out string rejection);
                        feedback.text = $"Não foi possível salvar: {rejection}";
                        feedback.color = Danger;
                        return;
                    }

                    DeckRecord source = _editingDeckSource;
                    ResetDeckEditingSession();
                    ShowDeckDetails(source);
                });
        }

        private void CloseDeckEditorUnsavedModal()
        {
            if (_deckEditorUnsavedModal != null)
                Destroy(_deckEditorUnsavedModal);
            _deckEditorUnsavedModal = null;
        }

        private static DeckRecord CloneDeckRecord(DeckRecord source)
        {
            var clone = new DeckRecord();
            CopyDeckRecord(source, clone);
            return clone;
        }

        private static void CopyDeckRecord(DeckRecord source, DeckRecord target)
        {
            target.deckId = source.deckId;
            target.displayName = source.displayName;
            target.caseTheme = source.caseTheme;
            target.mainDeckCardIds = new List<string>(
                source.mainDeckCardIds ?? new List<string>());
            target.extraDeckCardIds = new List<string>(
                source.extraDeckCardIds ?? new List<string>());
            target.sideDeckCardIds = new List<string>(
                source.sideDeckCardIds ?? new List<string>());
            target.featuredCardIds = new List<string>(
                source.featuredCardIds ?? new List<string>());
        }
    }
}
