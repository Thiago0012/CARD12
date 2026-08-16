using System;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private enum ProfileTab
        {
            Overview,
            Statistics,
            Icons
        }

        private ProfileTab _profileTab = ProfileTab.Overview;

        private void ShowPlayerProfileSetup(bool canReturn = false)
        {
            if (_repository?.HasPlayerProfile == true)
                ShowPlayerProfile();
            else
                ShowPlayerNameEditor(canReturn);
        }

        private void ShowPlayerProfile()
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildSharedBackground("PERFIL DO DUELISTA");
            BuildHeader(
                "PERFIL",
                ShowMainMenu);

            Image identity = CreatePanel(
                _screenRoot,
                "Identidade do Duelista",
                new Vector2(0.055f, 0.12f),
                new Vector2(0.33f, 0.84f),
                new Color(0.008f, 0.025f, 0.05f, 0.98f));
            AddOutline(identity.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f),
                new Vector2(2f, -2f));
            RankPresentationModel rank = _repository.GetRankPresentation();
            Text nickname = CreateText(
                identity.transform,
                _repository.PlayerDisplayName,
                30,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.84f),
                new Vector2(0.93f, 0.96f),
                TextAnchor.MiddleCenter);
            nickname.resizeTextMinSize = 18;
            nickname.horizontalOverflow = HorizontalWrapMode.Wrap;
            nickname.verticalOverflow = VerticalWrapMode.Truncate;

            Image rankRow = CreatePanel(
                identity.transform,
                "Elo do Duelista",
                new Vector2(0.09f, 0.72f),
                new Vector2(0.91f, 0.83f),
                new Color(0.015f, 0.055f, 0.09f, 0.82f));
            CreateRankBadgeImage(
                rankRow.transform,
                "Símbolo do Elo",
                rank.Tier,
                new Vector2(0.08f, 0.08f),
                new Vector2(0.28f, 0.92f),
                1f);
            CreateText(
                rankRow.transform,
                $"{RankRules.DisplayName(rank.Tier)}  •  {rank.Points} PE",
                17,
                FontStyle.Bold,
                Gold,
                new Vector2(0.30f, 0.08f),
                new Vector2(0.96f, 0.92f),
                TextAnchor.MiddleLeft);

            CreateBoundedHexIcon(
                identity.transform,
                "Ícone Equipado",
                _repository.EquippedIconId,
                new Vector2(0.30f, 0.38f),
                new Vector2(0.70f, 0.55f));
            CreateButton(identity.transform, "EDITAR NOME",
                new Vector2(0.16f, 0.06f), new Vector2(0.84f, 0.18f),
                Cyan, () => ShowPlayerNameEditor(true));

            Image detail = CreatePanel(
                _screenRoot,
                "Dados do Perfil",
                new Vector2(0.35f, 0.12f),
                new Vector2(0.945f, 0.84f),
                new Color(0.008f, 0.025f, 0.05f, 0.98f));
            AddOutline(detail.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.6f),
                new Vector2(2f, -2f));
            CreateProfileTabButton(detail.transform, "VISÃO GERAL",
                ProfileTab.Overview, 0.03f, 0.32f);
            CreateProfileTabButton(detail.transform, "ESTATÍSTICAS",
                ProfileTab.Statistics, 0.34f, 0.65f);
            CreateProfileTabButton(detail.transform, "ÍCONES",
                ProfileTab.Icons, 0.67f, 0.97f);

            if (_profileTab == ProfileTab.Overview)
                BuildProfileOverview(detail.transform, rank);
            else if (_profileTab == ProfileTab.Statistics)
                BuildProfileStatistics(detail.transform);
            else
                BuildOwnedIcons(detail.transform);
        }

        private void CreateProfileTabButton(
            Transform parent,
            string label,
            ProfileTab tab,
            float minX,
            float maxX)
        {
            CreateButton(parent, label, new Vector2(minX, 0.84f),
                new Vector2(maxX, 0.96f), _profileTab == tab ? Lime : Cyan,
                () => { _profileTab = tab; ShowPlayerProfile(); });
        }

        private void BuildProfileOverview(
            Transform parent,
            RankPresentationModel rank)
        {
            DuelStatisticsScope stats = _repository.Statistics?.overall ??
                new DuelStatisticsScope();
            CreateText(parent, "RESUMO DO DUELISTA", 28, FontStyle.Bold,
                Color.white, new Vector2(0.06f, 0.69f),
                new Vector2(0.94f, 0.81f), TextAnchor.MiddleLeft);
            CreateText(parent,
                $"Elo atual\n{RankRules.DisplayName(rank.Tier)}\n\n" +
                $"Pontos ranqueados\n{rank.Points} PE\n\n" +
                $"Duelos registrados\n{stats.duelsPlayed:N0}",
                21, FontStyle.Bold, Muted, new Vector2(0.07f, 0.17f),
                new Vector2(0.48f, 0.68f), TextAnchor.UpperLeft);
            CreateText(parent,
                $"Vitórias\n{stats.wins:N0}\n\n" +
                $"Derrotas\n{stats.losses:N0}\n\n" +
                $"Empates\n{stats.draws:N0}",
                21, FontStyle.Bold, Muted, new Vector2(0.52f, 0.17f),
                new Vector2(0.93f, 0.68f), TextAnchor.UpperLeft);
        }

        private void BuildProfileStatistics(Transform parent)
        {
            DuelStatisticsScope all = _repository.Statistics?.overall ??
                new DuelStatisticsScope();
            Canvas.ForceUpdateCanvases();
            float measuredWidth = parent is RectTransform parentRect
                ? parentRect.rect.width * 0.90f
                : 0f;
            float availableWidth = measuredWidth >= 280f
                ? measuredWidth
                : 900f;
            int summaryColumns = availableWidth >= 880f
                ? 4
                : availableWidth >= 480f
                    ? 2
                    : 1;
            int detailColumns = availableWidth >= 700f ? 2 : 1;

            RectTransform content = CreateStatisticsScrollContent(parent);
            AddStatisticsHeading(content, all);

            float summaryCellWidth =
                (availableWidth - 24f * (summaryColumns - 1)) /
                summaryColumns;
            GridLayoutGroup summary = CreateStatisticsGrid(
                content,
                "Resumo das Estatísticas",
                summaryColumns,
                new Vector2(summaryCellWidth, 92f),
                92f * Mathf.CeilToInt(4f / summaryColumns) + 18f);
            AddStatisticsSummaryCard(
                summary.transform,
                "DUELOS",
                all.duelsPlayed.ToString("N0"),
                Cyan);
            AddStatisticsSummaryCard(
                summary.transform,
                "VITÓRIAS",
                all.wins.ToString("N0"),
                Lime);
            AddStatisticsSummaryCard(
                summary.transform,
                "DERROTAS",
                all.losses.ToString("N0"),
                Danger);
            double winRate = all.duelsPlayed > 0
                ? all.wins * 100.0 / all.duelsPlayed
                : 0.0;
            AddStatisticsSummaryCard(
                summary.transform,
                "TAXA DE VITÓRIA",
                $"{winRate:0.#}%",
                Gold);

            float detailCellWidth =
                (availableWidth - 24f * (detailColumns - 1)) /
                detailColumns;
            int detailRows = Mathf.CeilToInt(4f / detailColumns);
            GridLayoutGroup details = CreateStatisticsGrid(
                content,
                "Grupos de Estatísticas",
                detailColumns,
                new Vector2(detailCellWidth, 250f),
                detailRows * 250f + Mathf.Max(0, detailRows - 1) * 20f + 18f);
            AddStatisticsMetricCard(
                details.transform,
                "COMBATE",
                $"Dano causado\n{all.damageDealt:N0}\n\n" +
                $"Dano recebido\n{all.damageReceived:N0}\n\n" +
                $"Maior dano causado em um duelo\n" +
                $"{all.maxDamageDealtInSingleDuel:N0}\n\n" +
                $"Maior dano recebido em um duelo\n" +
                $"{all.maxDamageReceivedInSingleDuel:N0}",
                Gold);
            AddDuelProfileRadar(details.transform, all);
            AddStatisticsMetricCard(
                details.transform,
                "INVOCAÇÕES E BATALHA",
                $"Monstros invocados\n{all.monstersSummoned:N0}\n\n" +
                $"Invocações-Especiais\n{all.specialSummons:N0}\n\n" +
                $"Destruídos por batalha\n" +
                $"{all.monstersDestroyedByBattle:N0}\n\n" +
                $"Destruídos por efeito\n" +
                $"{all.monstersDestroyedByEffect:N0}",
                Cyan);
            AddStatisticsMetricCard(
                details.transform,
                "MAGIAS E ARMADILHAS",
                $"Magias ativadas\n{all.spellsActivated:N0}\n\n" +
                $"Armadilhas ativadas\n{all.trapsActivated:N0}\n\n" +
                $"Magias destruídas\n{all.spellsDestroyed:N0}\n\n" +
                $"Armadilhas destruídas\n{all.trapsDestroyed:N0}",
                Blue);
        }

        private static RectTransform CreateStatisticsScrollContent(
            Transform parent)
        {
            Image viewport = CreatePanel(
                parent,
                "Estatísticas Gerais",
                new Vector2(0.025f, 0.045f),
                new Vector2(0.975f, 0.815f),
                new Color(0.003f, 0.014f, 0.026f, 0.78f));
            viewport.gameObject.AddComponent<RectMask2D>();

            GameObject contentObject = new(
                "Conteúdo das Estatísticas Gerais",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(22f, 0f);
            content.offsetMax = new Vector2(-38f, 0f);
            VerticalLayoutGroup layout =
                contentObject.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 12, 20);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter =
                contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 46f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            Image track = CreatePanel(
                viewport.transform,
                "Rolagem das Estatísticas",
                new Vector2(0.975f, 0.03f),
                new Vector2(0.992f, 0.97f),
                new Color(0.04f, 0.12f, 0.17f, 0.85f));
            Image handle = CreatePanel(
                track.transform,
                "Alça",
                new Vector2(0.08f, 0f),
                new Vector2(0.92f, 0.32f),
                Cyan);
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private static void AddStatisticsHeading(
            Transform parent,
            DuelStatisticsScope stats)
        {
            Image heading = CreateLayoutStatisticsPanel(
                parent,
                "Cabeçalho das Estatísticas",
                88f,
                new Color(0.01f, 0.06f, 0.095f, 0.94f));
            CreateText(
                heading.transform,
                "ESTATÍSTICAS GERAIS",
                27,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.025f, 0.46f),
                new Vector2(0.975f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateText(
                heading.transform,
                stats.duelsPlayed == 0
                    ? "Nenhum duelo registrado. O perfil será preenchido ao concluir partidas válidas."
                    : $"{stats.duelsPlayed:N0} duelo(s) válido(s) • " +
                      $"{stats.draws:N0} empate(s)",
                15,
                FontStyle.Bold,
                Muted,
                new Vector2(0.025f, 0.08f),
                new Vector2(0.975f, 0.47f),
                TextAnchor.MiddleLeft);
        }

        private static GridLayoutGroup CreateStatisticsGrid(
            Transform parent,
            string name,
            int columns,
            Vector2 cellSize,
            float height)
        {
            GameObject gridObject = new(
                name,
                typeof(RectTransform),
                typeof(GridLayoutGroup),
                typeof(LayoutElement));
            gridObject.transform.SetParent(parent, false);
            LayoutElement element = gridObject.GetComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            GridLayoutGroup grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(24f, 20f);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, columns);
            grid.childAlignment = TextAnchor.UpperCenter;
            return grid;
        }

        private static Image CreateLayoutStatisticsPanel(
            Transform parent,
            string name,
            float height,
            Color color)
        {
            Image panel = CreatePanel(
                parent,
                name,
                Vector2.zero,
                Vector2.one,
                color);
            LayoutElement element = panel.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return panel;
        }

        private static void AddStatisticsSummaryCard(
            Transform parent,
            string label,
            string value,
            Color accent)
        {
            Image card = CreatePanel(
                parent,
                label,
                Vector2.zero,
                Vector2.one,
                new Color(0.012f, 0.052f, 0.082f, 0.98f));
            AddOutline(card.gameObject, accent, new Vector2(2f, -2f));
            CreateText(card.transform, label, 14, FontStyle.Bold, Muted,
                new Vector2(0.06f, 0.58f), new Vector2(0.94f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateText(card.transform, value, 28, FontStyle.Bold, accent,
                new Vector2(0.06f, 0.10f), new Vector2(0.94f, 0.62f),
                TextAnchor.MiddleCenter);
        }

        private static void AddStatisticsMetricCard(
            Transform parent,
            string title,
            string body,
            Color accent)
        {
            Image card = CreatePanel(
                parent,
                title,
                Vector2.zero,
                Vector2.one,
                new Color(0.008f, 0.035f, 0.058f, 0.98f));
            AddOutline(card.gameObject, accent, new Vector2(2f, -2f));
            CreateText(card.transform, title, 19, FontStyle.Bold, accent,
                new Vector2(0.055f, 0.81f), new Vector2(0.945f, 0.95f),
                TextAnchor.MiddleLeft);
            Text metric = CreateText(
                card.transform,
                body,
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.055f, 0.07f),
                new Vector2(0.945f, 0.81f),
                TextAnchor.UpperLeft);
            metric.lineSpacing = 0.90f;
            metric.resizeTextMinSize = 13;
        }

        private static void AddDuelProfileRadar(
            Transform parent,
            DuelStatisticsScope stats)
        {
            Image card = CreatePanel(
                parent,
                "Perfil de Duelo",
                Vector2.zero,
                Vector2.one,
                new Color(0.008f, 0.035f, 0.058f, 0.98f));
            AddOutline(card.gameObject, Cyan, new Vector2(2f, -2f));
            CreateText(card.transform, "PERFIL DE DUELO", 19,
                FontStyle.Bold, Cyan, new Vector2(0.055f, 0.84f),
                new Vector2(0.945f, 0.96f), TextAnchor.MiddleLeft);

            GameObject radarObject = new(
                "Gráfico Radar",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DuelProfileRadarGraphic));
            radarObject.transform.SetParent(card.transform, false);
            RectTransform radarRect = radarObject.GetComponent<RectTransform>();
            radarRect.anchorMin = new Vector2(0.30f, 0.20f);
            radarRect.anchorMax = new Vector2(0.70f, 0.68f);
            radarRect.offsetMin = Vector2.zero;
            radarRect.offsetMax = Vector2.zero;
            DuelProfileRadarGraphic radar =
                radarObject.GetComponent<DuelProfileRadarGraphic>();
            radar.raycastTarget = false;

            float duels = Mathf.Max(1f, stats.duelsPlayed);
            DuelStatsVisualizationConfig config =
                DuelStatsVisualizationConfig.Resolve();
            float[] raw =
            {
                stats.damageDealt / duels,
                stats.monstersSummoned / duels,
                stats.monstersDestroyedByBattle / duels,
                stats.monstersDestroyedByEffect / duels,
                (stats.spellsActivated + stats.spellsDestroyed) / duels,
                (stats.trapsActivated + stats.trapsDestroyed) / duels
            };
            float[] values = stats.duelsPlayed == 0
                ? new float[6]
                : new[]
                {
                    DuelStatsVisualizationConfig.Normalize(
                        raw[0], config.damagePerDuelCap),
                    DuelStatsVisualizationConfig.Normalize(
                        raw[1], config.summonsPerDuelCap),
                    DuelStatsVisualizationConfig.Normalize(
                        raw[2], config.battleDestroysPerDuelCap),
                    DuelStatsVisualizationConfig.Normalize(
                        raw[3], config.effectDestroysPerDuelCap),
                    DuelStatsVisualizationConfig.Normalize(
                        raw[4], config.spellActionsPerDuelCap),
                    DuelStatsVisualizationConfig.Normalize(
                        raw[5], config.trapActionsPerDuelCap)
                };
            radar.SetValues(values);

            AddRadarLabel(card.transform, "DANO", raw[0],
                new Vector2(0.39f, 0.73f), new Vector2(0.61f, 0.84f));
            AddRadarLabel(card.transform, "INVOCAÇÕES", raw[1],
                new Vector2(0.70f, 0.59f), new Vector2(0.98f, 0.72f));
            AddRadarLabel(card.transform, "BATALHA", raw[2],
                new Vector2(0.72f, 0.23f), new Vector2(0.98f, 0.38f));
            AddRadarLabel(card.transform, "EFEITOS", raw[3],
                new Vector2(0.39f, 0.03f), new Vector2(0.61f, 0.16f));
            AddRadarLabel(card.transform, "MAGIAS", raw[4],
                new Vector2(0.02f, 0.23f), new Vector2(0.28f, 0.38f));
            AddRadarLabel(card.transform, "ARMADILHAS", raw[5],
                new Vector2(0.02f, 0.59f), new Vector2(0.30f, 0.72f));
        }

        private static void AddRadarLabel(
            Transform parent,
            string label,
            float value,
            Vector2 min,
            Vector2 max)
        {
            CreateText(
                parent,
                $"{label}\n{value:0.#}/duelo",
                12,
                FontStyle.Bold,
                Color.white,
                min,
                max,
                TextAnchor.MiddleCenter);
        }

        private void BuildOwnedIcons(Transform parent)
        {
            RectTransform grid = CreateScrollGrid(parent, "Ícones Possuídos",
                new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.80f),
                new Vector2(180f, 205f), new Vector2(18f, 18f), 4);
            foreach (ProfileIconDefinition icon in ProfileIconCatalog.All)
            {
                if (_repository.OwnsIcon(icon.IconId))
                    CreateOwnedIconTile(grid, icon);
            }
        }

        private void CreateOwnedIconTile(
            Transform parent,
            ProfileIconDefinition icon)
        {
            bool equipped = string.Equals(_repository.EquippedIconId,
                icon.IconId, StringComparison.Ordinal);
            Image tile = CreateShopTile(parent, icon.DisplayName,
                equipped ? Lime : Cyan);
            CreateBoundedHexIcon(tile.transform, icon.DisplayName, icon.IconId,
                new Vector2(0.16f, 0.27f), new Vector2(0.84f, 0.90f));
            CreateText(tile.transform, icon.DisplayName, 14, FontStyle.Bold,
                Color.white, new Vector2(0.04f, 0.10f),
                new Vector2(0.96f, 0.28f), TextAnchor.MiddleCenter);
            AddButtonBehaviour(tile, () =>
            {
                if (_repository.TryEquipIcon(icon.IconId, out string rejection))
                    ShowPlayerProfile();
                else
                {
                    _shopFeedback = rejection;
                    _shopFeedbackIsError = true;
                }
            });
            if (equipped)
                CreateText(tile.transform, "EQUIPADO", 11, FontStyle.Bold,
                    Lime, new Vector2(0.04f, 0.01f),
                    new Vector2(0.96f, 0.11f), TextAnchor.MiddleCenter);
        }

        private void CreateProfileIconShopTile(
            Transform parent,
            ProfileIconDefinition icon)
        {
            bool owned = _repository.OwnsIcon(icon.IconId);
            bool equipped = string.Equals(_repository.EquippedIconId,
                icon.IconId, StringComparison.Ordinal);
            Image tile = CreateShopTile(parent, icon.DisplayName,
                equipped ? Lime : Cyan);
            CreateBoundedHexIcon(tile.transform, icon.DisplayName, icon.IconId,
                new Vector2(0.24f, 0.33f), new Vector2(0.76f, 0.91f));
            CreateText(tile.transform, icon.DisplayName, 17, FontStyle.Bold,
                Color.white, new Vector2(0.04f, 0.21f),
                new Vector2(0.96f, 0.34f), TextAnchor.MiddleCenter);
            if (owned)
            {
                string action = equipped ? "EQUIPADO" : "EQUIPAR";
                CreateButton(tile.transform, action,
                    new Vector2(0.08f, 0.04f),
                    new Vector2(0.92f, 0.20f), Lime, () =>
                    HandleProfileIconShopAction(icon));
            }
            else
            {
                CreateShopPriceButton(tile.transform, "COMPRAR",
                    ProfileIconCatalog.IconPriceCoins,
                    new Vector2(0.08f, 0.04f),
                    new Vector2(0.92f, 0.20f), Gold, () =>
                    HandleProfileIconShopAction(icon));
            }
        }

        private void HandleProfileIconShopAction(ProfileIconDefinition icon)
        {
            if (_repository.OwnsIcon(icon.IconId))
            {
                if (!_repository.TryEquipIcon(icon.IconId, out string rejection))
                {
                    _shopFeedback = rejection;
                    _shopFeedbackIsError = true;
                }
                else
                {
                    _shopFeedback = $"{icon.DisplayName} equipado.";
                    _shopFeedbackIsError = false;
                }
                ShowEconomyShop();
                return;
            }
            ShowProfileIconPurchaseConfirmation(icon);
        }

        private void ShowProfileIconPurchaseConfirmation(
            ProfileIconDefinition icon)
        {
            SetDuelPresentation(false);
            ClearScreen();
            BuildShopBackground("CONFIRMAR COMPRA");
            BuildHeader(icon.DisplayName, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);
            Image panel = CreatePanel(_screenRoot, "Confirmação do Ícone",
                new Vector2(0.29f, 0.16f), new Vector2(0.71f, 0.80f),
                new Color(0.008f, 0.025f, 0.05f, 0.99f));
            AddOutline(panel.gameObject, Cyan, new Vector2(2f, -2f));
            CreateHexIcon(panel.transform, icon.DisplayName, icon.IconId,
                new Vector2(0.25f, 0.37f), new Vector2(0.75f, 0.88f));
            CreateText(panel.transform, icon.DisplayName, 27, FontStyle.Bold,
                Color.white, new Vector2(0.06f, 0.25f),
                new Vector2(0.94f, 0.38f), TextAnchor.MiddleCenter);
            CreateShopPriceButton(panel.transform, "COMPRAR POR",
                ProfileIconCatalog.IconPriceCoins,
                new Vector2(0.13f, 0.07f), new Vector2(0.87f, 0.22f), Gold,
                () =>
                {
                    bool ok = _repository.TryPurchaseIcon(icon.IconId,
                        Guid.NewGuid().ToString("N"), out _, out string rejection);
                    _shopFeedback = ok
                        ? $"{icon.DisplayName} adquirido."
                        : rejection;
                    _shopFeedbackIsError = !ok;
                    ShowEconomyShop();
                });
        }

        private static HexIconView CreateHexIcon(
            Transform parent,
            string name,
            string iconId,
            Vector2 min,
            Vector2 max)
        {
            GameObject item = new(name, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(HexIconView));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            HexIconView view = item.GetComponent<HexIconView>();
            view.SetIcon(iconId);
            return view;
        }

        private static HexIconView CreateBoundedHexIcon(
            Transform parent,
            string name,
            string iconId,
            Vector2 min,
            Vector2 max)
        {
            GameObject slot = new(
                $"{name} - Área Reservada",
                typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            slotRect.anchorMin = min;
            slotRect.anchorMax = max;
            slotRect.offsetMin = Vector2.zero;
            slotRect.offsetMax = Vector2.zero;
            return CreateHexIcon(
                slot.transform,
                name,
                iconId,
                Vector2.zero,
                Vector2.one);
        }

        public void DecorateMainMenuProfileButton(Button profileButton)
        {
            if (profileButton == null || _repository == null)
                return;
            Transform prior = profileButton.transform.Find("Perfil Equipado");
            HexIconView view = prior != null
                ? prior.GetComponent<HexIconView>()
                : CreateHexIcon(profileButton.transform, "Perfil Equipado",
                    _repository.EquippedIconId, new Vector2(0.08f, 0.08f),
                    new Vector2(0.92f, 0.92f));
            view?.SetIcon(_repository.EquippedIconId);
        }
    }
}
