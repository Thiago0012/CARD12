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
            BuildHeader("PERFIL", ShowMainMenu);

            Image identity = CreatePanel(
                _screenRoot,
                "Identidade do Duelista",
                new Vector2(0.055f, 0.12f),
                new Vector2(0.33f, 0.84f),
                new Color(0.008f, 0.025f, 0.05f, 0.98f));
            AddOutline(identity.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.75f),
                new Vector2(2f, -2f));
            CreateHexIcon(identity.transform, "Ícone Equipado", _repository.EquippedIconId,
                new Vector2(0.18f, 0.48f), new Vector2(0.82f, 0.91f));
            CreateText(identity.transform, _repository.PlayerDisplayName, 30,
                FontStyle.Bold, Color.white, new Vector2(0.06f, 0.36f),
                new Vector2(0.94f, 0.48f), TextAnchor.MiddleCenter);
            RankPresentationModel rank = _repository.GetRankPresentation();
            CreateText(identity.transform,
                $"{RankRules.DisplayName(rank.Tier)}  •  {rank.Points} PE",
                17, FontStyle.Bold, Gold, new Vector2(0.06f, 0.29f),
                new Vector2(0.94f, 0.37f), TextAnchor.MiddleCenter);
            CreateButton(identity.transform, "EDITAR NOME",
                new Vector2(0.16f, 0.09f), new Vector2(0.84f, 0.20f),
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
            DuelStatisticsScope online = _repository.Statistics?.online ??
                new DuelStatisticsScope();
            DuelStatisticsScope ranked = _repository.Statistics?.ranked ??
                new DuelStatisticsScope();
            CreateText(parent, "TOTAL", 17, FontStyle.Bold, Gold,
                new Vector2(0.035f, 0.73f), new Vector2(0.325f, 0.81f),
                TextAnchor.MiddleLeft);
            CreateText(parent, FormatStatistics(all), 14, FontStyle.Bold,
                Color.white, new Vector2(0.035f, 0.10f),
                new Vector2(0.325f, 0.73f), TextAnchor.UpperLeft);
            CreateText(parent, "ONLINE", 17, FontStyle.Bold, Cyan,
                new Vector2(0.355f, 0.73f), new Vector2(0.645f, 0.81f),
                TextAnchor.MiddleLeft);
            CreateText(parent, FormatStatistics(online), 14, FontStyle.Bold,
                Color.white, new Vector2(0.355f, 0.10f),
                new Vector2(0.645f, 0.73f), TextAnchor.UpperLeft);
            CreateText(parent, "RANQUEADO", 17, FontStyle.Bold, Lime,
                new Vector2(0.675f, 0.73f), new Vector2(0.965f, 0.81f),
                TextAnchor.MiddleLeft);
            CreateText(parent, FormatStatistics(ranked), 14, FontStyle.Bold,
                Color.white, new Vector2(0.675f, 0.10f),
                new Vector2(0.965f, 0.73f), TextAnchor.UpperLeft);
        }

        private static string FormatStatistics(DuelStatisticsScope scope)
        {
            return $"Duelos: {scope.duelsPlayed:N0}\n" +
                   $"Vitórias / Derrotas / Empates: {scope.wins:N0} / " +
                   $"{scope.losses:N0} / {scope.draws:N0}\n" +
                   $"Dano causado: {scope.damageDealt:N0}\n" +
                   $"Monstros invocados: {scope.monstersSummoned:N0}\n" +
                   $"Invocações-Especiais: {scope.specialSummons:N0}\n" +
                   $"Magias ativadas: {scope.spellsActivated:N0}\n" +
                   $"Armadilhas ativadas: {scope.trapsActivated:N0}\n" +
                   $"Destruídos por batalha: {scope.monstersDestroyedByBattle:N0}\n" +
                   $"Destruídos por efeito: {scope.monstersDestroyedByEffect:N0}\n" +
                   $"Magias destruídas: {scope.spellsDestroyed:N0}\n" +
                   $"Armadilhas destruídas: {scope.trapsDestroyed:N0}";
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
            CreateHexIcon(tile.transform, icon.DisplayName, icon.IconId,
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
            CreateHexIcon(tile.transform, icon.DisplayName, icon.IconId,
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
                typeof(CanvasRenderer), typeof(Image),
                typeof(AspectRatioFitter), typeof(HexIconView));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            AspectRatioFitter fitter = item.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
            HexIconView view = item.GetComponent<HexIconView>();
            view.SetIcon(iconId);
            return view;
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
