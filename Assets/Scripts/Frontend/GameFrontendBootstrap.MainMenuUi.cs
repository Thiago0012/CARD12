using System;
using System.Collections;
using ArcaneArena.Multiplayer;
using ArcaneDuel.Game;
using ArcaneDuel.Game.Accounts;
using ArcaneDuel.Game.Competitive;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private enum MultiplayerHubMode
        {
            Ranked,
            Casual,
            Tournaments
        }

        private const string MainMenuAssetsPath =
            "Frontend/MainMenuUiAssets";
        private const string MainMenuHudOverlayShaderPath =
            "Frontend/MainMenuHudOverlay";
        private const string MainMenuHudOverlayMaterialPath =
            "Frontend/MainMenuHudOverlayMaterial";
        private const string ConnectionProbeUrl =
            "https://services.api.unity.com";

        private Coroutine _connectionMonitor;
        private Coroutine _offlineDuelPreparation;
        private Image[] _connectionBars;
        private Text _connectionStatus;
        private Text _relayRegionStatus;
        private MainMenuUiAssets _mainMenuAssets;
        private Material _mainMenuHudOverlayMaterial;
        private bool _ownsMainMenuHudOverlayMaterial;
        private MainMenuSceneView _mainMenuSceneView;

        public void ShowMainMenu()
        {
            if (_deckEditorNewMarkersWereShown)
            {
                _repository?.ClearPendingDeckEditorNewCards();
                _deckEditorNewMarkersWereShown = false;
            }
            MainMenuMusicController.SetDeckEditorMode(false);
            _tournamentPage = TournamentPage.None;
            if (_repository != null && _repository.NeedsStarterDeckSelection)
            {
                ShowStarterDeckSelection();
                return;
            }

            SetDuelPresentation(false);
            ClearScreen();
            if (_mainMenuSceneView != null)
            {
                _mainMenuSceneView.Bind(this);
                _mainMenuSceneView.SetMainMenuVisible(true);
                RefreshAuthoredMainMenuArtwork();
                return;
            }
            BuildTemplateMainMenu();
        }

        private bool TryAttachAuthoredMainMenu()
        {
            if (!string.Equals(
                    UnityEngine.SceneManagement.SceneManager
                        .GetActiveScene().name,
                    MainMenuSceneName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            _mainMenuSceneView =
                FindAnyObjectByType<MainMenuSceneView>(
                    FindObjectsInactive.Include);
            if (_mainMenuSceneView == null ||
                !_mainMenuSceneView.IsConfigured)
            {
                _mainMenuSceneView = null;
                return false;
            }

            _shopSceneView = FindAnyObjectByType<ShopSceneView>(
                FindObjectsInactive.Include);
            if (_shopSceneView != null && !_shopSceneView.IsConfigured)
            {
                Debug.LogWarning(
                    "A Shop View da MainMenu está incompleta. " +
                    "A loja usará o layout de compatibilidade em runtime.");
                _shopSceneView = null;
            }

            _font = MasterDuelTypography.Resolve(
                FontStyle.Normal,
                17);
            _canvas = _mainMenuSceneView.SceneCanvas;
            _canvasRect = _canvas != null
                ? _canvas.GetComponent<RectTransform>()
                : null;
            if (_canvas != null)
                MasterDuelTypography.ApplyToHierarchy(_canvas.transform);
            _screenRoot = _mainMenuSceneView.DynamicRoot;
            if (_canvas == null || _screenRoot == null)
            {
                _mainMenuSceneView = null;
                _canvas = null;
                _canvasRect = null;
                _screenRoot = null;
                return false;
            }

            UniversalUiLayout.ConfigureCanvasScaler(
                _canvas.GetComponent<CanvasScaler>());
            ApplyAuthoredMainMenuHudOverlay();
            _mainMenuSceneView.Bind(this);
            EnsureEventSystem();
            return true;
        }

        public void MainMenuDuel()
        {
            FrontendClickAudio.Play();
            RunMainMenuFeatureTransition(
                ShowDuelHub,
                LoadingCardMotionStyle.DuelCharge,
                "ABRINDO CENTRAL DE DUELOS",
                "Preparando seus modos, deck ativo e progresso de elo.");
        }

        public void MainMenuDecks()
        {
            FrontendClickAudio.Play();
            OpenDeckEditorScene();
        }

        public void MainMenuShop()
        {
            FrontendClickAudio.Play();
            if (!PlayerIdAccessRuntime.Allows(
                    PlayerIdCapability.Economy,
                    out string rejection))
            {
                ShowPlayerIdCapabilityBlocked(rejection);
                return;
            }
            RunMainMenuFeatureTransition(
                ShowDeckShop,
                LoadingCardMotionStyle.ShopSpiral,
                "ABRINDO LOJA",
                "Organizando pacotes, decks e itens exclusivos.");
        }

        public void MainMenuMultiplayer()
        {
            FrontendClickAudio.Play();
            RunMainMenuFeatureTransition(
                ShowDuelHub,
                LoadingCardMotionStyle.MultiplayerCrossflow,
                "ABRINDO CENTRAL DE DUELOS",
                "Salas casuais e ranqueadas agora ficam no mesmo lugar.");
        }

        public void MainMenuSettings()
        {
            FrontendClickAudio.Play();
            ShowAnimationOptions();
        }

        public void MainMenuProfile()
        {
            FrontendClickAudio.Play();
            ShowPlayerProfileSetup(true);
        }

        public void MainMenuFriends()
        {
            FrontendClickAudio.Play();
            OpenPlayerSearchFromBell();
        }

        private void BuildTemplateMainMenu()
        {
            _mainMenuAssets ??=
                Resources.Load<MainMenuUiAssets>(MainMenuAssetsPath);
            if (_mainMenuAssets == null || !_mainMenuAssets.IsReady)
            {
                Debug.LogWarning(
                    "Assets da nova tela inicial não foram sincronizados.");
                ShowPanelMainMenuLegacy();
                return;
            }

            if (_mainMenuAssets.HasUnifiedDuelMenus)
            {
                BuildUnifiedMainMenu();
                return;
            }

            CreateTemplateButton(
                "DUELAR",
                _mainMenuAssets.duelButton,
                new Vector2(0.0718f, 0.4692f),
                new Vector2(0.2976f, 0.5654f),
                () => RunMainMenuFeatureTransition(
                    OpenBotDuelSelectionFromMainMenu,
                    LoadingCardMotionStyle.DuelCharge,
                    "PREPARANDO DUELO",
                    "As cartas avançam para o próximo confronto."));
            CreateTemplateButton(
                "MULTIPLAYER",
                _mainMenuAssets.multiplayerButton,
                new Vector2(0.0730f, 0.3624f),
                new Vector2(0.2988f, 0.4586f),
                () => RunMainMenuFeatureTransition(
                    ShowMultiplayerRoom,
                    LoadingCardMotionStyle.MultiplayerCrossflow,
                    "CONECTANDO MULTIPLAYER",
                    "Cruzando rotas para encontrar outros duelistas."));
            CreateTemplateButton(
                "DECKS",
                _mainMenuAssets.decksButton,
                new Vector2(0.0736f, 0.2582f),
                new Vector2(0.2994f, 0.3545f),
                OpenDeckEditorScene);
            CreateTemplateButton(
                "LOJA",
                _mainMenuAssets.shopButton,
                new Vector2(0.0733f, 0.1557f),
                new Vector2(0.2991f, 0.2519f),
                () => RunMainMenuFeatureTransition(
                    ShowDeckShop,
                    LoadingCardMotionStyle.ShopSpiral,
                    "ABRINDO LOJA",
                    "Organizando pacotes, decks e itens exclusivos."));
            CreateTemplateButton(
                "CONFIGURAÇÕES",
                _mainMenuAssets.settingsButton,
                new Vector2(0.9432f, 0.9213f),
                new Vector2(0.9743f, 0.9873f),
                ShowAnimationOptions);

            CreateInvisibleButton(
                "PERFIL",
                new Vector2(0.793f, 0.919f),
                new Vector2(0.827f, 0.988f),
                () => ShowPlayerProfileSetup(true));
            Button friendsButton = CreateInvisibleButton(
                "AMIGOS (SINO)",
                new Vector2(0.891f, 0.918f),
                new Vector2(0.927f, 0.995f),
                OpenPlayerSearchFromBell);
            DecorateMainMenuFriendsButton(friendsButton);

            // A moldura vem depois das artes: os botoes aparecem atraves
            // dos recortes transparentes do shader e nunca sobre a HUD.
            var hudOverlay = CreateFullCanvasArtwork(
                "Moldura HUD da Tela Inicial",
                _mainMenuAssets.hud);
            if (!TryApplyMainMenuHudOverlay(hudOverlay))
            {
                // Se o shader estiver ausente, preserva a navegacao e o
                // comportamento visual anterior em vez de ocultar botoes.
                hudOverlay.transform.SetAsFirstSibling();
            }

            BuildVersionOverlay();
        }

        private void BuildUnifiedMainMenu()
        {
            CreateFullCanvasArtwork(
                "Nova Tela Inicial",
                _mainMenuAssets.mainMenu);
            CreateRuntimeMainMenuArtwork();

            CreateInvisibleButton(
                "DUELAR",
                new Vector2(0.071f, 0.457f),
                new Vector2(0.301f, 0.555f),
                () => RunMainMenuFeatureTransition(
                    ShowDuelHub,
                    LoadingCardMotionStyle.DuelCharge,
                    "ABRINDO CENTRAL DE DUELOS",
                    "Preparando seus modos, deck ativo e progresso de elo."));
            CreateInvisibleButton(
                "DECKS",
                new Vector2(0.071f, 0.344f),
                new Vector2(0.301f, 0.447f),
                OpenDeckEditorScene);
            CreateInvisibleButton(
                "LOJA",
                new Vector2(0.071f, 0.232f),
                new Vector2(0.301f, 0.337f),
                () => RunMainMenuFeatureTransition(
                    ShowDeckShop,
                    LoadingCardMotionStyle.ShopSpiral,
                    "ABRINDO LOJA",
                    "Organizando pacotes, decks e itens exclusivos."));
            CreateInvisibleButton(
                "CONFIGURAÇÕES",
                new Vector2(0.942f, 0.923f),
                new Vector2(0.977f, 0.990f),
                ShowAnimationOptions);
            CreateInvisibleButton(
                "PERFIL",
                new Vector2(0.792f, 0.927f),
                new Vector2(0.826f, 0.990f),
                () => ShowPlayerProfileSetup(true));
            Button friendsButton = CreateInvisibleButton(
                "AMIGOS (SINO)",
                new Vector2(0.891f, 0.918f),
                new Vector2(0.927f, 0.995f),
                OpenPlayerSearchFromBell);
            DecorateMainMenuFriendsButton(friendsButton);
        }

        public void ShowDuelHub()
        {
            SetDuelPresentation(false);
            ClearScreen();
            _mainMenuAssets ??=
                Resources.Load<MainMenuUiAssets>(MainMenuAssetsPath);
            if (_mainMenuAssets == null ||
                _mainMenuAssets.duelHub == null)
            {
                ShowDuelRoom();
                return;
            }

            CreateFullCanvasArtwork(
                "Nova Central de Duelos",
                _mainMenuAssets.duelHub);

            CreateInvisibleButton(
                "VOLTAR DA CENTRAL DE DUELOS",
                new Vector2(0.008f, 0.911f),
                new Vector2(0.058f, 0.985f),
                () => RunMainMenuTransition(ShowMainMenu));
            CreateInvisibleButton(
                "DUELAR OFFLINE",
                new Vector2(0.020f, 0.679f),
                new Vector2(0.315f, 0.866f),
                StartOfflineRandomDuelFromDuelHub);
            CreateInvisibleButton(
                "PROCURAR RIVAL RANQUEADO",
                new Vector2(0.020f, 0.477f),
                new Vector2(0.315f, 0.663f),
                StartRankedMatchmakingFromDuelHub);
            CreateInvisibleButton(
                "DUELO MULTIPLAYER CASUAL",
                new Vector2(0.020f, 0.281f),
                new Vector2(0.315f, 0.466f),
                () => OpenMultiplayerPanel(
                    true,
                    CompetitivePolicy.Unranked));
            CreateInvisibleButton(
                "DUELO MULTIPLAYER RANQUEADO",
                new Vector2(0.020f, 0.080f),
                new Vector2(0.315f, 0.265f),
                () => OpenMultiplayerPanel(
                    true,
                    CompetitivePolicy.Ranked));
            CreateInvisibleButton(
                "ALTERAR DECK ATIVO",
                new Vector2(0.425f, 0.392f),
                new Vector2(0.570f, 0.463f),
                OpenDeckEditorScene);

            BuildDuelHubDeckPresentation();
            BuildDuelHubRankPresentation();
            BuildDuelHubStoryEntry();
        }

        private void BuildDuelHubStoryEntry()
        {
            // Crônicas é um modo especial da Central, não parte da progressão
            // de elo. A faixa superior direita mantém o acesso próximo do
            // título, equilibra o botão de voltar e não encobre deck, patente
            // ou nenhum dos quatro modos de duelo da arte original.
            Image storyButton = CreateArcaneSurface(
                _screenRoot,
                "Acesso às Crônicas do Duelo",
                new Vector2(0.742f, 0.907f),
                new Vector2(0.974f, 0.985f),
                ArcaneGold,
                true,
                0.84f);
            AddButtonBehaviour(storyButton, ShowStoryRoguelite);
            Button behaviour = storyButton.GetComponent<Button>();
            ArcanePanelSheenGraphic sheen =
                storyButton.GetComponentInChildren<ArcanePanelSheenGraphic>();
            if (behaviour != null && sheen != null)
                behaviour.targetGraphic = sheen;

            Image seal = CreateArcaneSurface(
                storyButton.transform,
                "Selo das Crônicas",
                new Vector2(0.025f, 0.16f),
                new Vector2(0.145f, 0.84f),
                ArcaneCyan,
                true,
                0.80f);
            seal.raycastTarget = false;
            CreateText(
                seal.transform,
                "✦",
                23,
                FontStyle.Bold,
                new Color(0.96f, 0.80f, 0.46f, 1f),
                new Vector2(0.08f, 0.08f),
                new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter)
                .raycastTarget = false;

            CreateText(
                storyButton.transform,
                "CRÔNICAS DO DUELO",
                17,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.175f, 0.43f),
                new Vector2(0.82f, 0.88f),
                TextAnchor.MiddleLeft)
                .raycastTarget = false;
            CreateText(
                storyButton.transform,
                "BETA  •  EM DESENVOLVIMENTO",
                10,
                FontStyle.Bold,
                new Color(0.48f, 0.90f, 0.96f, 0.96f),
                new Vector2(0.175f, 0.12f),
                new Vector2(0.82f, 0.47f),
                TextAnchor.MiddleLeft)
                .raycastTarget = false;
            CreateText(
                storyButton.transform,
                "›",
                29,
                FontStyle.Bold,
                new Color(0.92f, 0.72f, 0.36f, 1f),
                new Vector2(0.84f, 0.12f),
                new Vector2(0.96f, 0.88f),
                TextAnchor.MiddleCenter)
                .raycastTarget = false;
            CreatePanel(
                storyButton.transform,
                "Energia ciano das Crônicas",
                new Vector2(0.18f, 0.045f),
                new Vector2(0.78f, 0.075f),
                new Color(ArcaneCyan.r, ArcaneCyan.g, ArcaneCyan.b, 0.82f))
                .raycastTarget = false;
        }

        private void StartRankedMatchmakingFromDuelHub()
        {
            if (!CanStartWithSelectedDeck())
                return;
            ArcaneArenaMultiplayerController.StartRankedMatchmaking();
        }

        public void StartRankedBotFallbackFromMatchmaking()
        {
            ShowRankedBotDeckSelection();
        }

        private void BuildDuelHubDeckPresentation()
        {
            DeckRecord selectedDeck = _repository?.SelectedDeck;
            if (selectedDeck != null)
            {
                CreateDuelDeckPreview(
                    _screenRoot,
                    selectedDeck,
                    new Vector2(0.384f, 0.548f),
                    new Vector2(0.616f, 0.778f));
            }

            string deckLabel = selectedDeck != null
                ? selectedDeck.displayName?.ToUpperInvariant()
                : "SELECIONE UM DECK VÁLIDO";
            _duelRoomStatus = CreateText(
                _screenRoot,
                deckLabel,
                19,
                FontStyle.Bold,
                selectedDeck != null ? Color.white : Gold,
                new Vector2(0.382f, 0.476f),
                new Vector2(0.618f, 0.535f),
                TextAnchor.MiddleCenter);
        }

        private void BuildDuelHubRankPresentation()
        {
            RankPresentationModel rank = GetRankPresentation();
            Image currentRankBadge = CreateRankBadgeImage(
                _screenRoot,
                "Patente atual centralizada",
                rank.Tier,
                new Vector2(0.727f, 0.515f),
                new Vector2(0.905f, 0.795f),
                1f);
            ApplyCapturedRectTransform(
                currentRankBadge.rectTransform,
                new Vector2(0.727f, 0.515f),
                new Vector2(0.905f, 0.795f),
                -9.480713f,
                5.600006f,
                9.480713f,
                -5.600006f);
            CreateText(
                _screenRoot,
                RankRules.DisplayName(rank.Tier),
                24,
                FontStyle.Bold,
                Gold,
                new Vector2(0.704f, 0.474f),
                new Vector2(0.911f, 0.535f),
                TextAnchor.MiddleCenter);
            Text currentRankPoints = CreateText(
                _screenRoot,
                rank.IsMaximum
                    ? "200 PE · ELO MÁXIMO"
                    : $"{rank.Points} PE",
                26,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.726f, 0.371f),
                new Vector2(0.900f, 0.455f),
                TextAnchor.MiddleCenter);
            currentRankPoints.text = rank.Points.ToString();
            ApplyCapturedRectTransform(
                currentRankPoints.rectTransform,
                new Vector2(0.726f, 0.371f),
                new Vector2(0.900f, 0.455f),
                0f,
                9f,
                0f,
                -9f);

            var progressObject = new GameObject(
                "Barra de progresso de elo",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(ArcaneRankProgressGraphic));
            progressObject.transform.SetParent(_screenRoot, false);
            RectTransform progressRect =
                progressObject.GetComponent<RectTransform>();
            ApplyCapturedRectTransform(
                progressRect,
                new Vector2(0.373f, 0.224f),
                new Vector2(0.802f, 0.268f),
                26.8411f,
                -4.944245f,
                28.2537f,
                4.237945f);
            ArcaneRankProgressGraphic progressGraphic =
                progressObject.GetComponent<ArcaneRankProgressGraphic>();
            progressGraphic.SetProgress(
                rank.Progress01,
                ArcaneCyan,
                ArcaneGold);
            progressGraphic.raycastTarget = false;

            CreateText(
                _screenRoot,
                rank.Points.ToString(),
                23,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.379f, 0.095f),
                new Vector2(0.498f, 0.163f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                rank.IsMaximum ? "0" : rank.PointsUntilNext.ToString(),
                23,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.659f, 0.095f),
                new Vector2(0.784f, 0.163f),
                TextAnchor.MiddleCenter);

            if (rank.IsMaximum)
            {
                CreateText(
                    _screenRoot,
                    "MAX",
                    26,
                    FontStyle.Bold,
                    Gold,
                    new Vector2(0.835f, 0.105f),
                    new Vector2(0.952f, 0.265f),
                    TextAnchor.MiddleCenter);
            }
            else
            {
                Image nextRankBadge = CreateRankBadgeImage(
                    _screenRoot,
                    "Próximo elo",
                    rank.NextTier,
                    new Vector2(0.835f, 0.095f),
                    new Vector2(0.952f, 0.278f),
                    0.95f);
                ApplyCapturedRectTransform(
                    nextRankBadge.rectTransform,
                    new Vector2(0.835f, 0.095f),
                    new Vector2(0.952f, 0.278f),
                    -9.5f,
                    0f,
                    9.5f,
                    0f);
            }
        }

        private static void RunMainMenuTransition(Action action)
        {
            if (action == null)
                return;
            DuelOnlineSession session = DuelOnlineSession.EnsureInstance();
            OnlineLoadingScreenPresenter presenter =
                session != null ? session.TransitionPresenter : null;
            if (presenter == null)
            {
                action.Invoke();
                return;
            }
            presenter.FadeThroughBlack(action);
        }

        private static void RunMainMenuFeatureTransition(
            Action action,
            LoadingCardMotionStyle style,
            string primary,
            string secondary)
        {
            if (action == null)
                return;
            DuelOnlineSession session = DuelOnlineSession.EnsureInstance();
            OnlineLoadingScreenPresenter presenter =
                session != null ? session.TransitionPresenter : null;
            if (presenter == null)
            {
                action.Invoke();
                return;
            }

            presenter.ShowFeatureTransition(
                primary,
                secondary,
                style,
                action);
        }

        private void OpenBotDuelSelectionFromMainMenu()
        {
            if (CanStartWithSelectedDeck())
            {
                ShowCasualBotDeckSelection();
                return;
            }

            ShowDeckGallery();
        }

        private void StartOfflineRandomDuelFromDuelHub()
        {
            if (!CanStartWithSelectedDeck())
            {
                ShowDeckGallery();
                return;
            }

            if (_offlineDuelPreparation != null)
                return;

            _offlineDuelPreparation = StartCoroutine(
                PrepareOfflineRandomDuel());
        }

        private IEnumerator PrepareOfflineRandomDuel()
        {
            OnlineLoadingScreenPresenter presenter = DuelOnlineSession
                .EnsureInstance()
                .TransitionPresenter;

            // O modo Offline monta o confronto automaticamente. A transição
            // informa apenas que a partida está sendo preparada, sem simular
            // uma busca por outro jogador nem revelar detalhes internos do
            // adversário antes do prelúdio. Ela não usa ShowFeatureTransition:
            // esse recurso encerra o mesmo painel que o pedra-papel-tesoura
            // precisa assumir no frame seguinte.
            presenter?.ShowDuelLoading(
                "PREPARANDO PARTIDA",
                "Definindo o próximo confronto.",
                0.12f);

            const float preparationSeconds = 0.62f;
            float startedAt = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startedAt < preparationSeconds)
            {
                float progress = Mathf.InverseLerp(
                    0f,
                    preparationSeconds,
                    Time.realtimeSinceStartup - startedAt);
                presenter?.SetProgress(Mathf.Lerp(0.12f, 0.88f, progress));
                yield return null;
            }

            _offlineDuelPreparation = null;
            StartRandomBotDuel();
        }

        private void ShowMultiplayerRoom()
        {
            RenderMultiplayerRoom(MultiplayerHubMode.Ranked);
        }

        private void RenderMultiplayerRoom(MultiplayerHubMode selectedMode)
        {
            SetDuelPresentation(false);
            ClearScreen();

            _mainMenuAssets ??=
                Resources.Load<MainMenuUiAssets>(MainMenuAssetsPath);
            if (_mainMenuAssets == null ||
                !_mainMenuAssets.HasMultiplayerLobby)
            {
                ShowLegacyMultiplayerRoom();
                return;
            }

            CreateFullCanvasArtwork(
                "Lobby Multiplayer",
                _mainMenuAssets.multiplayerLobby);

            CreateMultiplayerLobbyButton(
                "RANQUEADA",
                _mainMenuAssets.rankedModeButton,
                new Vector2(0.0084f, 0.6532f),
                new Vector2(0.2195f, 0.8138f),
                () => RenderMultiplayerRoom(MultiplayerHubMode.Ranked));
            CreateMultiplayerLobbyButton(
                "CASUAL",
                _mainMenuAssets.casualModeButton,
                new Vector2(0.0078f, 0.4606f),
                new Vector2(0.2189f, 0.6298f),
                () => RenderMultiplayerRoom(MultiplayerHubMode.Casual));
            CreateMultiplayerLobbyButton(
                "TORNEIOS",
                _mainMenuAssets.tournamentModeButton,
                new Vector2(0.0078f, 0.2766f),
                new Vector2(0.2183f, 0.4457f),
                () => RenderMultiplayerRoom(MultiplayerHubMode.Tournaments));

            CreateText(
                _screenRoot,
                "‹",
                44,
                FontStyle.Bold,
                Gold,
                new Vector2(0.022f, 0.892f),
                new Vector2(0.064f, 0.982f),
                TextAnchor.MiddleCenter);
            CreateText(
                _screenRoot,
                "MULTIPLAYER",
                30,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.067f, 0.898f),
                new Vector2(0.292f, 0.976f),
                TextAnchor.MiddleLeft);
            CreateInvisibleButton(
                "VOLTAR DO MULTIPLAYER",
                new Vector2(0.014f, 0.882f),
                new Vector2(0.305f, 0.988f),
                () => RunMainMenuTransition(ShowMainMenu));

            _duelRoomStatus = CreateText(
                _screenRoot,
                string.Empty,
                20,
                FontStyle.Bold,
                Muted,
                new Vector2(0.035f, 0.018f),
                new Vector2(0.965f, 0.104f),
                TextAnchor.MiddleCenter);
            BuildMultiplayerModeOverview(selectedMode);
        }

        private void ShowLegacyMultiplayerRoom()
        {
            BuildSharedBackground("MULTIPLAYER");
            BuildHeader(
                "MULTIPLAYER",
                () => RunMainMenuTransition(ShowMainMenu));

            var panel = CreatePanel(
                _screenRoot,
                "Sala Multiplayer",
                new Vector2(0.20f, 0.22f),
                new Vector2(0.80f, 0.80f),
                new Color(0.015f, 0.04f, 0.075f, 0.97f));
            AddOutline(
                panel.gameObject,
                new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f),
                new Vector2(3f, -3f));

            CreateText(
                panel.transform,
                "DUELO ONLINE",
                40,
                FontStyle.Bold,
                Color.white,
                new Vector2(0.08f, 0.74f),
                new Vector2(0.92f, 0.93f),
                TextAnchor.MiddleCenter);
            _duelRoomStatus = CreateText(
                panel.transform,
                "Crie uma sala privada ou entre usando o código do anfitrião.",
                21,
                FontStyle.Normal,
                Muted,
                new Vector2(0.10f, 0.56f),
                new Vector2(0.90f, 0.73f),
                TextAnchor.MiddleCenter);

            CreateButton(
                panel.transform,
                "CRIAR SALA PRIVADA",
                new Vector2(0.14f, 0.39f),
                new Vector2(0.86f, 0.51f),
                Cyan,
                () => OpenMultiplayerPanel(false));
            CreateButton(
                panel.transform,
                "ENTRAR COM CÓDIGO",
                new Vector2(0.14f, 0.23f),
                new Vector2(0.86f, 0.35f),
                Blue,
                () => OpenMultiplayerPanel(true));
            CreateButton(
                panel.transform,
                "TORNEIOS ONLINE",
                new Vector2(0.14f, 0.07f),
                new Vector2(0.86f, 0.19f),
                Gold,
                ShowTournamentHub);
        }

        private void BuildMultiplayerModeOverview(MultiplayerHubMode mode)
        {
            Image center = CreatePanel(
                _screenRoot,
                "Conteúdo do modo multiplayer",
                // A moldura central da arte fica à esquerda do centro
                // geométrico da tela. Estes limites mantêm o conteúdo no
                // centro visual do círculo, em vez de no centro do Canvas.
                new Vector2(0.241f, 0.185f),
                new Vector2(0.703f, 0.842f),
                Color.clear);
            center.raycastTarget = false;
            Image side = CreatePanel(
                _screenRoot,
                "Ações do modo multiplayer",
                new Vector2(0.738f, 0.185f),
                new Vector2(0.952f, 0.842f),
                Color.clear);
            side.raycastTarget = false;

            switch (mode)
            {
                case MultiplayerHubMode.Ranked:
                    BuildRankedModeOverview(center.transform, side.transform);
                    break;
                case MultiplayerHubMode.Casual:
                    BuildCasualModeOverview(center.transform, side.transform);
                    break;
                default:
                    BuildTournamentModeOverview(center.transform, side.transform);
                    break;
            }
        }

        private void BuildRankedModeOverview(Transform center, Transform side)
        {
            RankPresentationModel rank = GetRankPresentation();
            CreateText(center, "RANQUEADA", 30, FontStyle.Bold, Gold,
                new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.98f),
                TextAnchor.MiddleCenter);
            CreateText(center, "SEU ELO ATUAL", 15, FontStyle.Bold, Muted,
                new Vector2(0.323f, 0.79f), new Vector2(0.723f, 0.87f),
                TextAnchor.MiddleCenter);

            CreateRankBadgeImage(
                center,
                "Elo atual grande",
                rank.Tier,
                new Vector2(0.323f, 0.34f),
                new Vector2(0.723f, 0.80f),
                1f);
            CreateRankBadgeImage(
                center,
                "Elo atual miniatura",
                rank.Tier,
                new Vector2(0.04f, 0.42f),
                new Vector2(0.27f, 0.68f),
                0.80f);
            CreateText(center, RankRules.DisplayName(rank.Tier), 17,
                FontStyle.Bold, Color.white, new Vector2(0.03f, 0.34f),
                new Vector2(0.28f, 0.42f), TextAnchor.MiddleCenter);

            if (rank.Tier < RankTier.GrandMaster)
            {
                CreateRankBadgeImage(
                    center,
                    "Próximo elo miniatura",
                    rank.NextTier,
                    new Vector2(0.73f, 0.42f),
                    new Vector2(0.96f, 0.68f),
                    0.80f);
                CreateText(center, RankRules.DisplayName(rank.NextTier), 17,
                    FontStyle.Bold, Color.white, new Vector2(0.72f, 0.34f),
                    new Vector2(0.97f, 0.42f), TextAnchor.MiddleCenter);
            }
            else
            {
                CreateText(center, "MAX", 25, FontStyle.Bold, Gold,
                    new Vector2(0.72f, 0.43f),
                    new Vector2(0.97f, 0.62f), TextAnchor.MiddleCenter);
            }

            CreateText(center, RankRules.DisplayName(rank.Tier), 27,
                FontStyle.Bold, Gold, new Vector2(0.273f, 0.27f),
                new Vector2(0.773f, 0.35f), TextAnchor.MiddleCenter);
            Image bar = CreatePanel(center, "Barra de PE",
                new Vector2(0.16f, 0.19f), new Vector2(0.84f, 0.245f),
                new Color(0.015f, 0.02f, 0.03f, 1f));
            Image fill = CreatePanel(bar.transform, "Progresso de PE",
                Vector2.zero,
                new Vector2(Mathf.Clamp01(rank.Progress01), 1f),
                new Color(0.10f, 0.80f, 1f, 1f));
            fill.raycastTarget = false;
            string progress = rank.IsMaximum
                ? "MAX · 200 PE"
                : $"{rank.Points} PE · {rank.PointsUntilNext} PARA " +
                  RankRules.DisplayName(rank.NextTier);
            CreateText(center, progress, 16, FontStyle.Bold, Color.white,
                new Vector2(0.12f, 0.10f), new Vector2(0.88f, 0.18f),
                TextAnchor.MiddleCenter);
            string rewardLabel = rank.IsMaximum
                ? "RECOMPENSA DO ELO"
                : "PRÓXIMA PROMOÇÃO";
            int rewardCoins = RankPromotionRewards.CoinsFor(
                rank.IsMaximum ? rank.Tier : rank.NextTier);
            Image rewardRow = CreatePanel(center, "Recompensa em Moedas",
                new Vector2(0.20f, 0.035f), new Vector2(0.80f, 0.10f),
                Color.clear);
            CreateText(rewardRow.transform, rewardLabel, 14, FontStyle.Bold,
                Gold, new Vector2(0.01f, 0f), new Vector2(0.63f, 1f),
                TextAnchor.MiddleRight);
            CreateShopCurrencyIcon(rewardRow.transform, "Moeda da Recompensa",
                new Vector2(0.65f, 0.12f), new Vector2(0.76f, 0.88f));
            CreateText(rewardRow.transform, rewardCoins.ToString("N0"), 15,
                FontStyle.Bold, Gold, new Vector2(0.78f, 0f),
                new Vector2(0.99f, 1f), TextAnchor.MiddleLeft);
            if (rank.ShieldActive)
            {
                CreateText(center, "PROTEÇÃO CONTRA QUEDA ATIVA", 14,
                    FontStyle.Bold, new Color(0.30f, 0.90f, 1f, 1f),
                    new Vector2(0.18f, 0.00f), new Vector2(0.82f, 0.04f),
                    TextAnchor.MiddleCenter);
            }

            CreateText(side, "DUELO RANQUEADO", 24, FontStyle.Bold, Gold,
                new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.94f),
                TextAnchor.MiddleCenter);
            CreateText(side,
                "Vitórias e derrotas alteram seus PE. O resultado é validado " +
                "pelo host e salvo uma única vez.",
                17, FontStyle.Normal, Muted, new Vector2(0.10f, 0.57f),
                new Vector2(0.90f, 0.80f), TextAnchor.UpperCenter);
            CreateButton(side, "CRIAR SALA RANQUEADA",
                new Vector2(0.09f, 0.43f), new Vector2(0.91f, 0.55f), Gold,
                () => OpenMultiplayerPanel(false, CompetitivePolicy.Ranked));
            CreateButton(side, "RIVAL ALEATÓRIO",
                new Vector2(0.09f, 0.28f), new Vector2(0.91f, 0.40f), Cyan,
                ArcaneArenaMultiplayerController.StartRankedMatchmaking);
            CreateButton(side, "BUSCAR RIVAL IA",
                new Vector2(0.09f, 0.13f), new Vector2(0.91f, 0.25f), Blue,
                ShowRankedBotDeckSelection);
            CreateText(side,
                "O rival é escolhido automaticamente por proximidade de PE.",
                13, FontStyle.Normal, Muted, new Vector2(0.10f, 0.02f),
                new Vector2(0.90f, 0.11f), TextAnchor.MiddleCenter);
        }

        private void BuildCasualModeOverview(Transform center, Transform side)
        {
            CreateText(center, "DUELO CASUAL", 34, FontStyle.Bold, Cyan,
                new Vector2(0.08f, 0.75f), new Vector2(0.92f, 0.91f),
                TextAnchor.MiddleCenter);
            CreateText(center, "SEM PRESSÃO · SEM ALTERAÇÃO DE ELO", 21,
                FontStyle.Bold, Color.white, new Vector2(0.10f, 0.60f),
                new Vector2(0.90f, 0.73f), TextAnchor.MiddleCenter);
            CreateText(center,
                "Use seus decks desbloqueados, convide um amigo por código " +
                "e jogue com todas as regras do campo online sem ganhar ou perder PE.",
                19, FontStyle.Normal, Muted, new Vector2(0.13f, 0.30f),
                new Vector2(0.87f, 0.56f), TextAnchor.MiddleCenter);
            CreateText(center, "CASUAL", 58, FontStyle.Bold,
                new Color(0.12f, 0.80f, 1f, 0.30f),
                new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.28f),
                TextAnchor.MiddleCenter);

            CreateText(side, "SALA PRIVADA", 24, FontStyle.Bold, Cyan,
                new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateButton(side, "CRIAR SALA CASUAL",
                new Vector2(0.09f, 0.49f), new Vector2(0.91f, 0.64f), Cyan,
                () => OpenMultiplayerPanel(false, CompetitivePolicy.Unranked));
            CreateButton(side, "ENTRAR COM CÓDIGO",
                new Vector2(0.09f, 0.30f), new Vector2(0.91f, 0.45f), Blue,
                () => OpenMultiplayerPanel(true, CompetitivePolicy.Unranked));
            CreateText(side,
                "O modo casual mantém o mesmo Relay e a mesma sincronização " +
                "do duelo ranqueado.",
                15, FontStyle.Normal, Muted, new Vector2(0.10f, 0.09f),
                new Vector2(0.90f, 0.25f), TextAnchor.MiddleCenter);
        }

        private void BuildTournamentModeOverview(Transform center, Transform side)
        {
            CreateText(center, "TORNEIOS ONLINE", 34, FontStyle.Bold, Gold,
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateText(center,
                "Crie chaves mata-mata ou por pontos, defina regras, decks, " +
                "maioria mínima e escolha se o torneio concede PE ranqueado.",
                20, FontStyle.Normal, Color.white,
                new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.70f),
                TextAnchor.MiddleCenter);
            CreateText(center,
                "A política de elo fica bloqueada quando a chave começa.",
                17, FontStyle.Bold, Cyan, new Vector2(0.13f, 0.25f),
                new Vector2(0.87f, 0.38f), TextAnchor.MiddleCenter);
            CreateText(center, "CAMPEONATO", 46, FontStyle.Bold,
                new Color(0.90f, 0.62f, 0.16f, 0.25f),
                new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.24f),
                TextAnchor.MiddleCenter);

            CreateText(side, "CENTRAL DE TORNEIOS", 22, FontStyle.Bold, Gold,
                new Vector2(0.07f, 0.77f), new Vector2(0.93f, 0.92f),
                TextAnchor.MiddleCenter);
            CreateButton(side, "ABRIR TORNEIOS",
                new Vector2(0.09f, 0.45f), new Vector2(0.91f, 0.62f), Gold,
                ShowTournamentHub);
            CreateText(side,
                "Criação, entrada, lobby, classificação, métricas e chave em " +
                "um único fluxo.",
                16, FontStyle.Normal, Muted, new Vector2(0.10f, 0.18f),
                new Vector2(0.90f, 0.40f), TextAnchor.MiddleCenter);
        }

        private static Image CreateRankBadgeImage(
            Transform parent,
            string name,
            RankTier tier,
            Vector2 min,
            Vector2 max,
            float alpha)
        {
            GameObject item = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = item.GetComponent<Image>();
            image.sprite = RankBadgeCatalog.Get(tier);
            image.preserveAspect = true;
            image.color = tier == RankTier.Bronze
                ? new Color(0.72f, 0.34f, 0.12f, Mathf.Clamp01(alpha))
                : new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            image.raycastTarget = false;
            return image;
        }

        private void CreateMultiplayerLobbyButton(
            string label,
            Texture texture,
            Vector2 min,
            Vector2 max,
            System.Action action)
        {
            CreateFullCanvasArtwork(
                $"Arte Botão {label}",
                texture);

            var hitArea = CreatePanel(
                _screenRoot,
                $"Ação {label}",
                min,
                max,
                Color.clear);
            hitArea.raycastTarget = true;

            var button = hitArea.gameObject.AddComponent<Button>();
            // The artwork fills the whole canvas. Tinting it through the
            // button transition darkens and blurs the complete lobby, so a
            // transparent hit area owns interaction without touching art.
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                action?.Invoke();
            });
        }

        private void ShowRankedModeNotice()
        {
            if (_duelRoomStatus == null)
                return;

            _duelRoomStatus.text =
                "RANQUEADA • O matchmaking competitivo ainda não está " +
                "disponível nesta versão.";
            _duelRoomStatus.color = Gold;
        }

        private void OpenMultiplayerPanel(
            bool focusJoinCode,
            CompetitivePolicy policy = CompetitivePolicy.Unranked)
        {
            ArcaneArenaMultiplayerController.ShowPanel(
                focusJoinCode,
                policy);
            if (_duelRoomStatus == null)
                return;

            if (policy == CompetitivePolicy.Ranked)
            {
                _duelRoomStatus.text =
                    "RANQUEADA · o elo e os PE serão selados ao iniciar.";
                _duelRoomStatus.color = Gold;
                return;
            }

            _duelRoomStatus.text = focusJoinCode
                ? "Abrindo a entrada por código..."
                : "Preparando uma sala privada...";
            _duelRoomStatus.color = Cyan;
        }

        private RawImage CreateFullCanvasArtwork(
            string name,
            Texture texture)
        {
            var item = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            item.transform.SetParent(_screenRoot, false);
            var rect = item.GetComponent<RectTransform>();
            Stretch(rect);
            var artwork = item.GetComponent<RawImage>();
            artwork.texture = texture;
            artwork.color = Color.white;
            artwork.material = null;
            artwork.raycastTarget = false;
            return artwork;
        }

        private bool TryApplyMainMenuHudOverlay(RawImage artwork)
        {
            if (artwork == null)
                return false;

            if (_mainMenuHudOverlayMaterial == null)
            {
                var authoredMaterial = artwork.material;
                if (authoredMaterial != null &&
                    authoredMaterial.shader != null &&
                    authoredMaterial.shader.name ==
                    "ArcaneArena/UI/MainMenuHudOverlay")
                {
                    _mainMenuHudOverlayMaterial = authoredMaterial;
                    _ownsMainMenuHudOverlayMaterial = false;
                }
            }

            if (_mainMenuHudOverlayMaterial == null)
            {
                _mainMenuHudOverlayMaterial = Resources.Load<Material>(
                    MainMenuHudOverlayMaterialPath);
                _ownsMainMenuHudOverlayMaterial = false;
            }

            if (_mainMenuHudOverlayMaterial == null)
            {
                var shader = Resources.Load<Shader>(
                    MainMenuHudOverlayShaderPath);
                if (shader == null)
                    return false;

                _mainMenuHudOverlayMaterial = new Material(shader)
                {
                    name = "Material HUD da Tela Inicial (Runtime)",
                    hideFlags = HideFlags.DontSave
                };
                _ownsMainMenuHudOverlayMaterial = true;
            }

            artwork.material = _mainMenuHudOverlayMaterial;
            artwork.raycastTarget = false;
            return true;
        }

        private void ApplyAuthoredMainMenuHudOverlay()
        {
            if (_mainMenuSceneView == null)
                return;

            var images = _mainMenuSceneView.GetComponentsInChildren<RawImage>(
                true);
            for (var index = 0; index < images.Length; index++)
            {
                var image = images[index];
                if (image == null ||
                    image.name != "Moldura HUD da Tela Inicial")
                {
                    continue;
                }

                TryApplyMainMenuHudOverlay(image);
                return;
            }
        }

        private void CreateTemplateButton(
            string label,
            Texture texture,
            Vector2 min,
            Vector2 max,
            System.Action action)
        {
            RawImage artwork = CreateFullCanvasArtwork(
                $"Arte Botão {label}",
                texture);
            float artworkOffsetY = label switch
            {
                "MULTIPLAYER" => 0.2065f,
                "DECKS" => -0.1044f,
                "LOJA" => -0.1028f,
                _ => 0f
            };
            artwork.rectTransform.anchorMin +=
                Vector2.up * artworkOffsetY;
            artwork.rectTransform.anchorMax +=
                Vector2.up * artworkOffsetY;
            var hitArea = CreatePanel(
                _screenRoot,
                $"Ação {label}",
                min,
                max,
                Color.clear);
            hitArea.raycastTarget = true;

            var button = hitArea.gameObject.AddComponent<Button>();
            // Keep the imported template untouched. A transparent hit area
            // receives input without tinting or outlining the artwork.
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                action?.Invoke();
            });
        }

        private Button CreateInvisibleButton(
            string label,
            Vector2 min,
            Vector2 max,
            System.Action action)
        {
            var hitArea = CreatePanel(
                _screenRoot,
                $"Ação {label}",
                min,
                max,
                Color.clear);
            hitArea.raycastTarget = true;
            var button = hitArea.gameObject.AddComponent<Button>();
            button.targetGraphic = hitArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                FrontendClickAudio.Play();
                action?.Invoke();
            });
            return button;
        }

        private void BuildVersionOverlay()
        {
            var cover = CreatePanel(
                _screenRoot,
                "Versão Atual",
                new Vector2(0.802f, 0.018f),
                new Vector2(0.914f, 0.072f),
                new Color(0.008f, 0.025f, 0.045f, 0.98f));
            CreateText(
                cover.transform,
                "VERSÃO\n" +
                $"v{ProjectIdentity.ProjectVersion} • CORE {ProjectIdentity.CoreApiVersion}",
                12,
                FontStyle.Normal,
                new Color(0.74f, 0.91f, 0.96f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                TextAnchor.MiddleLeft);
        }

        private void BuildConnectionIndicator()
        {
            var regionCover = CreatePanel(
                _screenRoot,
                "Região Relay",
                new Vector2(0.060f, 0.017f),
                new Vector2(0.190f, 0.073f),
                new Color(0.008f, 0.025f, 0.045f, 0.98f));
            _relayRegionStatus = CreateText(
                regionCover.transform,
                "RELAY\nAUTOMÁTICO",
                12,
                FontStyle.Normal,
                new Color(0.32f, 0.88f, 0.96f, 1f),
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleLeft);

            var barRoot = CreatePanel(
                _screenRoot,
                "Qualidade da Conexão",
                new Vector2(0.132f, 0.033f),
                new Vector2(0.155f, 0.069f),
                new Color(0.008f, 0.025f, 0.045f, 0.98f));
            _connectionBars = new Image[4];
            for (var index = 0; index < _connectionBars.Length; index++)
            {
                float xMin = 0.05f + index * 0.235f;
                float height = 0.30f + index * 0.21f;
                _connectionBars[index] = CreatePanel(
                    barRoot.transform,
                    $"Sinal {index + 1}",
                    new Vector2(xMin, 0.05f),
                    new Vector2(xMin + 0.15f, height),
                    new Color(0.12f, 0.18f, 0.22f, 0.85f));
            }

            var statusCover = CreatePanel(
                _screenRoot,
                "Status da Conexão",
                new Vector2(0.266f, 0.017f),
                new Vector2(0.365f, 0.073f),
                new Color(0.008f, 0.025f, 0.045f, 0.98f));
            _connectionStatus = CreateText(
                statusCover.transform,
                "SERVIÇOS\nVERIFICANDO...",
                12,
                FontStyle.Normal,
                Gold,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleLeft);

            SetConnectionQuality(3, Gold, "VERIFICANDO...");
            if (Application.isBatchMode)
            {
                SetConnectionQuality(
                    Application.internetReachability ==
                    NetworkReachability.NotReachable ? 1 : 4,
                    Application.internetReachability ==
                    NetworkReachability.NotReachable
                        ? Hex("#FF3D45")
                        : Hex("#35E66B"),
                    "DISPONÍVEIS");
                return;
            }

            _connectionMonitor =
                StartCoroutine(MonitorConnectionQuality());
        }

        private IEnumerator MonitorConnectionQuality()
        {
            while (_connectionBars != null && _connectionStatus != null)
            {
                RefreshRelayRegionIndicator();
                if (Application.internetReachability ==
                    NetworkReachability.NotReachable)
                {
                    SetConnectionQuality(1, Hex("#FF3D45"), "SEM CONEXÃO");
                }
                else
                {
                    float startedAt = Time.realtimeSinceStartup;
                    using var request = UnityWebRequest.Head(ConnectionProbeUrl);
                    request.timeout = 4;
                    yield return request.SendWebRequest();
                    int latencyMs = Mathf.RoundToInt(
                        (Time.realtimeSinceStartup - startedAt) * 1000f);

                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.DataProcessingError)
                    {
                        SetConnectionQuality(
                            1,
                            Hex("#FF3D45"),
                            "INDISPONÍVEIS");
                    }
                    else
                    {
                        ApplyMeasuredLatency(latencyMs);
                    }
                }

                yield return new WaitForSecondsRealtime(8f);
            }

            _connectionMonitor = null;
        }

        private void ApplyMeasuredLatency(int latencyMs)
        {
            if (latencyMs <= 90)
            {
                SetConnectionQuality(
                    4,
                    Hex("#35E66B"),
                    "ONLINE");
            }
            else if (latencyMs <= 180)
            {
                SetConnectionQuality(
                    3,
                    Hex("#F1D547"),
                    "ONLINE");
            }
            else if (latencyMs <= 350)
            {
                SetConnectionQuality(
                    2,
                    Hex("#FF8B24"),
                    "COM OSCILAÇÃO");
            }
            else
            {
                SetConnectionQuality(
                    1,
                    Hex("#FF3D45"),
                    "COM OSCILAÇÃO");
            }
        }

        private void RefreshRelayRegionIndicator()
        {
            if (_relayRegionStatus == null)
                return;

            string region = DuelOnlineSession.Instance?.RelayRegion;
            _relayRegionStatus.text = string.IsNullOrWhiteSpace(region)
                ? "RELAY\nAUTOMÁTICO"
                : $"RELAY\n{region.ToUpperInvariant()}";
        }

        private void SetConnectionQuality(
            int filledBars,
            Color color,
            string status)
        {
            if (_connectionBars != null)
            {
                for (var index = 0; index < _connectionBars.Length; index++)
                {
                    if (_connectionBars[index] == null)
                        continue;
                    _connectionBars[index].color = index < filledBars
                        ? color
                        : new Color(0.10f, 0.16f, 0.20f, 0.82f);
                }
            }

            if (_connectionStatus == null)
                return;
            _connectionStatus.text = "STATUS\n" + status;
            _connectionStatus.color = color;
        }

        private void StopMainMenuConnectionMonitor()
        {
            if (_connectionMonitor != null)
                StopCoroutine(_connectionMonitor);
            _connectionMonitor = null;
            _connectionBars = null;
            _connectionStatus = null;
            _relayRegionStatus = null;
        }

        private void ReleaseMainMenuHudOverlayMaterial()
        {
            if (_mainMenuHudOverlayMaterial == null)
                return;

            if (_ownsMainMenuHudOverlayMaterial)
                Destroy(_mainMenuHudOverlayMaterial);
            _mainMenuHudOverlayMaterial = null;
            _ownsMainMenuHudOverlayMaterial = false;
        }
    }
}
