using System;
using System.Collections.Generic;
using ArcaneArena.Cards;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private const string StarterCatalogResourcePath =
            "StarterDecks/StarterDeckCatalog";

        private StarterDeckCatalog _starterDeckCatalog;
        private Image _starterDetailArtwork;
        private Text _starterDetailName;
        private Text _starterDetailType;
        private Text _starterDetailEffect;
        private GameObject _starterClaimModal;
        private float _starterGalleryScroll = 1f;

        private void ShowStarterDeckSelection()
        {
            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = null;
            _starterDeckCatalog ??=
                Resources.Load<StarterDeckCatalog>(StarterCatalogResourcePath);
            BuildShopBackground("ESCOLHA SEU DECK INICIAL");

            Image titleSurface = CreateArcaneSurface(
                _screenRoot,
                "Faixa da Escolha Inicial",
                new Vector2(0.19f, 0.885f),
                new Vector2(0.81f, 0.972f),
                ArcaneGold,
                true,
                0.78f);
            CreateText(
                titleSurface.transform,
                "ESCOLHA SEU DECK INICIAL",
                31,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                "A escolha é gratuita, definitiva e acontece uma única vez neste perfil.",
                17,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.08f, 0.84f),
                new Vector2(0.92f, 0.89f),
                TextAnchor.MiddleCenter);

            if (_starterDeckCatalog == null)
            {
                CreateText(
                    _screenRoot,
                    "Catalogo de decks iniciais ausente. Execute a sincronizacao de conteudo.",
                    22,
                    FontStyle.Bold,
                    Danger,
                    new Vector2(0.12f, 0.35f),
                    new Vector2(0.88f, 0.65f),
                    TextAnchor.MiddleCenter);
                return;
            }

            int columns = Screen.width < 760
                ? 1
                : Screen.width < 1280
                    ? 2
                    : 3;
            float tileWidth = columns == 3
                ? 470f
                : columns == 2
                    ? 650f
                    : 920f;
            RectTransform grid = CreateScrollGrid(
                _screenRoot,
                "Seis Decks Iniciais",
                new Vector2(0.055f, 0.075f),
                new Vector2(0.945f, 0.825f),
                new Vector2(tileWidth, 285f),
                new Vector2(22f, 22f),
                columns);
            foreach (StarterDeckDefinition definition in
                     _starterDeckCatalog.Decks)
            {
                CreateStarterDeckTile(grid, definition);
            }
            ScrollRect galleryScroll = grid.GetComponentInParent<ScrollRect>();
            if (galleryScroll != null)
                galleryScroll.verticalNormalizedPosition = _starterGalleryScroll;
        }

        private void CreateStarterDeckTile(
            Transform parent,
            StarterDeckDefinition definition)
        {
            if (definition == null)
                return;

            Color accent = definition.IsPublishable ? Cyan : Danger;
            Image tile = CreateArcaneSurface(
                parent,
                definition.DisplayName,
                Vector2.zero,
                Vector2.one,
                accent,
                true,
                0.78f);

            CreateText(tile.transform, definition.DisplayName, 20,
                FontStyle.Bold, Color.white,
                new Vector2(0.04f, 0.82f), new Vector2(0.96f, 0.97f),
                TextAnchor.MiddleCenter);

            for (int index = 0;
                 index < Mathf.Min(3, definition.PreviewCardIds.Count);
                 index++)
            {
                string cardId = definition.PreviewCardIds[index];
                CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
                float left = 0.08f + index * 0.295f;
                Image artwork = CreateCardArtwork(
                    tile.transform,
                    entry?.Artwork,
                    new Vector2(left, 0.22f),
                    new Vector2(left + 0.25f, 0.79f),
                    0f,
                    false);
                AddBanlistBadge(artwork.transform, cardId);
            }

            string counts =
                $"PRINCIPAL {definition.MainDeck.Count}  |  " +
                $"EXTRA {definition.ExtraDeck.Count}  |  " +
                $"SIDE {definition.SideDeck.Count}";
            CreateText(tile.transform, counts, 12, FontStyle.Bold, Muted,
                new Vector2(0.03f, 0.105f), new Vector2(0.97f, 0.21f),
                TextAnchor.MiddleCenter);
            CreateText(
                tile.transform,
                definition.IsPublishable
                    ? "VALIDADO - VER DETALHES"
                    : "AGUARDA SUBSTITUICAO APROVADA",
                12,
                FontStyle.Bold,
                accent,
                new Vector2(0.03f, 0.015f),
                new Vector2(0.97f, 0.105f),
                TextAnchor.MiddleCenter);
            AddButtonBehaviour(tile, () =>
            {
                ScrollRect scroll = tile.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                    _starterGalleryScroll = scroll.verticalNormalizedPosition;
                ShowStarterDeckDetails(definition, "Main");
            });
            Button tileButton = tile.GetComponent<Button>();
            ArcanePanelSheenGraphic tileSheen =
                tile.GetComponentInChildren<ArcanePanelSheenGraphic>();
            if (tileButton != null && tileSheen != null)
                tileButton.targetGraphic = tileSheen;
        }

        private void ShowStarterDeckDetails(
            StarterDeckDefinition definition,
            string section)
        {
            if (definition == null)
            {
                ShowStarterDeckSelection();
                return;
            }

            SetDuelPresentation(false);
            ClearScreen();
            _shopBackAction = ShowStarterDeckSelection;
            BuildShopBackground("DETALHES DO DECK INICIAL");
            BuildProfessionalShopHeader(
                definition.DisplayName,
                ShowStarterDeckSelection);
            CreateText(
                _screenRoot,
                $"COMPOSIÇÃO VALIDADA PELO CORE  •  BANLIST {definition.BanlistVersion}",
                14,
                FontStyle.Bold,
                new Color(0.65f, 0.90f, 0.96f, 0.95f),
                new Vector2(0.31f, 0.83f),
                new Vector2(0.965f, 0.885f),
                TextAnchor.MiddleCenter);

            Image detail = CreateArcaneSurface(
                _screenRoot,
                "Detalhes da Carta",
                new Vector2(0.035f, 0.115f),
                new Vector2(0.285f, 0.82f),
                ArcaneGold,
                false,
                0.82f);
            _starterDetailArtwork = CreateCardArtwork(
                detail.transform, null,
                new Vector2(0.13f, 0.47f), new Vector2(0.87f, 0.955f),
                0f, false);
            _starterDetailArtwork.color = Color.clear;
            AddOutline(
                _starterDetailArtwork.gameObject,
                new Color(ArcaneGold.r, ArcaneGold.g, ArcaneGold.b, 0.76f),
                new Vector2(2f, -2f));
            var artworkButton =
                _starterDetailArtwork.gameObject.AddComponent<Button>();
            artworkButton.targetGraphic = _starterDetailArtwork;
            artworkButton.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                OpenDeckEditorZoom();
            });
            _starterDetailName = CreateText(
                detail.transform, "Selecione uma carta", 19,
                FontStyle.Bold, Color.white,
                new Vector2(0.06f, 0.385f), new Vector2(0.94f, 0.47f),
                TextAnchor.MiddleCenter);
            _starterDetailType = CreateText(
                detail.transform, string.Empty, 13,
                FontStyle.Bold, Cyan,
                new Vector2(0.06f, 0.325f), new Vector2(0.94f, 0.39f),
                TextAnchor.MiddleCenter);
            _starterDetailEffect = CreateScrollableText(
                detail.transform,
                "Efeito da Carta Inicial",
                new Vector2(0.055f, 0.035f),
                new Vector2(0.945f, 0.315f),
                13);

            CreateStarterSectionButton(definition, "Main", "PRINCIPAL",
                new Vector2(0.31f, 0.75f), new Vector2(0.50f, 0.82f), section);
            CreateStarterSectionButton(definition, "Extra", "EXTRA",
                new Vector2(0.515f, 0.75f), new Vector2(0.705f, 0.82f), section);
            CreateStarterSectionButton(definition, "Side", "SIDE",
                new Vector2(0.72f, 0.75f), new Vector2(0.91f, 0.82f), section);

            IReadOnlyList<string> cards = StarterSection(definition, section);
            RectTransform grid = CreateScrollGrid(
                _screenRoot,
                "Cartas do " + section,
                new Vector2(0.31f, 0.22f),
                new Vector2(0.965f, 0.73f),
                new Vector2(145f, 210f),
                new Vector2(14f, 14f),
                7);
            Image gridViewport = grid.GetComponentInParent<Image>();
            if (gridViewport != null)
            {
                AddOutline(
                    gridViewport.gameObject,
                    new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.66f),
                    new Vector2(2f, -2f));
            }
            foreach (string cardId in cards)
            {
                CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
                Image artwork = CreateCardArtwork(
                    grid, entry?.Artwork, Vector2.zero, Vector2.one, 0f, false);
                AddBanlistBadge(artwork.transform, cardId);
                artwork.raycastTarget = true;
                AddButtonBehaviour(artwork,
                    () => ShowStarterCardDetails(cardId));
            }

            if (cards.Count > 0)
                ShowStarterCardDetails(cards[0]);

            Color claimColor = definition.IsPublishable ? Lime : Danger;
            CreateArcaneActionButton(
                _screenRoot,
                definition.IsPublishable
                    ? "ESCOLHER ESTE DECK"
                    : "DECK INDISPONIVEL",
                new Vector2(0.40f, 0.09f),
                new Vector2(0.75f, 0.175f),
                claimColor,
                () =>
                {
                    if (definition.IsPublishable)
                        ShowStarterClaimConfirmation(definition);
                },
                20);
            if (!definition.IsPublishable)
            {
                string issue = definition.ValidationIssues.Count > 0
                    ? string.Join(" ", definition.ValidationIssues)
                    : "A composicao ainda nao e legal na banlist ativa.";
                CreateText(_screenRoot, issue, 13, FontStyle.Bold, Danger,
                    new Vector2(0.31f, 0.015f), new Vector2(0.91f, 0.085f),
                    TextAnchor.MiddleCenter);
            }

            _deckEditorDetailArtwork = _starterDetailArtwork;
            BuildDeckEditorZoomViewer(
                "Visualizador Ampliado do Deck Inicial");
        }

        private void CreateStarterSectionButton(
            StarterDeckDefinition definition,
            string section,
            string label,
            Vector2 min,
            Vector2 max,
            string selected)
        {
            CreateArcaneTabButton(
                _screenRoot,
                $"{label} {StarterSection(definition, section).Count}",
                min,
                max,
                string.Equals(section, selected, StringComparison.Ordinal),
                () => ShowStarterDeckDetails(definition, section));
        }

        private static IReadOnlyList<string> StarterSection(
            StarterDeckDefinition definition,
            string section)
        {
            if (string.Equals(section, "Extra", StringComparison.Ordinal))
                return definition.ExtraDeck;
            if (string.Equals(section, "Side", StringComparison.Ordinal))
                return definition.SideDeck;
            return definition.MainDeck;
        }

        private void ShowStarterCardDetails(string cardId)
        {
            CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
            if (entry == null)
                return;

            if (_starterDetailArtwork != null)
            {
                _starterDetailArtwork.sprite = entry.Artwork;
                _starterDetailArtwork.color = entry.Artwork != null
                    ? Color.white
                    : Color.clear;
                RefreshBanlistBadge(_starterDetailArtwork.transform, cardId);
            }
            if (_starterDetailName != null)
                _starterDetailName.text = entry.DisplayName;
            if (_starterDetailType != null)
                _starterDetailType.text = StarterCardTypeSummary(entry);
            if (_starterDetailEffect != null)
                _starterDetailEffect.text = CardPresentationText.EffectPtBr(entry);
        }

        private static string StarterCardTypeSummary(CardCatalogEntry entry)
        {
            if (entry == null)
                return string.Empty;
            if (entry.Category == CardCategory.Monster &&
                !string.IsNullOrWhiteSpace(entry.RaceName))
            {
                return $"{entry.RaceName}  •  {entry.TypeName}";
            }
            return entry.TypeName;
        }

        private void ShowStarterClaimConfirmation(
            StarterDeckDefinition definition)
        {
            Image veil = CreatePanel(
                _screenRoot,
                "Confirmacao do Deck Inicial",
                Vector2.zero,
                Vector2.one,
                new Color(0f, 0f, 0f, 0.82f));
            _starterClaimModal = veil.gameObject;
            veil.transform.SetAsLastSibling();
            Image modal = CreateArcaneSurface(
                veil.transform,
                "Escolha Unica",
                new Vector2(0.27f, 0.27f),
                new Vector2(0.73f, 0.72f),
                ArcaneGold,
                true,
                0.94f);
            CreateText(modal.transform, "CONFIRMAR DECK INICIAL", 28,
                FontStyle.Bold, Color.white,
                new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.91f),
                TextAnchor.MiddleCenter);
            CreateText(
                modal.transform,
                $"{definition.DisplayName}\n\nGRÁTIS E UMA ÚNICA VEZ\nEsta escolha não poderá ser trocada depois.",
                18,
                FontStyle.Bold,
                Gold,
                new Vector2(0.09f, 0.35f),
                new Vector2(0.91f, 0.72f),
                TextAnchor.MiddleCenter);
            Text feedback = CreateText(
                modal.transform, string.Empty, 13,
                FontStyle.Bold, Danger,
                new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.35f),
                TextAnchor.MiddleCenter);
            CreateArcaneActionButton(modal.transform, "CANCELAR",
                new Vector2(0.08f, 0.08f), new Vector2(0.44f, 0.24f),
                Muted, () =>
                {
                    Destroy(veil.gameObject);
                    _starterClaimModal = null;
                });
            CreateArcaneActionButton(modal.transform, "CONFIRMAR",
                new Vector2(0.56f, 0.08f), new Vector2(0.92f, 0.24f),
                Lime, () =>
                {
                    if (_repository.TryClaimStarterDeck(
                            definition,
                            _starterDeckCatalog,
                            out _,
                            out string rejection))
                    {
                        _shopBackAction = null;
                        ShowMainMenu();
                        return;
                    }

                    feedback.text = rejection;
                });
        }
    }
}
