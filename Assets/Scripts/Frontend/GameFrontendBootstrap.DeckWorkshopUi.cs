using System;
using ArcaneArena.Cards;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Identidade visual da Oficina de Decks do Master Duel 2 Plus Ultra.
    /// A composição usa duas colunas calculadas (35% / 57%, com 2% de vão)
    /// e uma biblioteca cuja largura útil é dividida somente após descontar
    /// padding e espaçamento. Isso mantém o mesmo encaixe em PC e Android.
    /// </summary>
    public sealed partial class GameFrontendBootstrap
    {
        private static readonly Color DeckEmerald =
            new(0.16f, 0.88f, 0.57f, 1f);
        private static readonly Color DeckMint =
            new(0.47f, 1f, 0.76f, 1f);
        private static readonly Color DeckAmber =
            new(0.96f, 0.67f, 0.24f, 1f);
        private static readonly Color DeckGraphite =
            new(0.012f, 0.032f, 0.035f, 1f);
        private static readonly Color DeckMuted =
            new(0.58f, 0.72f, 0.68f, 1f);

        private void BuildDeckWorkshopGallery()
        {
            BuildDeckWorkshopBackground("BIBLIOTECA DE DECKS");
            int deckCount = _repository?.State?.decks?.Count ?? 0;
            BuildDeckWorkshopHeader(
                "OFICINA DE DECKS",
                "MONTE, ORGANIZE E ESCOLHA SEU ARSENAL",
                $"{deckCount:00}  DECKS",
                ReturnToMainMenuScene);

            DeckRecord activeDeck = ResolveActiveWorkshopDeck();
            BuildDeckWorkshopHero(activeDeck);
            BuildDeckWorkshopLibrary(deckCount);
        }

        private void BuildDeckWorkshopDetails(DeckRecord deck)
        {
            BuildDeckWorkshopBackground("CONFIGURAÇÃO DO DECK");
            BuildDeckWorkshopHeader(
                deck.displayName,
                "VISUALIZAÇÃO, VALIDAÇÃO E PREPARAÇÃO PARA O DUELO",
                "STANDARD",
                ShowDeckGallery);

            bool selected = _repository.IsSelected(deck);
            bool legal = DeckRepository.TryValidateForDuel(
                deck,
                _catalog,
                out string rejection);
            Color accent = selected ? DeckAmber : DeckEmerald;

            Image workspace = CreateArcaneSurface(
                _screenRoot,
                "Mesa de configuração do deck",
                new Vector2(0.035f, 0.075f),
                new Vector2(0.965f, 0.855f),
                accent,
                true,
                0.93f);

            CreateText(
                workspace.transform,
                selected ? "DECK ATIVO" : "DECK DA COLEÇÃO",
                13,
                FontStyle.Bold,
                selected ? DeckAmber : DeckEmerald,
                new Vector2(0.035f, 0.91f),
                new Vector2(0.48f, 0.975f),
                TextAnchor.MiddleLeft);
            CreateText(
                workspace.transform,
                deck.displayName,
                31,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.81f),
                new Vector2(0.48f, 0.92f),
                TextAnchor.MiddleLeft);

            Image display = CreateArcaneSurface(
                workspace.transform,
                "Expositor do deck",
                new Vector2(0.035f, 0.19f),
                new Vector2(0.515f, 0.80f),
                DeckEmerald,
                false,
                0.68f);
            CreateWorkshopEnergyLines(display.transform);
            CreateDeckCaseVisual(
                display.transform,
                deck.caseTheme,
                new Vector2(0.06f, 0.18f),
                new Vector2(0.39f, 0.85f));
            CreateFeaturedCards(
                display.transform,
                deck,
                new Vector2(0.31f, 0.12f),
                new Vector2(0.93f, 0.88f));

            Image information = CreateArcaneSurface(
                workspace.transform,
                "Dados do deck",
                new Vector2(0.535f, 0.19f),
                new Vector2(0.965f, 0.80f),
                legal ? DeckEmerald : DeckAmber,
                false,
                0.78f);
            CreateText(
                information.transform,
                "CONFIGURAÇÃO",
                13,
                FontStyle.Bold,
                DeckMuted,
                new Vector2(0.06f, 0.86f),
                new Vector2(0.94f, 0.96f),
                TextAnchor.MiddleLeft);
            CreateText(
                information.transform,
                "STANDARD",
                24,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.06f, 0.73f),
                new Vector2(0.94f, 0.87f),
                TextAnchor.MiddleLeft);

            CreateDeckWorkshopMetric(
                information.transform,
                "DECK PRINCIPAL",
                $"{deck.mainDeckCardIds.Count} / {MainDeckMinimum}–{MainDeckMaximum}",
                new Vector2(0.06f, 0.49f),
                new Vector2(0.47f, 0.70f),
                deck.mainDeckCardIds.Count >= MainDeckMinimum
                    ? DeckEmerald
                    : DeckAmber);
            CreateDeckWorkshopMetric(
                information.transform,
                "DECK ADICIONAL",
                $"{deck.extraDeckCardIds.Count} / {ExtraDeckMaximum}",
                new Vector2(0.53f, 0.49f),
                new Vector2(0.94f, 0.70f),
                DeckMint);

            Text selectionStatus = CreateText(
                information.transform,
                legal
                    ? selected
                        ? "PRONTO • selecionado para o próximo duelo"
                        : "PRONTO • escolha este deck para o próximo duelo"
                    : $"REVISAR DECK • {rejection}",
                14,
                FontStyle.Bold,
                legal ? (selected ? DeckAmber : DeckEmerald) : Danger,
                new Vector2(0.06f, 0.25f),
                new Vector2(0.94f, 0.44f),
                TextAnchor.MiddleLeft);

            CreateText(
                information.transform,
                "As cartas de destaque representam visualmente a identidade do deck. " +
                "Use o editor para alterar a lista, o porta-deck e a seleção principal.",
                13,
                FontStyle.Normal,
                DeckMuted,
                new Vector2(0.06f, 0.07f),
                new Vector2(0.94f, 0.24f),
                TextAnchor.UpperLeft);

            CreateArcaneActionButton(
                workspace.transform,
                selected ? "✓  DECK SELECIONADO" : "USAR NO DUELO",
                new Vector2(0.535f, 0.055f),
                new Vector2(0.745f, 0.155f),
                selected ? DeckAmber : DeckEmerald,
                () =>
                {
                    if (_repository.TrySelectDeck(
                            deck.deckId,
                            out string selectRejection))
                    {
                        ShowDeckDetails(deck);
                        return;
                    }

                    selectionStatus.text = selectRejection;
                    selectionStatus.color = Danger;
                },
                15);
            CreateArcaneActionButton(
                workspace.transform,
                "EDITAR DECK",
                new Vector2(0.765f, 0.055f),
                new Vector2(0.965f, 0.155f),
                DeckEmerald,
                () => ShowDeckEditor(deck),
                16);
        }

        private void BuildDeckWorkshopBackground(string section)
        {
            Image background = CreatePanel(
                _screenRoot,
                "Fundo da Oficina de Decks",
                Vector2.zero,
                Vector2.one,
                DeckGraphite);
            background.transform.SetAsFirstSibling();

            CreatePanel(
                background.transform,
                "Neblina esmeralda superior",
                new Vector2(0f, 0.78f),
                new Vector2(1f, 1f),
                new Color(0.015f, 0.13f, 0.095f, 0.76f))
                .raycastTarget = false;
            CreatePanel(
                background.transform,
                "Faixa do cabeçalho",
                new Vector2(0f, 0.875f),
                new Vector2(1f, 1f),
                new Color(0.012f, 0.045f, 0.047f, 0.98f))
                .raycastTarget = false;
            CreatePanel(
                background.transform,
                "Linha esmeralda do cabeçalho",
                new Vector2(0f, 0.872f),
                new Vector2(1f, 0.878f),
                new Color(DeckEmerald.r, DeckEmerald.g, DeckEmerald.b, 0.88f))
                .raycastTarget = false;

            for (int index = 0; index < 8; index++)
            {
                float x = 0.055f + index * 0.127f;
                CreatePanel(
                    background.transform,
                    $"Trilho vertical {index + 1}",
                    new Vector2(x, 0.055f),
                    new Vector2(x + 0.0011f, 0.87f),
                    new Color(0.20f, 0.86f, 0.59f, 0.075f))
                    .raycastTarget = false;
            }
            for (int index = 0; index < 7; index++)
            {
                float y = 0.07f + index * 0.112f;
                CreatePanel(
                    background.transform,
                    $"Trilho horizontal {index + 1}",
                    new Vector2(0.025f, y),
                    new Vector2(0.975f, y + 0.0015f),
                    new Color(0.20f, 0.86f, 0.59f, 0.065f))
                    .raycastTarget = false;
            }

            Image rayLeft = CreatePanel(
                background.transform,
                "Energia diagonal esquerda",
                new Vector2(0.015f, 0.17f),
                new Vector2(0.39f, 0.173f),
                new Color(DeckEmerald.r, DeckEmerald.g, DeckEmerald.b, 0.17f));
            rayLeft.rectTransform.localEulerAngles = new Vector3(0f, 0f, 17f);
            rayLeft.raycastTarget = false;
            Image rayRight = CreatePanel(
                background.transform,
                "Energia diagonal direita",
                new Vector2(0.62f, 0.73f),
                new Vector2(0.985f, 0.733f),
                new Color(DeckMint.r, DeckMint.g, DeckMint.b, 0.12f));
            rayRight.rectTransform.localEulerAngles = new Vector3(0f, 0f, -17f);
            rayRight.raycastTarget = false;

            CreateText(
                background.transform,
                $"MASTER DUEL 2 PLUS ULTRA  •  {section}",
                12,
                FontStyle.Bold,
                new Color(DeckMint.r, DeckMint.g, DeckMint.b, 0.58f),
                new Vector2(0.58f, 0.89f),
                new Vector2(0.805f, 0.975f),
                TextAnchor.MiddleRight);
        }

        private void BuildDeckWorkshopHeader(
            string title,
            string subtitle,
            string counter,
            Action backAction)
        {
            CreateArcaneActionButton(
                _screenRoot,
                "‹",
                new Vector2(0.018f, 0.905f),
                new Vector2(0.064f, 0.974f),
                DeckEmerald,
                backAction,
                26);
            CreateText(
                _screenRoot,
                title,
                30,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.078f, 0.928f),
                new Vector2(0.61f, 0.984f),
                TextAnchor.MiddleLeft);
            CreateText(
                _screenRoot,
                subtitle,
                12,
                FontStyle.Bold,
                DeckMuted,
                new Vector2(0.08f, 0.884f),
                new Vector2(0.62f, 0.93f),
                TextAnchor.MiddleLeft);

            Image counterPanel = CreateArcaneSurface(
                _screenRoot,
                "Contador da oficina",
                new Vector2(0.82f, 0.905f),
                new Vector2(0.965f, 0.974f),
                DeckAmber,
                true,
                0.76f);
            CreateText(
                counterPanel.transform,
                counter,
                16,
                FontStyle.Bold,
                new Color(1f, 0.85f, 0.58f, 1f),
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter);
        }

        private DeckRecord ResolveActiveWorkshopDeck()
        {
            if (_repository?.State?.decks == null ||
                _repository.State.decks.Count == 0)
                return null;

            string selectedId = _repository.State.selectedDeckId;
            foreach (DeckRecord deck in _repository.State.decks)
            {
                if (deck != null && deck.deckId == selectedId)
                    return deck;
            }
            return _repository.State.decks[0];
        }

        private void BuildDeckWorkshopHero(DeckRecord deck)
        {
            Image hero = CreateArcaneSurface(
                _screenRoot,
                "Deck ativo em destaque",
                new Vector2(0.035f, 0.075f),
                new Vector2(0.375f, 0.855f),
                deck != null && _repository.IsSelected(deck)
                    ? DeckAmber
                    : DeckEmerald,
                true,
                0.94f);
            CreateText(
                hero.transform,
                "DECK ATIVO",
                13,
                FontStyle.Bold,
                DeckAmber,
                new Vector2(0.07f, 0.925f),
                new Vector2(0.93f, 0.98f),
                TextAnchor.MiddleLeft);

            if (deck == null)
            {
                CreateText(
                    hero.transform,
                    "SEU ARSENAL\nCOMEÇA AQUI",
                    27,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.10f, 0.52f),
                    new Vector2(0.90f, 0.78f),
                    TextAnchor.MiddleCenter);
                CreateText(
                    hero.transform,
                    "Crie um deck para organizar suas cartas e entrar em duelo.",
                    14,
                    FontStyle.Normal,
                    DeckMuted,
                    new Vector2(0.12f, 0.36f),
                    new Vector2(0.88f, 0.51f),
                    TextAnchor.UpperCenter);
                CreateArcaneActionButton(
                    hero.transform,
                    "+  CRIAR PRIMEIRO DECK",
                    new Vector2(0.09f, 0.07f),
                    new Vector2(0.91f, 0.17f),
                    DeckEmerald,
                    CreateWorkshopDeck,
                    15);
                return;
            }

            CreateText(
                hero.transform,
                deck.displayName,
                27,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.84f),
                new Vector2(0.93f, 0.93f),
                TextAnchor.MiddleLeft);

            Image showcase = CreateArcaneSurface(
                hero.transform,
                "Vitrine do deck ativo",
                new Vector2(0.07f, 0.36f),
                new Vector2(0.93f, 0.83f),
                DeckEmerald,
                false,
                0.62f);
            CreateWorkshopEnergyLines(showcase.transform);
            CreateDeckCaseVisual(
                showcase.transform,
                deck.caseTheme,
                new Vector2(0.055f, 0.16f),
                new Vector2(0.40f, 0.84f));
            CreateWorkshopFeaturedCards(
                showcase.transform,
                deck,
                new Vector2(0.31f, 0.08f),
                new Vector2(0.97f, 0.91f),
                0.51f);

            bool legal = DeckRepository.TryValidateForDuel(
                deck,
                _catalog,
                out _);
            Image status = CreateArcaneSurface(
                hero.transform,
                "Estado do deck ativo",
                new Vector2(0.07f, 0.285f),
                new Vector2(0.93f, 0.35f),
                legal ? DeckEmerald : DeckAmber,
                false,
                0.78f);
            CreateText(
                status.transform,
                legal ? "●  PRONTO PARA O DUELO" : "◆  DECK PRECISA DE REVISÃO",
                12,
                FontStyle.Bold,
                legal ? DeckMint : DeckAmber,
                new Vector2(0.05f, 0.08f),
                new Vector2(0.95f, 0.92f),
                TextAnchor.MiddleCenter);

            CreateDeckWorkshopMetric(
                hero.transform,
                "PRINCIPAL",
                deck.mainDeckCardIds.Count.ToString(),
                new Vector2(0.07f, 0.18f),
                new Vector2(0.48f, 0.27f),
                DeckEmerald);
            CreateDeckWorkshopMetric(
                hero.transform,
                "ADICIONAL",
                deck.extraDeckCardIds.Count.ToString(),
                new Vector2(0.52f, 0.18f),
                new Vector2(0.93f, 0.27f),
                DeckMint);

            CreateArcaneActionButton(
                hero.transform,
                "GERENCIAR DECK",
                new Vector2(0.07f, 0.055f),
                new Vector2(0.93f, 0.155f),
                DeckAmber,
                () => ShowDeckDetails(deck),
                16);
        }

        private void BuildDeckWorkshopLibrary(int deckCount)
        {
            Image library = CreateArcaneSurface(
                _screenRoot,
                "Biblioteca de decks",
                new Vector2(0.395f, 0.075f),
                new Vector2(0.965f, 0.855f),
                DeckEmerald,
                false,
                0.88f);
            CreateText(
                library.transform,
                "BIBLIOTECA",
                22,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.90f),
                new Vector2(0.53f, 0.975f),
                TextAnchor.MiddleLeft);
            CreateText(
                library.transform,
                deckCount == 0
                    ? "Crie seu primeiro deck para começar."
                    : "Selecione um deck para ver detalhes, editar ou torná-lo ativo.",
                12,
                FontStyle.Normal,
                DeckMuted,
                new Vector2(0.035f, 0.835f),
                new Vector2(0.94f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                library.transform,
                "2 COLUNAS  •  GRADE RESPONSIVA",
                10,
                FontStyle.Bold,
                new Color(DeckEmerald.r, DeckEmerald.g, DeckEmerald.b, 0.66f),
                new Vector2(0.58f, 0.91f),
                new Vector2(0.95f, 0.97f),
                TextAnchor.MiddleRight);

            RectTransform content = CreateDeckWorkshopGrid(
                library.transform,
                new Vector2(0.025f, 0.035f),
                new Vector2(0.975f, 0.825f));
            CreateWorkshopNewDeckTile(content);
            if (_repository?.State?.decks == null)
                return;
            foreach (DeckRecord deck in _repository.State.decks)
            {
                if (deck != null)
                    CreateWorkshopDeckTile(content, deck);
            }
        }

        private static RectTransform CreateDeckWorkshopGrid(
            Transform parent,
            Vector2 min,
            Vector2 max)
        {
            Image viewport = CreatePanel(
                parent,
                "Grade responsiva da biblioteca",
                min,
                max,
                new Color(0.001f, 0.012f, 0.013f, 0.64f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            GameObject contentObject = new(
                "Conteúdo calculado",
                typeof(RectTransform),
                typeof(GridLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(ArcaneResponsiveGridFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(10f, 0f);
            content.offsetMax = new Vector2(-27f, 0f);

            GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.UpperCenter;
            ContentSizeFitter fitter =
                contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentObject.GetComponent<ArcaneResponsiveGridFitter>()
                .Configure(2, 190f, 14f);

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 58f;

            Image track = CreatePanel(
                viewport.transform,
                "Trilho da biblioteca",
                new Vector2(0.972f, 0.02f),
                new Vector2(0.992f, 0.98f),
                new Color(0.01f, 0.06f, 0.05f, 0.94f));
            GameObject slide = new("Área deslizante", typeof(RectTransform));
            slide.transform.SetParent(track.transform, false);
            Stretch(slide.GetComponent<RectTransform>());
            Image handle = CreatePanel(
                slide.transform,
                "Alça esmeralda",
                new Vector2(0.12f, 0f),
                new Vector2(0.88f, 1f),
                new Color(DeckEmerald.r, DeckEmerald.g, DeckEmerald.b, 0.9f));
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private void CreateWorkshopNewDeckTile(Transform parent)
        {
            Image tile = CreateArcaneSurface(
                parent,
                "Criar novo deck",
                Vector2.zero,
                Vector2.one,
                DeckEmerald,
                true,
                0.76f);
            CreateWorkshopEnergyLines(tile.transform);
            Image symbol = CreateArcaneSurface(
                tile.transform,
                "Símbolo de criação",
                new Vector2(0.055f, 0.18f),
                new Vector2(0.35f, 0.82f),
                DeckEmerald,
                true,
                0.86f);
            CreateText(
                symbol.transform,
                "+",
                52,
                FontStyle.Normal,
                DeckMint,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateText(
                tile.transform,
                "NOVO DECK",
                19,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.40f, 0.59f),
                new Vector2(0.94f, 0.82f),
                TextAnchor.MiddleLeft);
            CreateText(
                tile.transform,
                "Crie uma lista do zero e escolha sua identidade de duelo.",
                12,
                FontStyle.Normal,
                DeckMuted,
                new Vector2(0.40f, 0.30f),
                new Vector2(0.94f, 0.59f),
                TextAnchor.UpperLeft);
            CreateText(
                tile.transform,
                "CRIAR  →",
                12,
                FontStyle.Bold,
                DeckEmerald,
                new Vector2(0.40f, 0.10f),
                new Vector2(0.94f, 0.28f),
                TextAnchor.MiddleLeft);
            AddDeckWorkshopButton(tile, CreateWorkshopDeck);
        }

        private void CreateWorkshopDeckTile(Transform parent, DeckRecord deck)
        {
            bool selected = _repository.IsSelected(deck);
            bool legal = DeckRepository.TryValidateForDuel(
                deck,
                _catalog,
                out _);
            Color accent = selected ? DeckAmber : DeckEmerald;
            Image tile = CreateArcaneSurface(
                parent,
                $"Deck da biblioteca {deck.deckId}",
                Vector2.zero,
                Vector2.one,
                accent,
                selected,
                selected ? 0.90f : 0.72f);

            Image visual = CreateArcaneSurface(
                tile.transform,
                "Miniatura do deck",
                new Vector2(0.035f, 0.11f),
                new Vector2(0.43f, 0.89f),
                accent,
                false,
                0.62f);
            CreateDeckCaseVisual(
                visual.transform,
                deck.caseTheme,
                new Vector2(0.035f, 0.11f),
                new Vector2(0.48f, 0.90f));
            CreateWorkshopFeaturedCards(
                visual.transform,
                deck,
                new Vector2(0.29f, 0.06f),
                new Vector2(0.98f, 0.94f),
                0.58f);

            if (selected)
            {
                Image badge = CreatePanel(
                    tile.transform,
                    "Selo de deck ativo",
                    new Vector2(0.035f, 0.82f),
                    new Vector2(0.31f, 0.94f),
                    new Color(DeckAmber.r, DeckAmber.g, DeckAmber.b, 0.96f));
                CreateText(
                    badge.transform,
                    "ATIVO",
                    10,
                    FontStyle.Bold,
                    Ink,
                    Vector2.zero,
                    Vector2.one,
                    TextAnchor.MiddleCenter);
            }

            CreateText(
                tile.transform,
                deck.displayName,
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.46f, 0.67f),
                new Vector2(0.80f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                tile.transform,
                "STANDARD",
                10,
                FontStyle.Bold,
                accent,
                new Vector2(0.46f, 0.55f),
                new Vector2(0.80f, 0.68f),
                TextAnchor.MiddleLeft);
            CreateText(
                tile.transform,
                $"{deck.mainDeckCardIds.Count} PRINCIPAL  •  " +
                $"{deck.extraDeckCardIds.Count} ADICIONAL",
                11,
                FontStyle.Bold,
                DeckMuted,
                new Vector2(0.46f, 0.38f),
                new Vector2(0.94f, 0.55f),
                TextAnchor.MiddleLeft);
            CreateText(
                tile.transform,
                legal ? "●  PRONTO" : "◆  REVISAR",
                11,
                FontStyle.Bold,
                legal ? DeckMint : DeckAmber,
                new Vector2(0.46f, 0.23f),
                new Vector2(0.94f, 0.39f),
                TextAnchor.MiddleLeft);
            CreateText(
                tile.transform,
                "ABRIR DETALHES  →",
                10,
                FontStyle.Bold,
                accent,
                new Vector2(0.46f, 0.07f),
                new Vector2(0.94f, 0.23f),
                TextAnchor.MiddleLeft);

            EventTrigger trigger = tile.gameObject.AddComponent<EventTrigger>();
            Image deleteControl = CreateDeckDeleteControl(tile.transform, deck);
            bool keepDeleteVisible = Application.isMobilePlatform;
            deleteControl.gameObject.SetActive(keepDeleteVisible);
            AddTrigger(trigger, EventTriggerType.PointerEnter, _ =>
            {
                deleteControl.gameObject.SetActive(true);
                deleteControl.transform.SetAsLastSibling();
            });
            AddTrigger(trigger, EventTriggerType.PointerExit, _ =>
            {
                if (!keepDeleteVisible)
                    deleteControl.gameObject.SetActive(false);
            });
            AddDeckWorkshopButton(tile, () => ShowDeckDetails(deck));
        }

        private void CreateWorkshopDeck()
        {
            DeckRecord deck = _repository.CreateDeck(
                $"Novo Deck {_repository.State.decks.Count + 1}",
                _repository.State.decks.Count % CaseColors.Length);
            ShowDeckEditor(deck);
        }

        private static void CreateDeckWorkshopMetric(
            Transform parent,
            string label,
            string value,
            Vector2 min,
            Vector2 max,
            Color accent)
        {
            Image metric = CreateArcaneSurface(
                parent,
                $"Indicador {label}",
                min,
                max,
                accent,
                false,
                0.68f);
            CreateText(
                metric.transform,
                label,
                10,
                FontStyle.Bold,
                DeckMuted,
                new Vector2(0.08f, 0.52f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleLeft);
            CreateText(
                metric.transform,
                value,
                18,
                FontStyle.Bold,
                accent,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.56f),
                TextAnchor.MiddleLeft);
        }

        private static void CreateWorkshopEnergyLines(Transform parent)
        {
            for (int index = 0; index < 4; index++)
            {
                float y = 0.18f + index * 0.19f;
                Image line = CreatePanel(
                    parent,
                    $"Energia da vitrine {index + 1}",
                    new Vector2(0.05f, y),
                    new Vector2(0.95f, y + 0.006f),
                    new Color(DeckEmerald.r, DeckEmerald.g, DeckEmerald.b, 0.10f));
                line.raycastTarget = false;
            }
        }

        private void CreateWorkshopFeaturedCards(
            Transform parent,
            DeckRecord deck,
            Vector2 min,
            Vector2 max,
            float widthFraction)
        {
            float width = max.x - min.x;
            float height = max.y - min.y;
            float cardWidth = width * Mathf.Clamp(widthFraction, 0.42f, 0.64f);
            for (int index = 0; index < 3; index++)
            {
                CardCatalogEntry entry = null;
                if (deck.featuredCardIds != null &&
                    index < deck.featuredCardIds.Count)
                {
                    entry = DeckRepository.ResolveCard(
                        _catalog,
                        deck.featuredCardIds[index]);
                }

                float center = min.x + width *
                    (0.5f + (index - 1) * 0.215f);
                float bottom = min.y +
                    Mathf.Abs(index - 1) * height * 0.055f;
                CreateCardArtwork(
                    parent,
                    entry != null ? entry.Artwork : null,
                    new Vector2(center - cardWidth * 0.5f, bottom),
                    new Vector2(center + cardWidth * 0.5f, max.y),
                    (index - 1) * 7f,
                    true);
            }
        }

        private static void AddDeckWorkshopButton(
            Image surface,
            Action action)
        {
            AddButtonBehaviour(surface, action);
            Button button = surface.GetComponent<Button>();
            ArcanePanelSheenGraphic sheen =
                surface.GetComponentInChildren<ArcanePanelSheenGraphic>();
            if (button != null && sheen != null)
                button.targetGraphic = sheen;
        }
    }
}
