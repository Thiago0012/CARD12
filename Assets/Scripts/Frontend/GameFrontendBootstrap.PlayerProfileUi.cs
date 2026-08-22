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

        // Área segura interna do painel direito. A borda esquerda contém uma
        // coluna ornamental de runas; 6,5% preserva essa arte e mantém a mesma
        // distância óptica da moldura direita (5%).
        private const float ProfileContentMinX = 0.065f;
        private const float ProfileContentMaxX = 0.95f;
        // Limites medidos na arte ProfileArcaneFrame (1672 x 941). O painel
        // de dados ocupa 0.31..0.955 da tela e as três ranhuras, convertidas
        // para o espaço local dele, ocupam 0.067..0.965 com 0.014 de vão.
        private const float ProfileTabMinX = 0.067f;
        private const float ProfileTabMaxX = 0.965f;
        private const float ProfileTabGap = 0.014f;
        private const float ProfileCardGap = 0.018f;

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
            BuildArcaneProfileBackground();
            BuildArcaneProfileHeader(ShowMainMenu);

            Image identity = CreatePanel(
                _screenRoot,
                "Identidade do Duelista",
                new Vector2(0.060f, 0.06f),
                new Vector2(0.298f, 0.91f),
                Color.clear);
            RankPresentationModel rank = _repository.GetRankPresentation();
            CreateText(
                identity.transform,
                "IDENTIDADE DO DUELISTA",
                12,
                FontStyle.Bold,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.78f),
                new Vector2(0.08f, 0.915f),
                new Vector2(0.92f, 0.965f),
                TextAnchor.MiddleCenter);
            Text nickname = CreateText(
                identity.transform,
                _repository.PlayerDisplayName,
                31,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.07f, 0.82f),
                new Vector2(0.93f, 0.915f),
                TextAnchor.MiddleCenter);
            nickname.resizeTextMinSize = 18;
            nickname.horizontalOverflow = HorizontalWrapMode.Wrap;
            nickname.verticalOverflow = VerticalWrapMode.Truncate;

            Image rankRow = CreatePanel(
                identity.transform,
                "Elo do Duelista",
                new Vector2(0.075f, 0.54f),
                new Vector2(0.925f, 0.64f),
                new Color(0.002f, 0.014f, 0.026f, 0.30f));
            CreateRankBadgeImage(
                rankRow.transform,
                "Símbolo do Elo",
                rank.Tier,
                new Vector2(0.045f, 0.09f),
                new Vector2(0.27f, 0.91f),
                1f);
            CreateText(
                rankRow.transform,
                "CLASSIFICAÇÃO ATUAL",
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.29f, 0.53f),
                new Vector2(0.96f, 0.88f),
                TextAnchor.MiddleLeft);
            CreateText(
                rankRow.transform,
                $"{RankRules.DisplayName(rank.Tier)}  ·  {rank.Points} PE",
                17,
                FontStyle.Bold,
                Gold,
                new Vector2(0.29f, 0.12f),
                new Vector2(0.96f, 0.58f),
                TextAnchor.MiddleLeft);

            Image portraitStage = CreatePanel(
                identity.transform,
                "Emblema de Perfil",
                new Vector2(0.12f, 0.205f),
                new Vector2(0.88f, 0.525f),
                Color.clear);
            CreateBoundedHexIcon(
                portraitStage.transform,
                "Ícone Equipado",
                _repository.EquippedIconId,
                // A área reservada tem proporção suficiente para o
                // AspectRatioFitter inscrever um hexágono regular exatamente
                // dentro do hexágono autoral, sem encolhimento cumulativo.
                new Vector2(0.14f, 0.055f),
                new Vector2(0.86f, 0.985f));

            CreateProfileArtworkButton(
                identity.transform,
                "EDITAR NOME",
                // Retângulo autoral: x 132..467 e y 768..850 na imagem-base.
                new Vector2(0.080f, 0.044f),
                new Vector2(0.922f, 0.146f),
                () => ShowPlayerNameEditor(true),
                16);

            Image detail = CreatePanel(
                _screenRoot,
                "Dados do Perfil",
                new Vector2(0.31f, 0.06f),
                new Vector2(0.955f, 0.91f),
                Color.clear);
            CreateProfileTabButton(detail.transform, "VISÃO GERAL",
                ProfileTab.Overview, 0);
            CreateProfileTabButton(detail.transform, "ESTATÍSTICAS",
                ProfileTab.Statistics, 1);
            CreateProfileTabButton(detail.transform, "ÍCONES",
                ProfileTab.Icons, 2);

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
            int index)
        {
            bool active = _profileTab == tab;
            GetHorizontalCell(
                index,
                3,
                ProfileTabMinX,
                ProfileTabMaxX,
                ProfileTabGap,
                out float minX,
                out float maxX);
            CreateProfileArtworkButton(
                parent,
                label,
                new Vector2(minX, 0.848f),
                new Vector2(maxX, 0.928f),
                () =>
                {
                    _profileTab = tab;
                    ShowPlayerProfile();
                },
                16,
                active);
        }

        /// <summary>
        /// Área interativa para uma moldura já desenhada na arte-base. Não
        /// cria outro fundo ou contorno; assim, o botão nunca compete com a
        /// geometria autoral. A seleção é indicada apenas por texto e filete.
        /// </summary>
        private static Image CreateProfileArtworkButton(
            Transform parent,
            string label,
            Vector2 min,
            Vector2 max,
            Action action,
            int fontSize,
            bool selected = false)
        {
            Image hitArea = CreatePanel(
                parent,
                $"Ação da Moldura {label}",
                min,
                max,
                new Color(1f, 1f, 1f, 0.001f));
            AddButtonBehaviour(hitArea, action);
            CreateText(
                hitArea.transform,
                label,
                fontSize,
                FontStyle.Bold,
                selected
                    ? new Color(0.96f, 0.86f, 0.68f, 1f)
                    : Color.white,
                new Vector2(0.08f, 0.12f),
                new Vector2(0.92f, 0.88f),
                TextAnchor.MiddleCenter);
            if (selected)
            {
                CreatePanel(
                    hitArea.transform,
                    "Indicador da Aba Ativa",
                    new Vector2(0.26f, 0.025f),
                    new Vector2(0.74f, 0.055f),
                    new Color(ArcaneGold.r, ArcaneGold.g, ArcaneGold.b, 0.95f))
                    .raycastTarget = false;
            }
            return hitArea;
        }

        private static void GetHorizontalCell(
            int index,
            int count,
            float minX,
            float maxX,
            float gap,
            out float cellMin,
            out float cellMax)
        {
            int safeCount = Mathf.Max(1, count);
            int safeIndex = Mathf.Clamp(index, 0, safeCount - 1);
            float safeGap = Mathf.Max(0f, gap);
            float usable = Mathf.Max(
                0f,
                maxX - minX - safeGap * (safeCount - 1));
            float width = usable / safeCount;
            cellMin = minX + safeIndex * (width + safeGap);
            cellMax = cellMin + width;
        }

        private static Image CreateProfileSurface(
            Transform parent,
            string name,
            Vector2 min,
            Vector2 max,
            Color accent,
            Color surface,
            float outlineAlpha = 0.68f)
        {
            bool raised = outlineAlpha >= 0.50f;
            return CreateArcaneSurface(
                parent,
                name,
                min,
                max,
                accent,
                raised,
                Mathf.Lerp(0.72f, 0.96f, Mathf.Clamp01(outlineAlpha)));
        }

        private void BuildProfileOverview(
            Transform parent,
            RankPresentationModel rank)
        {
            DuelStatisticsScope stats = _repository.Statistics?.overall ??
                new DuelStatisticsScope();
            double winRate = stats.duelsPlayed > 0
                ? stats.wins * 100.0 / stats.duelsPlayed
                : 0.0;

            CreateText(
                parent,
                "VISÃO GERAL",
                25,
                FontStyle.Bold,
                Color.white,
                new Vector2(ProfileContentMinX + 0.008f, 0.742f),
                new Vector2(0.60f, 0.805f),
                TextAnchor.MiddleLeft);
            CreateText(
                parent,
                "PROGRESSÃO, DESEMPENHO E IDENTIDADE COMPETITIVA",
                12,
                FontStyle.Bold,
                Muted,
                new Vector2(0.43f, 0.744f),
                new Vector2(ProfileContentMaxX - 0.008f, 0.802f),
                TextAnchor.MiddleRight);

            Image rankHero = CreateProfileSurface(
                parent,
                "Progressão Competitiva",
                new Vector2(ProfileContentMinX, 0.45f),
                new Vector2(ProfileContentMaxX, 0.715f),
                Gold,
                new Color(0.008f, 0.035f, 0.064f, 0.98f),
                0.62f);
            CreateText(
                rankHero.transform,
                "PROGRESSÃO COMPETITIVA",
                13,
                FontStyle.Bold,
                Gold,
                new Vector2(0.035f, 0.79f),
                new Vector2(0.46f, 0.94f),
                TextAnchor.MiddleLeft);
            CreateRankBadgeImage(
                rankHero.transform,
                "Elo Atual",
                rank.Tier,
                new Vector2(0.03f, 0.13f),
                new Vector2(0.22f, 0.79f),
                1f);
            CreateText(
                rankHero.transform,
                RankRules.DisplayName(rank.Tier),
                29,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.23f, 0.49f),
                new Vector2(0.57f, 0.77f),
                TextAnchor.MiddleLeft);
            CreateText(
                rankHero.transform,
                rank.IsMaximum
                    ? "O ápice competitivo foi alcançado."
                    : $"{rank.PointsUntilNext} PE até " +
                      RankRules.DisplayName(rank.NextTier),
                15,
                FontStyle.Bold,
                Muted,
                new Vector2(0.23f, 0.33f),
                new Vector2(0.69f, 0.51f),
                TextAnchor.MiddleLeft);
            Image progress = CreatePanel(
                rankHero.transform,
                "Trilha da Progressão",
                new Vector2(0.23f, 0.20f),
                new Vector2(0.69f, 0.29f),
                new Color(0.002f, 0.012f, 0.024f, 1f));
            AddOutline(
                progress.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f),
                new Vector2(1f, -1f));
            CreatePanel(
                progress.transform,
                "PE Conquistado",
                Vector2.zero,
                new Vector2(Mathf.Clamp01(rank.Progress01), 1f),
                rank.IsMaximum ? Gold : Cyan).raycastTarget = false;
            CreateText(
                rankHero.transform,
                $"{rank.Points} PE",
                14,
                FontStyle.Bold,
                rank.IsMaximum ? Gold : Cyan,
                new Vector2(0.23f, 0.05f),
                new Vector2(0.45f, 0.18f),
                TextAnchor.MiddleLeft);

            Image nextRank = CreatePanel(
                rankHero.transform,
                "Próximo Marco",
                new Vector2(0.73f, 0.12f),
                new Vector2(0.96f, 0.88f),
                Color.clear);
            CreateText(
                nextRank.transform,
                rank.IsMaximum ? "PATAMAR ATUAL" : "PRÓXIMO PATAMAR",
                11,
                FontStyle.Bold,
                Muted,
                new Vector2(0.06f, 0.80f),
                new Vector2(0.94f, 0.96f),
                TextAnchor.MiddleCenter);
            CreateRankBadgeImage(
                nextRank.transform,
                "Emblema do Próximo Patamar",
                rank.IsMaximum ? rank.Tier : rank.NextTier,
                new Vector2(0.24f, 0.24f),
                new Vector2(0.76f, 0.78f),
                rank.IsMaximum ? 1f : 0.82f);
            CreateText(
                nextRank.transform,
                RankRules.DisplayName(
                    rank.IsMaximum ? rank.Tier : rank.NextTier),
                13,
                FontStyle.Bold,
                rank.IsMaximum ? Gold : Color.white,
                new Vector2(0.05f, 0.04f),
                new Vector2(0.95f, 0.22f),
                TextAnchor.MiddleCenter);

            string[] labels =
            {
                "DUELOS VÁLIDOS",
                "TAXA DE VITÓRIA",
                "VITÓRIAS",
                "DERROTAS"
            };
            string[] values =
            {
                stats.duelsPlayed.ToString("N0"),
                $"{winRate:0.#}%",
                stats.wins.ToString("N0"),
                stats.losses.ToString("N0")
            };
            string[] captions =
            {
                "PARTIDAS REGISTRADAS",
                stats.duelsPlayed > 0 ? "DESEMPENHO GERAL" : "SEM HISTÓRICO",
                "RESULTADOS POSITIVOS",
                $"{stats.draws:N0} EMPATE(S)"
            };
            Color[] accents =
            {
                ArcaneCyan,
                ArcaneGold,
                ArcaneCyan,
                new Color(0.72f, 0.43f, 0.36f, 1f)
            };
            for (int index = 0; index < labels.Length; index++)
            {
                GetHorizontalCell(
                    index,
                    labels.Length,
                    ProfileContentMinX,
                    ProfileContentMaxX,
                    ProfileCardGap,
                    out float cardMinX,
                    out float cardMaxX);
                CreateOverviewMetricCard(
                    parent,
                    labels[index],
                    values[index],
                    captions[index],
                    accents[index],
                    new Vector2(cardMinX, 0.245f),
                    new Vector2(cardMaxX, 0.425f));
            }

            Image record = CreateProfileSurface(
                parent,
                "Registro do Duelista",
                new Vector2(ProfileContentMinX, 0.05f),
                new Vector2(ProfileContentMaxX, 0.215f),
                ArcaneCyan,
                new Color(0.006f, 0.028f, 0.052f, 0.96f),
                0.40f);
            CreateText(
                record.transform,
                "REGISTRO DO DUELISTA",
                13,
                FontStyle.Bold,
                ArcaneCyan,
                new Vector2(0.035f, 0.56f),
                new Vector2(0.34f, 0.90f),
                TextAnchor.MiddleLeft);
            CreateText(
                record.transform,
                stats.duelsPlayed == 0
                    ? "Conclua duelos válidos para construir seu histórico competitivo."
                    : $"{stats.wins:N0} vitória(s) · {stats.losses:N0} derrota(s) · " +
                      $"{stats.draws:N0} empate(s)",
                15,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.12f),
                new Vector2(0.72f, 0.58f),
                TextAnchor.MiddleLeft);
            CreateText(
                record.transform,
                rank.ShieldActive ? "PROTEÇÃO DE ELO ATIVA" : "PERFIL SINCRONIZADO",
                12,
                FontStyle.Bold,
                rank.ShieldActive ? Cyan : Muted,
                new Vector2(0.72f, 0.12f),
                new Vector2(0.965f, 0.88f),
                TextAnchor.MiddleCenter);
        }

        private static void CreateOverviewMetricCard(
            Transform parent,
            string label,
            string value,
            string caption,
            Color accent,
            Vector2 min,
            Vector2 max)
        {
            Image card = CreateProfileSurface(
                parent,
                label,
                min,
                max,
                accent,
                new Color(0.006f, 0.031f, 0.055f, 0.98f),
                0.46f);
            CreateText(card.transform, label, 11, FontStyle.Bold, Muted,
                new Vector2(0.07f, 0.68f), new Vector2(0.93f, 0.92f),
                TextAnchor.MiddleLeft);
            CreateText(card.transform, value, 28, FontStyle.Bold, accent,
                new Vector2(0.07f, 0.28f), new Vector2(0.93f, 0.70f),
                TextAnchor.MiddleLeft);
            CreateText(card.transform, caption, 10, FontStyle.Bold,
                new Color(0.60f, 0.72f, 0.80f, 0.90f),
                new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.29f),
                TextAnchor.MiddleLeft);
        }

        private void BuildProfileStatistics(Transform parent)
        {
            DuelStatisticsScope all = _repository.Statistics?.overall ??
                new DuelStatisticsScope();
            const int summaryColumns = 4;
            const int detailColumns = 2;

            RectTransform content = CreateStatisticsScrollContent(parent);
            AddStatisticsHeading(content, all);

            GridLayoutGroup summary = CreateStatisticsGrid(
                content,
                "Resumo das Estatísticas",
                summaryColumns,
                new Vector2(1f, 92f),
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
                ArcaneGold);
            AddStatisticsSummaryCard(
                summary.transform,
                "DERROTAS",
                all.losses.ToString("N0"),
                new Color(0.72f, 0.43f, 0.36f, 1f));
            double winRate = all.duelsPlayed > 0
                ? all.wins * 100.0 / all.duelsPlayed
                : 0.0;
            AddStatisticsSummaryCard(
                summary.transform,
                "TAXA DE VITÓRIA",
                $"{winRate:0.#}%",
                ArcaneCyan);

            int detailRows = Mathf.CeilToInt(4f / detailColumns);
            GridLayoutGroup details = CreateStatisticsGrid(
                content,
                "Grupos de Estatísticas",
                detailColumns,
                new Vector2(1f, 250f),
                detailRows * 250f + Mathf.Max(0, detailRows - 1) * 20f + 18f);
            AddStatisticsMetricCard(
                details.transform,
                "COMBATE",
                ArcaneGold,
                "DANO CAUSADO", all.damageDealt.ToString("N0"),
                "DANO RECEBIDO", all.damageReceived.ToString("N0"),
                "RECORDE DE DANO", all.maxDamageDealtInSingleDuel.ToString("N0"),
                "MAIOR DANO SOFRIDO", all.maxDamageReceivedInSingleDuel.ToString("N0"));
            AddDuelProfileRadar(details.transform, all);
            AddStatisticsMetricCard(
                details.transform,
                "INVOCAÇÕES E BATALHA",
                ArcaneCyan,
                "MONSTROS INVOCADOS", all.monstersSummoned.ToString("N0"),
                "INVOCAÇÕES-ESPECIAIS", all.specialSummons.ToString("N0"),
                "DESTRUÍDOS EM BATALHA", all.monstersDestroyedByBattle.ToString("N0"),
                "DESTRUÍDOS POR EFEITO", all.monstersDestroyedByEffect.ToString("N0"));
            AddStatisticsMetricCard(
                details.transform,
                "MAGIAS E ARMADILHAS",
                ArcaneCyan,
                "MAGIAS ATIVADAS", all.spellsActivated.ToString("N0"),
                "ARMADILHAS ATIVADAS", all.trapsActivated.ToString("N0"),
                "MAGIAS DESTRUÍDAS", all.spellsDestroyed.ToString("N0"),
                "ARMADILHAS DESTRUÍDAS", all.trapsDestroyed.ToString("N0"));
        }

        private static RectTransform CreateStatisticsScrollContent(
            Transform parent)
        {
            Image viewport = CreateArcaneSurface(
                parent,
                "Estatísticas Gerais",
                new Vector2(ProfileContentMinX, 0.045f),
                new Vector2(ProfileContentMaxX, 0.815f),
                ArcaneCyan,
                false,
                0.58f);
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
                ArcaneCyan);
            CreatePanel(
                heading.transform,
                "Acento do Cabeçalho",
                new Vector2(0f, 0f),
                new Vector2(0.008f, 1f),
                Cyan).raycastTarget = false;
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
            ArcaneResponsiveGridFitter responsive =
                gridObject.AddComponent<ArcaneResponsiveGridFitter>();
            responsive.Configure(columns, cellSize.y, 24f);
            return grid;
        }

        private static Image CreateLayoutStatisticsPanel(
            Transform parent,
            string name,
            float height,
            Color color)
        {
            Image panel = CreateArcaneSurface(
                parent,
                name,
                Vector2.zero,
                Vector2.one,
                color,
                false,
                0.68f);
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
            Image card = CreateArcaneSurface(
                parent,
                label,
                Vector2.zero,
                Vector2.one,
                accent,
                true,
                0.76f);
            CreateText(card.transform, label, 14, FontStyle.Bold, Muted,
                new Vector2(0.09f, 0.58f), new Vector2(0.94f, 0.92f),
                TextAnchor.MiddleLeft);
            CreateText(card.transform, value, 28, FontStyle.Bold, accent,
                new Vector2(0.09f, 0.10f), new Vector2(0.94f, 0.62f),
                TextAnchor.MiddleLeft);
        }

        private static void AddStatisticsMetricCard(
            Transform parent,
            string title,
            Color accent,
            params string[] metricPairs)
        {
            Image card = CreateProfileSurface(
                parent,
                title,
                Vector2.zero,
                Vector2.one,
                accent,
                new Color(0.005f, 0.027f, 0.050f, 0.99f),
                0.55f);
            CreateText(card.transform, title, 19, FontStyle.Bold, accent,
                new Vector2(0.055f, 0.81f), new Vector2(0.945f, 0.95f),
                TextAnchor.MiddleLeft);
            int pairCount = Mathf.Min(4, metricPairs?.Length / 2 ?? 0);
            for (int index = 0; index < pairCount; index++)
            {
                float maxY = 0.79f - index * 0.18f;
                float minY = maxY - 0.155f;
                Image row = CreatePanel(
                    card.transform,
                    $"Métrica {index + 1}",
                    new Vector2(0.055f, minY),
                    new Vector2(0.945f, maxY),
                    index % 2 == 0
                        ? new Color(0.02f, 0.075f, 0.105f, 0.52f)
                        : new Color(0.01f, 0.045f, 0.075f, 0.48f));
                CreatePanel(
                    row.transform,
                    "Marcador",
                    new Vector2(0f, 0.17f),
                    new Vector2(0.012f, 0.83f),
                    new Color(accent.r, accent.g, accent.b, 0.82f))
                    .raycastTarget = false;
                CreateText(
                    row.transform,
                    metricPairs[index * 2],
                    12,
                    FontStyle.Bold,
                    Muted,
                    new Vector2(0.045f, 0.08f),
                    new Vector2(0.73f, 0.92f),
                    TextAnchor.MiddleLeft);
                CreateText(
                    row.transform,
                    metricPairs[index * 2 + 1],
                    17,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.73f, 0.08f),
                    new Vector2(0.96f, 0.92f),
                    TextAnchor.MiddleRight);
            }
        }

        private static void AddDuelProfileRadar(
            Transform parent,
            DuelStatisticsScope stats)
        {
            Image card = CreateProfileSurface(
                parent,
                "Perfil de Duelo",
                Vector2.zero,
                Vector2.one,
                Cyan,
                new Color(0.005f, 0.027f, 0.050f, 0.99f),
                0.58f);
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
            int ownedCount = 0;
            foreach (ProfileIconDefinition icon in ProfileIconCatalog.All)
            {
                if (_repository.OwnsIcon(icon.IconId))
                    ownedCount++;
            }

            Image heading = CreateProfileSurface(
                parent,
                "Coleção de Ícones",
                new Vector2(ProfileContentMinX, 0.685f),
                new Vector2(ProfileContentMaxX, 0.81f),
                ArcaneGold,
                new Color(0.006f, 0.034f, 0.060f, 0.98f),
                0.42f);
            CreateText(
                heading.transform,
                "COLEÇÃO DE ÍCONES",
                22,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.035f, 0.43f),
                new Vector2(0.60f, 0.88f),
                TextAnchor.MiddleLeft);
            CreateText(
                heading.transform,
                "Selecione um emblema para representar seu perfil e suas partidas.",
                13,
                FontStyle.Bold,
                Muted,
                new Vector2(0.035f, 0.08f),
                new Vector2(0.74f, 0.46f),
                TextAnchor.MiddleLeft);
            Image counter = CreateProfileSurface(
                heading.transform,
                "Contador da Coleção",
                new Vector2(0.78f, 0.19f),
                new Vector2(0.96f, 0.81f),
                ArcaneGold,
                new Color(0.022f, 0.030f, 0.038f, 0.88f),
                0.60f);
            CreateText(
                counter.transform,
                $"{ownedCount:N0}\nPOSSUÍDOS",
                15,
                FontStyle.Bold,
                ArcaneGold,
                new Vector2(0.05f, 0.08f),
                new Vector2(0.95f, 0.92f),
                TextAnchor.MiddleCenter);

            RectTransform grid = CreateProfileIconGrid(parent);
            foreach (ProfileIconDefinition icon in ProfileIconCatalog.All)
            {
                if (_repository.OwnsIcon(icon.IconId))
                    CreateOwnedIconTile(grid, icon);
            }
        }

        private static RectTransform CreateProfileIconGrid(Transform parent)
        {
            Image viewport = CreatePanel(
                parent,
                "Ícones Possuídos",
                new Vector2(ProfileContentMinX, 0.055f),
                new Vector2(ProfileContentMaxX, 0.66f),
                Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();

            GameObject contentObject = new(
                "Conteúdo dos Ícones Possuídos",
                typeof(RectTransform),
                typeof(GridLayoutGroup),
                typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(18f, 0f);
            content.offsetMax = new Vector2(-42f, 0f);

            GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(1f, 235f);
            grid.spacing = new Vector2(22f, 22f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;
            ArcaneResponsiveGridFitter responsive =
                contentObject.AddComponent<ArcaneResponsiveGridFitter>();
            responsive.Configure(3, 235f, 22f);

            ContentSizeFitter fitter =
                contentObject.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 68f;

            Image track = CreatePanel(
                viewport.transform,
                "Rolagem dos Ícones",
                new Vector2(0.975f, 0.025f),
                new Vector2(0.992f, 0.975f),
                new Color(0.02f, 0.07f, 0.10f, 0.82f));
            Image handle = CreatePanel(
                track.transform,
                "Alça",
                new Vector2(0.14f, 0f),
                new Vector2(0.86f, 0.34f),
                new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.88f));
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        private void CreateOwnedIconTile(
            Transform parent,
            ProfileIconDefinition icon)
        {
            bool equipped = string.Equals(_repository.EquippedIconId,
                icon.IconId, StringComparison.Ordinal);
            Color accent = equipped ? ArcaneGold : ArcaneCyan;
            Image tile = CreateProfileSurface(
                parent,
                icon.DisplayName,
                Vector2.zero,
                Vector2.one,
                accent,
                new Color(0.006f, 0.032f, 0.057f, 0.99f),
                equipped ? 0.92f : 0.52f);
            CreateBoundedHexIcon(tile.transform, icon.DisplayName, icon.IconId,
                new Vector2(0.23f, 0.34f), new Vector2(0.77f, 0.91f));
            CreateText(tile.transform, icon.DisplayName, 15, FontStyle.Bold,
                Color.white, new Vector2(0.05f, 0.16f),
                new Vector2(0.95f, 0.34f), TextAnchor.MiddleCenter);
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
            {
                Image equippedBadge = CreateProfileSurface(
                    tile.transform,
                    "Estado Equipado",
                    new Vector2(0.18f, 0.035f),
                    new Vector2(0.82f, 0.155f),
                    ArcaneGold,
                    new Color(0.07f, 0.055f, 0.035f, 0.92f),
                    0.82f);
                CreateText(
                    equippedBadge.transform,
                    "EQUIPADO",
                    11,
                    FontStyle.Bold,
                    ArcaneGold,
                    new Vector2(0.04f, 0.05f),
                    new Vector2(0.96f, 0.95f),
                    TextAnchor.MiddleCenter);
            }
            else
            {
                CreateText(
                    tile.transform,
                    "CLIQUE PARA EQUIPAR",
                    10,
                    FontStyle.Bold,
                    Muted,
                    new Vector2(0.05f, 0.035f),
                    new Vector2(0.95f, 0.15f),
                    TextAnchor.MiddleCenter);
            }
        }

        private void CreateProfileIconShopTile(
            Transform parent,
            ProfileIconDefinition icon)
        {
            bool owned = _repository.OwnsIcon(icon.IconId);
            bool equipped = string.Equals(_repository.EquippedIconId,
                icon.IconId, StringComparison.Ordinal);
            Color accent = equipped
                ? new Color(0.78f, 1f, 0.20f, 1f)
                : owned
                    ? new Color(0.34f, 0.88f, 0.96f, 1f)
                    : new Color(0.98f, 0.68f, 0.18f, 1f);
            Image tile = CreateShopTile(parent, icon.DisplayName, accent);

            CreateText(tile.transform,
                equipped ? "EMBLEMA ATIVO" : owned
                    ? "EMBLEMA ADQUIRIDO"
                    : "EMBLEMA PREMIUM",
                10, FontStyle.Bold,
                new Color(accent.r, accent.g, accent.b, 0.92f),
                new Vector2(0.055f, 0.885f),
                new Vector2(0.945f, 0.965f),
                TextAnchor.MiddleCenter);
            CreateText(tile.transform, icon.DisplayName, 18, FontStyle.Bold,
                Color.white, new Vector2(0.055f, 0.765f),
                new Vector2(0.945f, 0.885f), TextAnchor.MiddleCenter);

            Image pedestal = CreatePanel(tile.transform,
                "Pedestal do Emblema",
                new Vector2(0.13f, 0.225f),
                new Vector2(0.87f, 0.765f),
                new Color(0.002f, 0.006f, 0.012f, 0.72f));
            DecorateRuntimeShopSurface(pedestal, accent, false, 18f);
            CreatePanel(pedestal.transform,
                "Reflexo do Pedestal",
                new Vector2(0.18f, 0.06f),
                new Vector2(0.82f, 0.075f),
                new Color(accent.r, accent.g, accent.b, 0.62f))
                .raycastTarget = false;
            CreatePanel(pedestal.transform,
                "Luz Superior",
                new Vector2(0.35f, 0.93f),
                new Vector2(0.65f, 0.945f),
                new Color(accent.r, accent.g, accent.b, 0.86f))
                .raycastTarget = false;
            CreateBoundedHexIcon(pedestal.transform,
                "Emblema " + icon.DisplayName,
                icon.IconId,
                new Vector2(0.25f, 0.08f),
                new Vector2(0.75f, 0.92f));

            if (owned)
            {
                string action = equipped ? "EQUIPADO" : "EQUIPAR";
                Image actionButton = CreateButton(tile.transform, action,
                    new Vector2(0.08f, 0.04f),
                    new Vector2(0.92f, 0.19f), accent, () =>
                    HandleProfileIconShopAction(icon));
                DecorateRuntimeShopButton(
                    actionButton,
                    accent,
                    !equipped,
                    8f);
            }
            else
            {
                CreateShopPriceButton(tile.transform, "COMPRAR",
                    ProfileIconCatalog.IconPriceCoins,
                    new Vector2(0.08f, 0.04f),
                    new Vector2(0.92f, 0.19f), Gold, () =>
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
            BuildProfessionalShopHeader(icon.DisplayName, ShowEconomyShop);
            CreateCoinBalance(_screenRoot);
            Image panel = CreatePanel(_screenRoot, "Confirmação do Ícone",
                new Vector2(0.29f, 0.16f), new Vector2(0.71f, 0.80f),
                new Color(0.008f, 0.025f, 0.05f, 0.99f));
            DecorateRuntimeShopSurface(panel, Gold, true, 15f);
            CreateText(panel.transform, "EMBLEMA PREMIUM", 14,
                FontStyle.Bold, Gold,
                new Vector2(0.08f, 0.90f), new Vector2(0.92f, 0.97f),
                TextAnchor.MiddleCenter);
            Image pedestal = CreatePanel(panel.transform,
                "Pedestal de Confirmação",
                new Vector2(0.22f, 0.39f), new Vector2(0.78f, 0.88f),
                new Color(0.002f, 0.006f, 0.012f, 0.74f));
            DecorateRuntimeShopSurface(pedestal, Gold, false, 18f);
            CreateBoundedHexIcon(pedestal.transform,
                icon.DisplayName, icon.IconId,
                new Vector2(0.23f, 0.06f), new Vector2(0.77f, 0.94f));
            CreateText(panel.transform, icon.DisplayName, 27, FontStyle.Bold,
                Color.white, new Vector2(0.06f, 0.27f),
                new Vector2(0.94f, 0.39f), TextAnchor.MiddleCenter);
            CreateText(panel.transform,
                "O emblema ficará disponível permanentemente no seu perfil.",
                14, FontStyle.Bold, Muted,
                new Vector2(0.09f, 0.21f), new Vector2(0.91f, 0.28f),
                TextAnchor.MiddleCenter);
            CreateShopPriceButton(panel.transform, "COMPRAR POR",
                ProfileIconCatalog.IconPriceCoins,
                new Vector2(0.13f, 0.055f), new Vector2(0.87f, 0.195f), Gold,
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
