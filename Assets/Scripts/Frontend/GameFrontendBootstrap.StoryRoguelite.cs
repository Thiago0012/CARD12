using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ArcaneArena.Cards;
using ArcaneArena.StoryRoguelite;
using ArcaneDuel.Game;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private readonly List<Sprite> _storyRuntimeSprites = new();
        private readonly List<string> _storyDraftMain = new();
        private StoryRunManager _storyManager;
        private RectTransform _storyMapStage;
        private RectTransform _storyMapMarker;
        private StoryMapViewportController _storyMapViewport;
        private Text _storyDeckStatus;

        public void ShowStoryRoguelite()
        {
            _storyManager = StoryRogueliteRuntime.GetManager(_catalog);
            FlushStoryAccountCoinRewards();
            if (!_storyManager.HasActiveRun)
            {
                if (_storyManager.Save != null &&
                    _storyManager.Save.status != StoryRunStatus.Active)
                {
                    ShowStoryRunSummary();
                    return;
                }
                ShowStoryStarterSelection();
                return;
            }

            _storyManager.RepairInvalidPendingEncounter();
            if (!_storyManager.HasActiveRun)
            {
                ShowStoryRunSummary();
                return;
            }
            _storyManager.PrepareCurrentNode();
            StoryRunSave save = _storyManager.Save;
            if (save.pendingEncounter != null)
            {
                ShowStoryNpcEncounter(save.pendingEncounter);
                return;
            }
            if (save.pendingRelicReward != null)
            {
                ShowStoryRelicReward(save.pendingRelicReward);
                return;
            }
            if (save.pendingReward != null)
            {
                ShowStoryReward(save.pendingReward);
                return;
            }
            if (save.pendingRandomEvent != null)
            {
                ShowStoryRandomEvent(save.pendingRandomEvent);
                return;
            }
            if (save.pendingChoice != null)
            {
                ShowStoryChoice(save.pendingChoice);
                return;
            }
            ShowStoryMap();
        }

        private void ShowStoryStarterSelection()
        {
            ClearScreen();
            ClearStoryRuntimeSprites();
            _shopBackAction = ShowDuelHub;
            BuildStoryBackground("CRÔNICAS DO DUELO");
            BuildProfessionalShopHeader(
                "CRÔNICAS DO DUELO · ESCOLHA SEU DECK",
                ShowDuelHub);

            CreateText(
                _screenRoot,
                "A jornada começa com exatamente 20 cartas no Deck Principal. " +
                "O Deck Adicional pode conter até 15 cartas.",
                18,
                FontStyle.Normal,
                Muted,
                new Vector2(0.07f, 0.82f),
                new Vector2(0.93f, 0.88f),
                TextAnchor.MiddleLeft);

            IReadOnlyList<StoryStarterDeck> starters =
                StoryStarterDeckService.BuildStarters();
            for (int index = 0; index < starters.Count; index++)
            {
                StoryStarterDeck starter = starters[index];
                int column = index % 5;
                int row = index / 5;
                float x = 0.055f + column * 0.184f;
                float top = 0.79f - row * 0.31f;
                Image tile = CreatePanel(
                    _screenRoot,
                    starter.DisplayName,
                    new Vector2(x, top - 0.275f),
                    new Vector2(x + 0.17f, top),
                    Color.clear);
                DecorateRuntimeShopSurface(tile, Gold, true, 11f);
                CardCatalogEntry cover = DeckRepository.ResolveCard(
                    _catalog, starter.CoverCardId);
                CreateCardArtwork(
                    tile.transform,
                    cover?.Artwork,
                    new Vector2(0.28f, 0.28f),
                    new Vector2(0.72f, 0.92f),
                    0f,
                    true).raycastTarget = false;
                CreateText(
                    tile.transform,
                    starter.DisplayName?.ToUpperInvariant() ?? "DECK INICIAL",
                    15,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.05f, 0.10f),
                    new Vector2(0.95f, 0.27f),
                    TextAnchor.MiddleCenter);
                Image choose = CreateButton(
                    tile.transform,
                    "ESCOLHER",
                    new Vector2(0.18f, 0.015f),
                    new Vector2(0.82f, 0.12f),
                    Gold,
                    () => StartStoryRun(starter.Main, starter.Extra));
                DecorateRuntimeShopButton(choose, Gold, true, 6f);
            }

            Image custom = CreateButton(
                _screenRoot,
                "MONTAR 20 CARTAS MANUALMENTE",
                new Vector2(0.34f, 0.045f),
                new Vector2(0.66f, 0.105f),
                Cyan,
                ShowStoryCustomDeckBuilder);
            DecorateRuntimeShopButton(custom, Cyan, true, 8f);
        }

        private void ShowStoryCustomDeckBuilder()
        {
            ClearScreen();
            _shopBackAction = ShowStoryStarterSelection;
            BuildStoryBackground("DECK INICIAL PERSONALIZADO");
            BuildProfessionalShopHeader(
                "MONTE SEU DECK INICIAL",
                ShowStoryStarterSelection);

            Text counter = CreateText(
                _screenRoot,
                $"SELECIONADAS  {_storyDraftMain.Count} / 20",
                22,
                FontStyle.Bold,
                _storyDraftMain.Count == 20 ? Lime : Gold,
                new Vector2(0.07f, 0.82f),
                new Vector2(0.42f, 0.88f),
                TextAnchor.MiddleLeft);
            counter.name = "Contador do deck inicial";

            Image selected = CreatePanel(
                _screenRoot,
                "Cartas selecionadas",
                new Vector2(0.67f, 0.18f),
                new Vector2(0.95f, 0.80f),
                Color.clear);
            DecorateRuntimeShopSurface(selected, Cyan, true, 12f);
            CreateText(
                selected.transform,
                _storyDraftMain.Count == 0
                    ? "Clique nas cartas do catálogo para adicioná-las."
                    : string.Join("\n", _storyDraftMain.Select((id, index) =>
                    {
                        CardCatalogEntry entry = DeckRepository.ResolveCard(
                            _catalog, id);
                        return $"{index + 1:00} · {entry?.DisplayName ?? id}";
                    })),
                15,
                FontStyle.Normal,
                Color.white,
                new Vector2(0.06f, 0.13f),
                new Vector2(0.94f, 0.93f),
                TextAnchor.UpperLeft);

            RectTransform grid = CreateScrollGrid(
                _screenRoot,
                "Catálogo da jornada",
                new Vector2(0.05f, 0.18f),
                new Vector2(0.64f, 0.80f),
                new Vector2(83f, 124f),
                new Vector2(9f, 10f),
                7);
            foreach (CardCatalogEntry entry in ReadyCatalogEntries()
                         .Where(entry => !DeckRepository.BelongsToExtraDeck(entry)))
            {
                string cardId = entry.OfficialCardId;
                if (string.IsNullOrWhiteSpace(cardId)) continue;
                Image card = CreateCardArtwork(
                    grid,
                    entry.Artwork,
                    Vector2.zero,
                    Vector2.one,
                    0f,
                    false);
                card.rectTransform.sizeDelta = new Vector2(83f, 124f);
                AddButtonBehaviour(card, () =>
                {
                    int limit = BanlistService.Active.MaximumCopies(cardId);
                    if (_storyDraftMain.Count < 20 &&
                        _storyDraftMain.Count(id => string.Equals(
                            id, cardId, StringComparison.Ordinal)) < limit)
                    {
                        _storyDraftMain.Add(cardId);
                    }
                    ShowStoryCustomDeckBuilder();
                });
            }

            Image remove = CreateButton(
                _screenRoot,
                "REMOVER ÚLTIMA",
                new Vector2(0.67f, 0.10f),
                new Vector2(0.805f, 0.16f),
                Danger,
                () =>
                {
                    if (_storyDraftMain.Count > 0)
                        _storyDraftMain.RemoveAt(_storyDraftMain.Count - 1);
                    ShowStoryCustomDeckBuilder();
                });
            DecorateRuntimeShopButton(remove, Danger, false, 7f);
            Image begin = CreateButton(
                _screenRoot,
                "INICIAR JORNADA",
                new Vector2(0.815f, 0.10f),
                new Vector2(0.95f, 0.16f),
                _storyDraftMain.Count == 20 ? Lime : Muted,
                () =>
                {
                    if (_storyDraftMain.Count == 20)
                        StartStoryRun(_storyDraftMain, Array.Empty<string>());
                });
            begin.GetComponent<Button>().interactable =
                _storyDraftMain.Count == 20;
            DecorateRuntimeShopButton(begin,
                _storyDraftMain.Count == 20 ? Lime : Muted,
                true,
                7f);
        }

        private void StartStoryRun(
            IReadOnlyList<string> main,
            IReadOnlyList<string> extra)
        {
            try
            {
                long seed = DateTime.UtcNow.Ticks ^ BitConverter.ToInt64(
                    Guid.NewGuid().ToByteArray(), 0);
                _storyManager = StoryRogueliteRuntime.GetManager(_catalog);
                _storyManager.StartNew(
                    seed,
                    main,
                    extra,
                    _repository?.State?.localProfileId,
                    _repository?.PlayerDisplayName,
                    _repository?.EquippedIconId);
                _storyDraftMain.Clear();
                ShowStoryMap();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowStoryError(exception.GetBaseException().Message,
                    ShowStoryStarterSelection);
            }
        }

        private void ShowStoryMap()
        {
            StoryRunSave save = _storyManager?.Save;
            StoryMapRecord map = _storyManager?.CurrentMap;
            if (save == null || map == null)
            {
                ShowStoryRoguelite();
                return;
            }
            ClearScreen();
            ClearStoryRuntimeSprites();
            _shopBackAction = ShowDuelHub;
            BuildStoryBackground("MAPA DA JORNADA");
            BuildProfessionalShopHeader(
                $"ATO {save.actIndex} · {map.displayName.ToUpperInvariant()}",
                ExitStoryRunToHub);

            Image stage = CreatePanel(
                _screenRoot,
                "Mapa roguelite",
                StoryRogueliteUiLayout.MapPanelMin,
                StoryRogueliteUiLayout.MapPanelMax,
                new Color(0.005f, 0.010f, 0.014f, 0.98f));
            DecorateRuntimeShopSurface(stage, Cyan, false, 13f);

            Image viewport = CreatePanel(
                stage.transform,
                "Janela navegável do mapa procedural",
                StoryRogueliteUiLayout.MapViewportMin,
                StoryRogueliteUiLayout.MapViewportMax,
                new Color(0.006f, 0.025f, 0.034f, 0.98f));
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 52f;

            GameObject contentObject = new(
                "Rotas geradas da jornada",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            contentObject.transform.SetParent(viewport.transform, false);
            _storyMapStage = contentObject.GetComponent<RectTransform>();
            _storyMapStage.anchorMin = new Vector2(0f, 1f);
            _storyMapStage.anchorMax = new Vector2(0f, 1f);
            _storyMapStage.pivot = new Vector2(0f, 1f);
            _storyMapStage.anchoredPosition = Vector2.zero;
            Vector2 baseMapSize = StoryRogueliteUiLayout.MapBaseSize;
            _storyMapStage.sizeDelta = baseMapSize;
            Image proceduralSurface = contentObject.GetComponent<Image>();
            proceduralSurface.color = new Color(
                0.008f, 0.045f, 0.050f, 0.995f);
            proceduralSurface.raycastTarget = true;
            scroll.content = _storyMapStage;

            GameObject edgeObject = new(
                "Conexões do mapa",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(StoryMapEdgeGraphic));
            edgeObject.transform.SetParent(_storyMapStage, false);
            Stretch(edgeObject.GetComponent<RectTransform>());
            edgeObject.GetComponent<StoryMapEdgeGraphic>().Configure(
                map, _storyManager.NodesForCurrentMap());

            foreach (StoryMapNodeRecord node in map.nodes)
            {
                StoryRuntimeNode runtime = _storyManager.RuntimeNode(
                    node.nodeId);
                if (runtime == null) continue;
                bool mysteryRevealed = _storyManager.HasArtifact(
                        "marked-map") ||
                    save.revealedNodeIds.Contains(
                        node.nodeId, StringComparer.Ordinal);
                RogueliteNodeType visibleType = node.NodeType ==
                        RogueliteNodeType.Mystery && !mysteryRevealed
                        ? RogueliteNodeType.Mystery
                        : runtime.NodeType;
                Vector2 half = StoryRogueliteUiLayout.NodeHalfSize(
                    visibleType);
                string label = StoryNodeGlyph(visibleType) + "\n" +
                               StoryNodeShortLabel(visibleType);
                Image nodeButton = CreateButton(
                    _storyMapStage,
                    label,
                    node.NormalizedPosition - half,
                    node.NormalizedPosition + half,
                    StoryNodeColor(runtime),
                    () => OnStoryNodePressed(node.nodeId));
                nodeButton.name = "Ponto · " + node.nodeId;
                DecorateRuntimeShopButton(
                    nodeButton, StoryNodeColor(runtime),
                    runtime.state == RogueliteNodeState.Current ||
                    runtime.state == RogueliteNodeState.Available,
                    5f);
                Text nodeText = nodeButton.GetComponentInChildren<Text>();
                if (nodeText != null)
                {
                    nodeText.fontSize = visibleType == RogueliteNodeType.Boss
                        ? 15
                        : 13;
                    nodeText.resizeTextForBestFit = true;
                    nodeText.resizeTextMinSize = 9;
                    nodeText.resizeTextMaxSize = 15;
                }
            }

            _storyMapViewport = viewport.gameObject.AddComponent<
                StoryMapViewportController>();
            _storyMapViewport.Configure(
                scroll,
                _storyMapStage,
                baseMapSize,
                StoryRogueliteUiLayout.InitialMapZoom);

            Image zoomOut = CreateButton(stage.transform,
                "−", new Vector2(0.02f, 0.89f), new Vector2(0.075f, 0.965f),
                Cyan, () => _storyMapViewport?.ZoomOut());
            Image zoomReset = CreateButton(stage.transform,
                "100%", new Vector2(0.08f, 0.89f), new Vector2(0.17f, 0.965f),
                Muted, () => _storyMapViewport?.ResetZoom());
            Image zoomIn = CreateButton(stage.transform,
                "+", new Vector2(0.175f, 0.89f), new Vector2(0.23f, 0.965f),
                Cyan, () => _storyMapViewport?.ZoomIn());
            DecorateRuntimeShopButton(zoomOut, Cyan, true, 5f);
            DecorateRuntimeShopButton(zoomReset, Muted, false, 5f);
            DecorateRuntimeShopButton(zoomIn, Cyan, true, 5f);

            StoryMapNodeRecord current = map.Node(save.currentNodeId);
            if (current != null)
            {
                GameObject markerObject = new(
                    "Marcador do perfil equipado",
                    typeof(RectTransform));
                markerObject.transform.SetParent(
                    _storyMapStage != null
                        ? _storyMapStage
                        : stage.transform,
                    false);
                _storyMapMarker = markerObject.GetComponent<RectTransform>();
                RectTransform marker = _storyMapMarker;
                SetStoryMarkerPosition(
                    marker,
                    StoryRogueliteUiLayout.ResolveMarkerPosition(
                        map,
                        current));
                // A caixa externa conserva o tamanho do marcador anterior.
                // O AspectRatioFitter do HexIconView fica somente no filho,
                // para recortar a arte em hexágono sem recalcular a escala
                // usando o mapa inteiro como referência.
                HexIconView markerIcon = CreateHexIcon(
                    markerObject.transform,
                    "Ícone hexagonal do jogador",
                    save.equippedIconId,
                    Vector2.zero,
                    Vector2.one);
                markerIcon.SetAccent(Gold);
                marker.SetAsLastSibling();
                _storyMapViewport.Focus(current.NormalizedPosition);
            }

            BuildStoryMapSidebar(save, map);
        }

        private void BuildStoryMapSidebar(
            StoryRunSave save,
            StoryMapRecord map)
        {
            Image panel = CreatePanel(
                _screenRoot,
                "Estado da jornada",
                new Vector2(0.745f, 0.075f),
                new Vector2(0.97f, 0.875f),
                Color.clear);
            DecorateRuntimeShopSurface(panel, Gold, true, 12f);
            CreateText(panel.transform,
                "JORNADA ATIVA",
                20, FontStyle.Bold, Gold,
                new Vector2(0.07f, 0.91f), new Vector2(0.93f, 0.98f),
                TextAnchor.MiddleCenter);
            CreateText(panel.transform,
                $"ATO  {save.actIndex} / {save.mapSequence.Count}",
                15, FontStyle.Bold, Color.white,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.90f),
                TextAnchor.MiddleLeft);
            BuildStorySealHearts(
                panel.transform,
                save.seals,
                _storyManager.MaxSeals);
            CreateText(panel.transform,
                $"FRAGMENTOS DA RUN  {save.fragments}\n" +
                $"MOEDAS DA CONTA  {_repository?.CoinBalance ?? 0}\n" +
                $"GANHAS NESTA RUN  {save.accountCoinsEarned}",
                13, FontStyle.Bold, Color.white,
                new Vector2(0.08f, 0.65f), new Vector2(0.92f, 0.72f),
                TextAnchor.UpperLeft);
            CreateText(panel.transform,
                "RELÍQUIAS ATIVAS",
                14, FontStyle.Bold, Cyan,
                new Vector2(0.08f, 0.59f), new Vector2(0.92f, 0.65f),
                TextAnchor.MiddleLeft);
            Text relics = CreateScrollableText(
                panel.transform,
                "Detalhes das relíquias",
                new Vector2(0.07f, 0.39f),
                new Vector2(0.93f, 0.59f),
                12);
            relics.text = StoryRelicSummary(save);

            CreateText(panel.transform,
                "Arraste para mover · pinça/roda para zoom",
                11, FontStyle.Bold, Muted,
                new Vector2(0.07f, 0.34f), new Vector2(0.93f, 0.39f),
                TextAnchor.MiddleCenter);
            Image guide = CreateButton(panel.transform,
                "GUIA DO MAPA E RECURSOS",
                new Vector2(0.08f, 0.285f), new Vector2(0.92f, 0.35f),
                Lime, ShowStoryMapGuide);
            DecorateRuntimeShopButton(guide, Lime, true, 6f);
            Image deck = CreateButton(panel.transform,
                "DECK DA JORNADA",
                new Vector2(0.08f, 0.215f), new Vector2(0.92f, 0.275f),
                Cyan, ShowStoryDeckManagement);
            DecorateRuntimeShopButton(deck, Cyan, true, 7f);

            Image leave = CreateButton(panel.transform,
                "SAIR DA RUN",
                new Vector2(0.08f, 0.145f), new Vector2(0.92f, 0.205f),
                Gold, ExitStoryRunToHub);
            DecorateRuntimeShopButton(leave, Gold, true, 7f);
            Image abandon = CreateButton(panel.transform,
                "DESISTIR DA RUN",
                new Vector2(0.08f, 0.075f), new Vector2(0.92f, 0.135f),
                Danger, ShowStoryAbandonConfirmation);
            DecorateRuntimeShopButton(abandon, Danger, false, 7f);
        }

        private void BuildStorySealHearts(
            Transform parent,
            int currentSeals,
            int maximumSeals)
        {
            int total = Mathf.Max(1, maximumSeals);
            int filledCount = Mathf.Clamp(currentSeals, 0, total);
            Image band = CreatePanel(
                parent,
                "Corações das vidas da jornada",
                new Vector2(0.08f, 0.72f),
                new Vector2(0.92f, 0.84f),
                Color.clear);
            band.raycastTarget = false;
            Text label = CreateText(
                band.transform,
                $"VIDAS DA JORNADA  {filledCount} / {total}",
                12,
                FontStyle.Bold,
                Gold,
                new Vector2(0f, 0.62f),
                new Vector2(1f, 1f),
                TextAnchor.MiddleLeft);
            label.raycastTarget = false;

            const float maximumHeartRowWidth = 0.78f;
            float rowWidth = Mathf.Min(
                maximumHeartRowWidth,
                0.18f * total);
            float firstX = 0.5f - rowWidth * 0.5f;
            float heartWidth = rowWidth / total;
            for (int index = 0; index < total; index++)
            {
                bool filled = index < filledCount;
                Text heart = CreateText(
                    band.transform,
                    "♥",
                    31,
                    FontStyle.Bold,
                    filled
                        ? new Color(0.94f, 0.09f, 0.16f, 1f)
                        : new Color(0.018f, 0.020f, 0.027f, 1f),
                    new Vector2(firstX + index * heartWidth, 0f),
                    new Vector2(firstX + (index + 1) * heartWidth, 0.68f),
                    TextAnchor.MiddleCenter);
                heart.name = filled
                    ? $"Coração {index + 1} · cheio"
                    : $"Coração {index + 1} · perdido";
                heart.raycastTarget = false;
                AddOutline(
                    heart.gameObject,
                    filled
                        ? new Color(1f, 0.64f, 0.20f, 1f)
                        : new Color(0.50f, 0.09f, 0.13f, 1f),
                    new Vector2(1.5f, -1.5f));
            }
        }

        private void ExitStoryRunToHub()
        {
            // Every meaningful run mutation is persisted immediately. Leaving
            // this screen therefore only closes the presentation; it does not
            // abandon or reset the active journey.
            ShowDuelHub();
        }

        private void OnStoryNodePressed(string nodeId)
        {
            StoryRuntimeNode runtime = _storyManager.RuntimeNode(nodeId);
            if (runtime != null && runtime.state == RogueliteNodeState.Current &&
                !runtime.resolved)
            {
                ShowStoryRoguelite();
                return;
            }
            if (!_storyManager.SelectNode(nodeId, out string rejection))
            {
                ShowStoryToast(rejection);
                return;
            }
            StoryMapNodeRecord node = _storyManager.CurrentMap.Node(nodeId);
            bool mysteryRevealed = node?.NodeType !=
                    RogueliteNodeType.Mystery ||
                _storyManager.HasArtifact("marked-map") ||
                _storyManager.Save.revealedNodeIds.Contains(
                    nodeId, StringComparer.Ordinal);
            RogueliteNodeType displayType = mysteryRevealed && runtime != null
                ? runtime.NodeType
                : node?.NodeType ?? RogueliteNodeType.Mystery;
            string destinationLabel = displayType ==
                    RogueliteNodeType.Mystery
                ? StoryContentCatalog.PublicNodeLabel(
                    RogueliteNodeType.Mystery)
                : StoryContentCatalog.PublicNodeLabel(displayType);
            string encounterRisk = string.Empty;
            if (runtime != null && StoryRunManager.IsCombat(runtime.NodeType))
            {
                int enemyLp = _storyManager.ResolveEnemyLifePoints(
                    runtime.NodeType, _storyManager.Save.actIndex);
                encounterRisk =
                    $"\n\nRISCO: {StoryNodeLabel(runtime.NodeType).ToUpperInvariant()}" +
                    $" · LP DO INIMIGO {enemyLp:N0}";
            }
            Image veil = CreatePanel(_screenRoot,
                "Confirmação de rota",
                Vector2.zero, Vector2.one,
                new Color(0f, 0f, 0f, 0.82f));
            veil.transform.SetAsLastSibling();
            Image modal = CreatePanel(veil.transform,
                "Destino selecionado",
                new Vector2(0.30f, 0.30f), new Vector2(0.70f, 0.68f),
                Color.clear);
            DecorateRuntimeShopSurface(modal, Gold, true, 15f);
            CreateText(modal.transform,
                "CONFIRMAR ROTA",
                26, FontStyle.Bold, Gold,
                new Vector2(0.08f, 0.72f), new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateText(modal.transform,
                $"Destino: {destinationLabel}\n\n" +
                StoryNodeDescription(displayType) +
                "\n\nA escolha será salva antes do deslocamento." +
                encounterRisk,
                17, FontStyle.Normal, Color.white,
                new Vector2(0.10f, 0.30f), new Vector2(0.90f, 0.69f),
                TextAnchor.MiddleCenter);
            Image cancel = CreateButton(modal.transform,
                "CANCELAR",
                new Vector2(0.08f, 0.09f), new Vector2(0.47f, 0.27f),
                Muted, () => Destroy(veil.gameObject));
            DecorateRuntimeShopButton(cancel, Muted, false, 7f);
            Image confirm = CreateButton(modal.transform,
                "SEGUIR",
                new Vector2(0.53f, 0.09f), new Vector2(0.92f, 0.27f),
                Lime, () =>
                {
                    if (!_storyManager.CommitSelectedTransition(
                            out string failure))
                    {
                        ShowStoryToast(failure);
                        return;
                    }
                    Destroy(veil.gameObject);
                    StartCoroutine(AnimateStoryTransition(nodeId));
                });
            DecorateRuntimeShopButton(confirm, Lime, true, 7f);
        }

        private IEnumerator AnimateStoryTransition(string destinationNodeId)
        {
            StoryMapRecord map = _storyManager.CurrentMap;
            StoryMapNodeRecord from = map.Node(_storyManager.Save.currentNodeId);
            StoryMapNodeRecord to = map.Node(destinationNodeId);
            Vector2 start = from != null
                ? StoryRogueliteUiLayout.ResolveMarkerPosition(map, from)
                : Vector2.zero;
            Vector2 end = to != null
                ? StoryRogueliteUiLayout.ResolveMarkerPosition(map, to)
                : start;
            const float duration = 0.48f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(elapsed / duration));
                if (_storyMapMarker != null)
                    SetStoryMarkerPosition(
                        _storyMapMarker,
                        Vector2.Lerp(start, end, progress));
                yield return null;
            }
            _storyManager.FinalizeTransition();
            ShowStoryRoguelite();
        }

        private void ShowStoryNpcEncounter(
            StoryEncounterDefinition encounter)
        {
            if (_storyManager == null ||
                !_storyManager.IsEncounterReady(encounter))
            {
                _storyManager?.RepairInvalidPendingEncounter();
                ShowStoryRoguelite();
                return;
            }

            ClearScreen();
            ClearStoryRuntimeSprites();
            _shopBackAction = ShowStoryMap;
            BuildStoryBackground("ENCONTRO");
            BuildProfessionalShopHeader(
                $"ATO {encounter.act} · ENCONTRO",
                ShowStoryMap);
            Image veil = CreatePanel(_screenRoot,
                "Escurecimento do encontro",
                Vector2.zero, Vector2.one,
                new Color(0f, 0f, 0f, 0.78f));
            Vector4 veilOffsets =
                StoryRogueliteUiLayout.EncounterVeilOffsets;
            ApplyCapturedRectTransform(
                veil.rectTransform,
                Vector2.zero,
                Vector2.one,
                veilOffsets.x,
                veilOffsets.y,
                veilOffsets.z,
                veilOffsets.w);
            veil.transform.SetAsLastSibling();

            Sprite portrait = StoryLoadSprite(encounter.portraitResourcePath);
            Image portraitPanel = CreatePanel(veil.transform,
                "Retrato central do NPC",
                StoryRogueliteUiLayout.EncounterPortraitMin,
                StoryRogueliteUiLayout.EncounterPortraitMax,
                Color.clear);
            Vector4 portraitOffsets =
                StoryRogueliteUiLayout.EncounterPortraitOffsets;
            ApplyCapturedRectTransform(
                portraitPanel.rectTransform,
                StoryRogueliteUiLayout.EncounterPortraitMin,
                StoryRogueliteUiLayout.EncounterPortraitMax,
                portraitOffsets.x,
                portraitOffsets.y,
                portraitOffsets.z,
                portraitOffsets.w);
            if (portrait != null)
            {
                portraitPanel.sprite = portrait;
                portraitPanel.color = Color.white;
                portraitPanel.preserveAspect = true;
            }
            portraitPanel.raycastTarget = false;

            Image dialogue = CreatePanel(veil.transform,
                "Caixa de diálogo da loja",
                StoryRogueliteUiLayout.EncounterDialogueMin,
                StoryRogueliteUiLayout.EncounterDialogueMax,
                Color.clear);
            DecorateRuntimeShopSurface(dialogue, Gold, true, 17f);
            CreateText(dialogue.transform,
                encounter.npcName?.ToUpperInvariant() ?? "DUELISTA",
                25, FontStyle.Bold, Gold,
                new Vector2(0.04f, 0.70f), new Vector2(0.32f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateText(dialogue.transform,
                encounter.dialogueLine,
                19, FontStyle.Normal, Color.white,
                new Vector2(0.04f, 0.23f), new Vector2(0.70f, 0.72f),
                TextAnchor.MiddleLeft);
            CreateText(dialogue.transform,
                $"LP  {encounter.playerLifePoints:N0}  ×  {encounter.opponentLifePoints:N0}\n" +
                $"DIFICULDADE  NÍVEL {encounter.aiTier}" +
                (_storyManager.HasArtifact("duelist-lens")
                    ? "\nESTILO  " + encounter.enemyDeckId
                    : string.Empty),
                18, FontStyle.Bold, Cyan,
                new Vector2(0.72f, 0.46f), new Vector2(0.96f, 0.90f),
                TextAnchor.MiddleCenter);
            Image duel = CreateButton(dialogue.transform,
                "ACEITAR DUELO",
                StoryRogueliteUiLayout.EncounterDuelButtonMin,
                StoryRogueliteUiLayout.EncounterDuelButtonMax,
                Gold, () => LaunchStoryDuel(encounter));
            DecorateRuntimeShopButton(duel, Gold, true, 8f);
        }

        private void LaunchStoryDuel(StoryEncounterDefinition encounter)
        {
            StoryRunSave save = _storyManager.Save;
            StoryDeckValidationResult validation = StoryRunManager.Rules
                .Validate(save.mainDeck, save.extraDeck, false);
            if (!validation.IsValid)
            {
                ShowStoryError(validation.Summary, ShowStoryDeckManagement);
                return;
            }
            var playerDeck = new DeckRecord
            {
                deckId = "story:" + save.runId,
                displayName = "Deck da Jornada",
                mainDeckCardIds = new List<string>(save.mainDeck),
                extraDeckCardIds = new List<string>(save.extraDeck)
            };
            var opponentDeck = new DeckRecord
            {
                deckId = encounter.enemyDeckId,
                displayName = "Deck de " + encounter.npcName,
                mainDeckCardIds = new List<string>(encounter.enemyMainDeck),
                extraDeckCardIds = new List<string>(encounter.enemyExtraDeck)
            };
            _pendingPlayerLoadout = DuelDeckLoadout.Create(
                save.profileId, playerDeck, save.playerName);
            _pendingPlayerLoadout.identity =
                _repository?.CaptureDuelIdentitySnapshot();
            _pendingBotLoadout = DuelDeckLoadout.Create(
                "story:" + encounter.npcId,
                opponentDeck,
                encounter.npcName);
            _pendingBotLoadout.identity = new DuelIdentitySnapshot
            {
                stablePlayerId = "story:" + encounter.npcId,
                nickname = encounter.npcName,
                equippedIconId = ProfileIconCatalog.ResolveForStableIdentity(
                    encounter.npcId),
                rankTier = RankRules.ResolveTier(encounter.aiTier * 25),
                rankedPoints = 0,
                cosmeticsCatalogVersion = ProfileIconCatalog.CatalogVersion
            };
            BotRuntimeSelection.Select(
                encounter.botProfileId,
                unchecked((int)StoryDeterminism.Hash(
                    save.seed, encounter.encounterId, "bot")));
            if (!_storyManager.MarkDuelStarted(
                    encounter.encounterId, out string rejection))
            {
                ShowStoryError(rejection,
                    () => ShowStoryNpcEncounter(encounter));
                return;
            }
            StoryRogueliteRuntime.PrepareDuel(new StoryDuelLaunchContext
            {
                runId = save.runId,
                encounterId = encounter.encounterId,
                playerLifePoints = encounter.playerLifePoints,
                opponentLifePoints = encounter.opponentLifePoints,
                minimumMainDeckSize = StoryRunManager.Rules.minimumMainDeckSize
            });
            _activeDuelStatisticsId = string.Empty;
            _activeDuelStatisticsRanked = false;
            _pendingDuelMode = PendingDuelMode.StoryRoguelite;
            _pendingStartingPlayer = 0;
            BeginOfflineDuelPrelude();
        }

        private void ShowStoryReward(StoryPendingReward reward)
        {
            ClearScreen();
            ClearStoryRuntimeSprites();
            _shopBackAction = ShowStoryMap;
            BuildStoryBackground("RECOMPENSA DA JORNADA");
            BuildProfessionalShopHeader(reward.title, ShowStoryMap);
            CreateText(_screenRoot,
                reward.allowMultiple
                    ? "LOJA TEMPORÁRIA · COMPRE COM FRAGMENTOS ARCANOS"
                    : (Math.Max(1, reward.maximumClaims) > 1
                        ? $"ESCOLHA {Math.Max(1, reward.maximumClaims)} CARTAS"
                        : "ESCOLHA UMA CARTA") +
                      " · VAI PARA A RESERVA" +
                      (reward.fragmentsAwarded > 0 ||
                       reward.accountCoinsAwarded > 0
                          ? $"   ·   +{reward.fragmentsAwarded} FRAGMENTOS" +
                            $"   ·   +{reward.accountCoinsAwarded} MOEDAS"
                          : string.Empty),
                18, FontStyle.Bold, Cyan,
                new Vector2(0.12f, 0.78f), new Vector2(0.88f, 0.86f),
                TextAnchor.MiddleCenter);
            int cardCount = Mathf.Max(1, reward.cardIds.Count);
            float tileWidth = cardCount > 3 ? 0.145f : 0.18f;
            float gap = cardCount > 3 ? 0.025f : 0.05f;
            float totalWidth = cardCount * tileWidth +
                               (cardCount - 1) * gap;
            float firstX = 0.5f - totalWidth * 0.5f;
            for (int index = 0; index < reward.cardIds.Count; index++)
            {
                string cardId = reward.cardIds[index];
                CardCatalogEntry entry = DeckRepository.ResolveCard(
                    _catalog, cardId);
                float x = firstX + index * (tileWidth + gap);
                Image tile = CreatePanel(_screenRoot,
                    entry?.DisplayName ?? cardId,
                    new Vector2(x, StoryRogueliteUiLayout.RewardTileY.x),
                    new Vector2(
                        x + tileWidth,
                        StoryRogueliteUiLayout.RewardTileY.y),
                    Color.clear);
                DecorateRuntimeShopSurface(tile, Gold, true, 12f);
                Image artwork = CreateCardArtwork(
                    tile.transform,
                    entry?.Artwork,
                    StoryRogueliteUiLayout.RewardCardMin,
                    StoryRogueliteUiLayout.RewardCardMax,
                    0f,
                    true);
                artwork.name = "Carta inspecionável · " +
                               (entry?.DisplayName ?? cardId);
                AddButtonBehaviour(
                    artwork,
                    () => ShowStoryCardDetails(
                        cardId,
                        () => ShowStoryReward(reward)));
                CreateText(
                    tile.transform,
                    "CLIQUE NA CARTA PARA VER O EFEITO",
                    9,
                    FontStyle.Bold,
                    Cyan,
                    new Vector2(0.06f, 0.19f),
                    new Vector2(0.94f, 0.245f),
                    TextAnchor.MiddleCenter).raycastTarget = false;
                int cost = index < reward.costs.Count
                    ? reward.costs[index]
                    : 0;
                bool sold = reward.claimedCardIds.Contains(
                    cardId, StringComparer.Ordinal);
                Image choose = CreateButton(tile.transform,
                    sold ? "ESGOTADO" : cost > 0
                        ? $"COMPRAR · {cost}"
                        : "ESCOLHER",
                    new Vector2(0.10f, 0.05f), new Vector2(0.90f, 0.19f),
                    !sold && cost <= _storyManager.Save.fragments
                        ? Lime
                        : Muted,
                    () =>
                    {
                        _storyManager.ClaimReward(cardId);
                        ShowStoryRoguelite();
                    });
                choose.GetComponent<Button>().interactable =
                    !sold && cost <= _storyManager.Save.fragments;
                DecorateRuntimeShopButton(choose,
                    !sold && cost <= _storyManager.Save.fragments
                        ? Lime
                        : Muted,
                    true, 7f);
            }
            if (reward.allowMultiple)
            {
                int rerollCost = _storyManager.MerchantRerollCost(reward);
                Image reroll = CreateButton(_screenRoot,
                    rerollCost == 0
                        ? "NOVAS OFERTAS · GRÁTIS"
                        : $"NOVAS OFERTAS · {rerollCost}",
                    new Vector2(0.22f, 0.09f), new Vector2(0.48f, 0.16f),
                    Cyan, () =>
                    {
                        if (_storyManager.RerollMerchant(out string rejection))
                            ShowStoryReward(_storyManager.Save.pendingReward);
                        else ShowStoryToast(rejection);
                    });
                reroll.GetComponent<Button>().interactable =
                    _storyManager.Save.fragments >= rerollCost;
                DecorateRuntimeShopButton(reroll, Cyan, true, 8f);
                Image leave = CreateButton(_screenRoot,
                    "SAIR DO MERCADOR",
                    new Vector2(0.52f, 0.09f), new Vector2(0.78f, 0.16f),
                    Gold, () =>
                    {
                        _storyManager.FinishPendingReward();
                        ShowStoryMap();
                    });
                DecorateRuntimeShopButton(leave, Gold, true, 8f);
            }
        }

        private void ShowStoryCardDetails(
            string cardId,
            Action returnAction)
        {
            CardCatalogEntry entry = DeckRepository.ResolveCard(
                _catalog,
                cardId);
            if (entry == null)
                return;

            Action safeReturn = returnAction ?? ShowStoryRoguelite;
            SetDuelPresentation(false);
            ClearScreen();
            _deckEditorSelectedCardId = cardId;
            _shopBackAction = safeReturn;
            BuildStoryBackground("DETALHES DA CARTA");
            BuildProfessionalShopHeader(entry.DisplayName, safeReturn);

            Image panel = CreatePanel(
                _screenRoot,
                "Detalhes da carta nas Crônicas",
                new Vector2(0.065f, 0.08f),
                new Vector2(0.935f, 0.82f),
                new Color(0.008f, 0.025f, 0.05f, 0.98f));
            DecorateRuntimeShopSurface(panel, Gold, false, 14f);
            AddOutline(
                panel.gameObject,
                new Color(Gold.r, Gold.g, Gold.b, 0.78f),
                new Vector2(3f, -3f));

            _deckEditorDetailArtwork = CreateCardArtwork(
                panel.transform,
                entry.Artwork,
                new Vector2(0.04f, 0.11f),
                new Vector2(0.37f, 0.92f),
                0f,
                true);
            _deckEditorDetailArtwork.name =
                "Carta ampliável das Crônicas";
            AddButtonBehaviour(
                _deckEditorDetailArtwork,
                OpenDeckEditorZoom);
            AddOutline(
                _deckEditorDetailArtwork.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f),
                new Vector2(2f, -2f));
            CreateText(
                panel.transform,
                "CLIQUE NA CARTA PARA AMPLIAR",
                12,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.04f, 0.035f),
                new Vector2(0.37f, 0.105f),
                TextAnchor.MiddleCenter);

            Text title = CreateText(
                panel.transform,
                entry.DisplayName,
                30,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.42f, 0.78f),
                new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleLeft);
            title.name = "Título da carta nas Crônicas";
            title.resizeTextMinSize = 22;

            Image metadataPanel = CreatePanel(
                panel.transform,
                "Metadados da carta nas Crônicas",
                new Vector2(0.42f, 0.66f),
                new Vector2(0.96f, 0.77f),
                new Color(0.025f, 0.11f, 0.16f, 0.96f));
            DecorateRuntimeShopSurface(metadataPanel, Gold, false, 7f);
            CreateText(
                metadataPanel.transform,
                $"{entry.TypeName}  •  ID {entry.OfficialCardId}",
                17,
                FontStyle.Bold,
                Cyan,
                new Vector2(0.035f, 0.08f),
                new Vector2(0.965f, 0.92f),
                TextAnchor.MiddleLeft);

            Image effectHeader = CreatePanel(
                panel.transform,
                "Cabeçalho do efeito nas Crônicas",
                new Vector2(0.42f, 0.59f),
                new Vector2(0.96f, 0.65f),
                new Color(Gold.r, Gold.g, Gold.b, 0.92f));
            CreateText(
                effectHeader.transform,
                "EFEITO DA CARTA",
                16,
                FontStyle.Bold,
                Ink,
                new Vector2(0.035f, 0f),
                new Vector2(0.965f, 1f),
                TextAnchor.MiddleLeft);

            Text effect = CreateScrollableText(
                panel.transform,
                "Efeito da carta nas Crônicas",
                new Vector2(0.42f, 0.10f),
                new Vector2(0.96f, 0.58f),
                19);
            effect.text = CardPresentationText.EffectPtBr(entry);
            effect.color = new Color(0.92f, 0.96f, 1f, 1f);
            effect.lineSpacing = 1.14f;
            ScrollRect effectScroll = effect.GetComponentInParent<ScrollRect>();
            if (effectScroll != null)
            {
                effectScroll.scrollSensitivity = 56f;
                effectScroll.verticalNormalizedPosition = 1f;
            }

            BuildDeckEditorZoomViewer();
        }

        private void ShowStoryChoice(StoryPendingChoice choice)
        {
            ClearScreen();
            _shopBackAction = ShowStoryMap;
            BuildStoryBackground("EVENTO DA JORNADA");
            BuildProfessionalShopHeader(choice.title, ShowStoryMap);
            Image panel = CreatePanel(_screenRoot,
                "Evento persistido",
                new Vector2(0.16f, 0.18f), new Vector2(0.84f, 0.78f),
                Color.clear);
            DecorateRuntimeShopSurface(panel, Gold, true, 16f);
            CreateText(panel.transform, choice.body,
                19, FontStyle.Normal, Color.white,
                new Vector2(0.08f, 0.66f), new Vector2(0.92f, 0.91f),
                TextAnchor.MiddleCenter);
            int optionCount = Mathf.Max(1, choice.options.Count);
            float optionHeight = 0.54f / optionCount - 0.018f;
            for (int index = 0; index < choice.options.Count; index++)
            {
                StoryChoiceOption option = choice.options[index];
                float yMax = 0.62f - index * (optionHeight + 0.018f);
                Image button = CreateButton(panel.transform,
                    option.label,
                    new Vector2(0.12f, yMax - optionHeight),
                    new Vector2(0.88f, yMax),
                    index == 0 ? Gold : Cyan,
                    () =>
                    {
                        _storyManager.ResolveChoice(option.optionId);
                        ShowStoryRoguelite();
                    });
                button.GetComponent<Button>().interactable =
                    _storyManager.Save.seals + option.sealDelta >= 1;
                DecorateRuntimeShopButton(button,
                    index == 0 ? Gold : Cyan, true, 8f);
                CreateText(button.transform, option.description,
                    14, FontStyle.Normal, Muted,
                    new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.36f),
                    TextAnchor.MiddleCenter);
            }
        }

        private void ShowStoryDeckManagement()
        {
            StoryRunSave save = _storyManager.Save;
            ClearScreen();
            _shopBackAction = ShowStoryMap;
            BuildStoryBackground("DECK DA JORNADA");
            BuildProfessionalShopHeader("DECK DA JORNADA", ShowStoryMap);
            _storyDeckStatus = CreateText(_screenRoot,
                $"PRINCIPAL  {save.mainDeck.Count} / 20–30    ·    " +
                $"RESERVA  {save.reserveCards.Count}",
                20, FontStyle.Bold, Cyan,
                new Vector2(0.07f, 0.81f), new Vector2(0.93f, 0.88f),
                TextAnchor.MiddleCenter);

            CreateText(_screenRoot,
                "DECK PRINCIPAL · CLIQUE PARA ENVIAR À RESERVA",
                16, FontStyle.Bold, Gold,
                new Vector2(0.05f, 0.74f), new Vector2(0.49f, 0.80f),
                TextAnchor.MiddleLeft);
            CreateText(_screenRoot,
                "RESERVA · CLIQUE PARA ADICIONAR AO DECK",
                16, FontStyle.Bold, Lime,
                new Vector2(0.52f, 0.74f), new Vector2(0.96f, 0.80f),
                TextAnchor.MiddleLeft);
            RectTransform main = CreateScrollGrid(_screenRoot,
                "Deck Principal da jornada",
                new Vector2(0.04f, 0.12f), new Vector2(0.49f, 0.73f),
                new Vector2(80f, 119f), new Vector2(8f, 9f), 5);
            foreach (string cardId in save.mainDeck.ToArray())
                CreateStoryDeckCard(main, cardId, Gold, () =>
                {
                    if (!_storyManager.MoveMainCardToReserve(
                            cardId, out string rejection))
                        _storyDeckStatus.text = rejection;
                    else ShowStoryDeckManagement();
                });
            RectTransform reserve = CreateScrollGrid(_screenRoot,
                "Reserva da jornada",
                new Vector2(0.51f, 0.12f), new Vector2(0.96f, 0.73f),
                new Vector2(80f, 119f), new Vector2(8f, 9f), 5);
            foreach (string cardId in save.reserveCards.ToArray())
                CreateStoryDeckCard(reserve, cardId, Lime, () =>
                {
                    if (!_storyManager.MoveReserveCardToMain(
                            cardId, out string rejection))
                        _storyDeckStatus.text = rejection;
                    else ShowStoryDeckManagement();
                });
        }

        private void CreateStoryDeckCard(
            Transform parent,
            string cardId,
            Color accent,
            Action action)
        {
            CardCatalogEntry entry = DeckRepository.ResolveCard(_catalog, cardId);
            Image card = CreateCardArtwork(parent, entry?.Artwork,
                Vector2.zero, Vector2.one, 0f, false);
            card.rectTransform.sizeDelta = new Vector2(80f, 119f);
            AddOutline(card.gameObject, accent, new Vector2(1f, -1f));
            AddButtonBehaviour(card, action);
        }

        private void ShowStoryAbandonConfirmation()
        {
            Image veil = CreatePanel(_screenRoot,
                "Confirmar abandono",
                Vector2.zero, Vector2.one,
                new Color(0f, 0f, 0f, 0.84f));
            veil.transform.SetAsLastSibling();
            Image modal = CreatePanel(veil.transform,
                "Abandonar jornada",
                new Vector2(0.30f, 0.32f), new Vector2(0.70f, 0.68f),
                Color.clear);
            DecorateRuntimeShopSurface(modal, Danger, true, 15f);
            CreateText(modal.transform,
                "DESISTIR DESTA JORNADA?",
                25, FontStyle.Bold, Danger,
                new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.90f),
                TextAnchor.MiddleCenter);
            CreateText(modal.transform,
                "Esta ação encerra definitivamente a tentativa atual. " +
                "Use SAIR DA RUN se quiser continuar depois.",
                18, FontStyle.Normal, Color.white,
                new Vector2(0.10f, 0.38f), new Vector2(0.90f, 0.66f),
                TextAnchor.MiddleCenter);
            CreateButton(modal.transform, "CANCELAR",
                new Vector2(0.08f, 0.10f), new Vector2(0.47f, 0.29f),
                Muted, () => Destroy(veil.gameObject));
            CreateButton(modal.transform, "DESISTIR",
                new Vector2(0.53f, 0.10f), new Vector2(0.92f, 0.29f),
                Danger, () =>
                {
                    _storyManager.Abandon();
                    ShowStoryRunSummary();
                });
        }

        private void ShowStoryRunSummary()
        {
            ClearScreen();
            ClearStoryRuntimeSprites();
            StoryRunSave save = _storyManager?.Save;
            BuildStoryBackground("FIM DA JORNADA");
            BuildProfessionalShopHeader("CRÔNICAS DO DUELO", ShowDuelHub);
            string title = save?.status == StoryRunStatus.Completed
                ? "JORNADA CONCLUÍDA"
                : save?.status == StoryRunStatus.Failed
                    ? "OS SELOS SE ROMPERAM"
                    : "JORNADA ENCERRADA";
            CreateText(_screenRoot, title,
                42, FontStyle.Bold,
                save?.status == StoryRunStatus.Completed ? Gold : Danger,
                new Vector2(0.18f, 0.56f), new Vector2(0.82f, 0.74f),
                TextAnchor.MiddleCenter);
            CreateText(_screenRoot,
                $"ATOS ALCANÇADOS  {save?.actIndex ?? 0}\n" +
                $"DUELISTAS DERROTADOS  {save?.defeatedNpcIds?.Count ?? 0}\n" +
                $"CARTAS NA RESERVA  {save?.reserveCards?.Count ?? 0}\n" +
                $"RELÍQUIAS  {save?.artifacts?.Count ?? 0}\n" +
                $"FRAGMENTOS FINAIS  {save?.fragments ?? 0}\n" +
                $"MOEDAS GANHAS  {save?.accountCoinsEarned ?? 0}",
                22, FontStyle.Bold, Color.white,
                new Vector2(0.27f, 0.25f), new Vector2(0.73f, 0.55f),
                TextAnchor.MiddleCenter);
            Image newRun = CreateButton(_screenRoot,
                "NOVA JORNADA",
                new Vector2(0.38f, 0.15f), new Vector2(0.62f, 0.23f),
                Lime, ShowStoryStarterSelection);
            DecorateRuntimeShopButton(newRun, Lime, true, 8f);
        }

        private void ShowStoryError(string message, Action back)
        {
            ClearScreen();
            BuildStoryBackground("CRÔNICAS DO DUELO");
            BuildProfessionalShopHeader("JORNADA INDISPONÍVEL", back);
            CreateText(_screenRoot, message,
                23, FontStyle.Bold, Danger,
                new Vector2(0.18f, 0.35f), new Vector2(0.82f, 0.65f),
                TextAnchor.MiddleCenter);
        }

        private void ShowStoryToast(string message)
        {
            Text toast = CreateText(_screenRoot, message,
                16, FontStyle.Bold, Gold,
                new Vector2(0.25f, 0.015f), new Vector2(0.75f, 0.07f),
                TextAnchor.MiddleCenter);
            toast.transform.SetAsLastSibling();
            Destroy(toast.gameObject, 2.8f);
        }

        private void ShowStoryMapGuide()
        {
            Image veil = CreatePanel(_screenRoot,
                "Guia da jornada",
                Vector2.zero, Vector2.one,
                new Color(0f, 0f, 0f, 0.86f));
            veil.transform.SetAsLastSibling();
            Image modal = CreatePanel(veil.transform,
                "Guia do mapa e dos recursos",
                new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.92f),
                Color.clear);
            DecorateRuntimeShopSurface(modal, Cyan, true, 16f);
            CreateText(modal.transform,
                "GUIA DO MAPA · CRÔNICAS DO DUELO",
                25, FontStyle.Bold, Gold,
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f),
                TextAnchor.MiddleCenter);
            Text guide = CreateScrollableText(
                modal.transform,
                "Explicações da jornada",
                new Vector2(0.05f, 0.13f),
                new Vector2(0.95f, 0.87f),
                15);
            guide.text =
                "OBJETIVO\nAtravesse as rotas e derrote o Chefe. Toda rota " +
                "possui pelo menos dois duelistas antes do Chefe. Os três " +
                "atos são gerados uma vez e permanecem iguais ao retomar o save.\n\n" +
                "VIDAS / SELOS DE DUELO\nSão as vidas da run. Sair ou fechar " +
                "o jogo durante um duelo comum conta como derrota, remove 1 " +
                "selo e devolve você ao mapa. Sair durante o duelo do Chefe " +
                "encerra a run imediatamente.\n\n" +
                "FRAGMENTOS ARCANOS\nMoeda temporária desta run. Serve para " +
                "comprar cartas e atualizar ofertas. Desaparece no fim da run.\n\n" +
                "MOEDAS DA CONTA\nSão as mesmas moedas usadas na loja normal. " +
                "Uma vitória comum concede de 1 a 5; Elite, Arena Final e " +
                "Chefe concedem de 10 a 25. A concessão é salva sem duplicar.\n\n" +
                "RELÍQUIAS\nMelhorias passivas válidas até o fim da run. Não " +
                "são cartas e não ocupam o deck. Seus efeitos ativos aparecem " +
                "na lateral do mapa.\n\n" +
                "PONTOS DO MAPA\n" +
                "DUELO: batalha comum.\n" +
                "ELITE / ARENA FINAL: batalha difícil, com melhores moedas.\n" +
                "CHEFE: conclui o ato; abandonar esse duelo perde a run.\n" +
                "MERCADOR: compra cartas com Fragmentos Arcanos.\n" +
                "PACOTE / COFRE / RUÍNAS: escolha recompensas para a reserva.\n" +
                "SANTUÁRIO: escolha uma relíquia passiva.\n" +
                "OFICINA / FORJA: organize ou fortaleça os recursos do deck.\n" +
                "FONTE / ACAMPAMENTO: recuperação e preparação.\n" +
                "MISTÉRIO / ALTAR: evento de risco e recompensa.";
            Image close = CreateButton(modal.transform,
                "FECHAR",
                new Vector2(0.38f, 0.035f), new Vector2(0.62f, 0.105f),
                Cyan, () => Destroy(veil.gameObject));
            DecorateRuntimeShopButton(close, Cyan, true, 7f);
        }

        private void FlushStoryAccountCoinRewards()
        {
            if (_storyManager?.Save == null || _repository == null) return;
            foreach (StoryAccountCoinReward reward in
                     _storyManager.PendingAccountCoinRewards.ToArray())
            {
                if (reward == null) continue;
                if (_repository.TryGrantStoryRogueliteCoins(
                        reward.operationId,
                        reward.amount,
                        out _,
                        out string rejection))
                {
                    _storyManager.AcknowledgeAccountCoinReward(
                        reward.operationId);
                }
                else
                {
                    Debug.LogWarning("[Story Roguelite] " + rejection);
                }
            }
        }

        private void BuildStoryBackground(string section)
        {
            BuildShopBackground(section);
            Transform background = _screenRoot?.Find("Fundo");
            if (background == null)
                return;

            int act = Mathf.Clamp(_storyManager?.Save?.actIndex ?? 1, 1, 3);
            Sprite atmosphere = StoryLoadSprite(
                $"StoryRoguelite/Backgrounds/ChroniclesTowerAct{act}");
            Image artwork = background.Find("Arte de Fundo da Loja")
                ?.GetComponent<Image>();
            if (artwork != null && atmosphere != null)
            {
                artwork.sprite = atmosphere;
                artwork.preserveAspect = true;
                RectTransform artRect = artwork.rectTransform;
                artRect.anchorMin = Vector2.zero;
                artRect.anchorMax = Vector2.one;
                artRect.offsetMin = new Vector2(-113.2444f, 0f);
                artRect.offsetMax = new Vector2(113.2456f, 0f);
                AspectRatioFitter fitter =
                    artwork.GetComponent<AspectRatioFitter>();
                if (fitter != null)
                    fitter.aspectRatio =
                        atmosphere.rect.width / atmosphere.rect.height;
            }

            Image veil = background.Find("Contraste da Arte da Loja")
                ?.GetComponent<Image>();
            if (veil != null)
            {
                RectTransform veilRect = veil.rectTransform;
                veilRect.anchorMin = Vector2.zero;
                veilRect.anchorMax = Vector2.one;
                veilRect.pivot = new Vector2(0.5f, 0.5f);
                veilRect.offsetMin = new Vector2(-113.2444f, 0f);
                veilRect.offsetMax = new Vector2(113.2456f, 0f);
                veilRect.localScale = Vector3.one;
                veil.color = new Color32(3, 2, 3, 219);
            }

            Image betaNotice = CreateArcaneSurface(
                _screenRoot,
                "Aviso Beta das Crônicas",
                new Vector2(0.735f, 0.910f),
                new Vector2(0.965f, 0.975f),
                ArcaneGold,
                true,
                0.90f);
            betaNotice.raycastTarget = false;
            CreateText(
                betaNotice.transform,
                "BETA  •  CONTEÚDO EM DESENVOLVIMENTO",
                12,
                FontStyle.Bold,
                new Color(0.98f, 0.82f, 0.52f, 1f),
                new Vector2(0.05f, 0.08f),
                new Vector2(0.95f, 0.92f),
                TextAnchor.MiddleCenter)
                .raycastTarget = false;
        }

        private Sprite StoryLoadSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null) return sprite;
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            sprite.name = "Story · " + resourcePath;
            sprite.hideFlags = HideFlags.DontSave;
            _storyRuntimeSprites.Add(sprite);
            return sprite;
        }

        private void ClearStoryRuntimeSprites()
        {
            foreach (Sprite sprite in _storyRuntimeSprites)
                if (sprite != null) Destroy(sprite);
            _storyRuntimeSprites.Clear();
        }

        private static void SetStoryMarkerPosition(
            RectTransform marker,
            Vector2 position)
        {
            Vector2 half = StoryRogueliteUiLayout.MarkerHalfSize;
            Vector4 offsets = StoryRogueliteUiLayout.MarkerRectOffsets;
            marker.anchorMin = position - half;
            marker.anchorMax = position + half;
            marker.offsetMin = new Vector2(offsets.x, offsets.w);
            marker.offsetMax = new Vector2(-offsets.z, -offsets.y);
        }

        private static Color StoryNodeColor(StoryRuntimeNode node)
        {
            if (node.state == RogueliteNodeState.Current) return Gold;
            if (node.state == RogueliteNodeState.Available) return Cyan;
            if (node.state == RogueliteNodeState.Completed)
                return new Color(0.18f, 0.86f, 0.55f, 1f);
            return new Color(0.28f, 0.34f, 0.40f, 1f);
        }

        private static string StoryNodeGlyph(RogueliteNodeType type)
        {
            return type switch
            {
                RogueliteNodeType.Start => "◆",
                RogueliteNodeType.NormalDuel => "⚔",
                RogueliteNodeType.EliteDuel => "★",
                RogueliteNodeType.FinalDuelArena => "♜",
                RogueliteNodeType.Boss => "CHEFE",
                RogueliteNodeType.CardMerchant => "$",
                RogueliteNodeType.CardPack => "+",
                RogueliteNodeType.SpellRuins => "M/A",
                RogueliteNodeType.TreasureVault => "◇",
                RogueliteNodeType.RelicShrine => "✦",
                RogueliteNodeType.DeckWorkshop => "D",
                RogueliteNodeType.DeckForge => "F",
                RogueliteNodeType.HealingSpring => "♥",
                RogueliteNodeType.RestCamp => "△",
                RogueliteNodeType.MysteryEvent => "?",
                RogueliteNodeType.ForbiddenAltar => "!",
                _ => "?"
            };
        }

        private static string StoryNodeShortLabel(RogueliteNodeType type)
        {
            return type switch
            {
                RogueliteNodeType.Start => "INÍCIO",
                RogueliteNodeType.NormalDuel => "DUELO",
                RogueliteNodeType.EliteDuel => "ELITE",
                RogueliteNodeType.FinalDuelArena => "ARENA",
                RogueliteNodeType.Boss => "CHEFE",
                RogueliteNodeType.CardMerchant => "LOJA",
                RogueliteNodeType.CardPack => "PACOTE",
                RogueliteNodeType.SpellRuins => "RUÍNAS",
                RogueliteNodeType.TreasureVault => "COFRE",
                RogueliteNodeType.RelicShrine => "RELÍQUIA",
                RogueliteNodeType.DeckWorkshop => "OFICINA",
                RogueliteNodeType.DeckForge => "FORJA",
                RogueliteNodeType.HealingSpring => "FONTE",
                RogueliteNodeType.RestCamp => "DESCANSO",
                RogueliteNodeType.ForbiddenAltar => "ALTAR",
                RogueliteNodeType.MysteryEvent => "EVENTO",
                _ => "?"
            };
        }

        private static string StoryNodeDescription(RogueliteNodeType type)
        {
            return type switch
            {
                RogueliteNodeType.NormalDuel =>
                    "Duelo comum. A vitória concede 1–5 moedas da conta.",
                RogueliteNodeType.EliteDuel =>
                    "Duelo difícil. A vitória concede 10–25 moedas da conta.",
                RogueliteNodeType.FinalDuelArena =>
                    "Arena de alta dificuldade com recompensa de 10–25 moedas.",
                RogueliteNodeType.Boss =>
                    "Chefe do ato. Sair durante este duelo encerra a run.",
                RogueliteNodeType.CardMerchant =>
                    "Compre cartas usando os Fragmentos Arcanos desta run.",
                RogueliteNodeType.CardPack =>
                    "Escolha uma carta e envie-a para a reserva da jornada.",
                RogueliteNodeType.SpellRuins =>
                    "Encontre opções de Magias e Armadilhas para a reserva.",
                RogueliteNodeType.TreasureVault =>
                    "Abra um cofre com opções de recompensa.",
                RogueliteNodeType.RelicShrine =>
                    "Escolha uma melhoria passiva válida até o fim da run.",
                RogueliteNodeType.DeckWorkshop =>
                    "Ponto de preparação e organização do deck.",
                RogueliteNodeType.DeckForge =>
                    "Ponto de preparação antes dos próximos combates.",
                RogueliteNodeType.HealingSpring =>
                    "Permite recuperar um Selo de Duelo ou obter fragmentos.",
                RogueliteNodeType.RestCamp =>
                    "Pausa segura para recuperar ou procurar recursos.",
                RogueliteNodeType.ForbiddenAltar =>
                    "Evento de alto risco que troca segurança por recursos.",
                RogueliteNodeType.MysteryEvent =>
                    "Evento procedural com escolha de risco e recompensa.",
                RogueliteNodeType.Mystery =>
                    "O conteúdo deste ponto será revelado ao chegar.",
                _ => "Ponto de progressão da jornada."
            };
        }

        private static string StoryRelicSummary(StoryRunSave save)
        {
            IReadOnlyList<StoryRelicRuntimeState> active =
                StoryRelicService.Active(save);
            if (active.Count == 0)
                return "Nenhuma relíquia. Relíquias são melhorias passivas " +
                       "que duram até o fim da run e não ocupam o deck.";
            return string.Join("\n\n", active.Select(state =>
            {
                StoryRelicDefinition definition =
                    StoryRelicLibrary.Resolve(state.relicId);
                string charges = definition.initialCharges > 0
                    ? $" · CARGAS {state.chargesRemaining}/{definition.initialCharges}"
                    : string.Empty;
                string unavailable = definition.IsAvailable
                    ? string.Empty
                    : "\nINDISPONÍVEL NESTA VERSÃO: " +
                      definition.disabledReason;
                return definition.displayName.ToUpperInvariant() + "\n" +
                       definition.shortEffect + charges + unavailable;
            }));
        }

        private static string StoryNodeLabel(RogueliteNodeType type)
        {
            return type switch
            {
                RogueliteNodeType.NormalDuel => "duelo normal",
                RogueliteNodeType.EliteDuel => "duelo de elite",
                RogueliteNodeType.FinalDuelArena => "arena final",
                RogueliteNodeType.Boss => "chefe",
                _ => type.ToString()
            };
        }
    }
}
