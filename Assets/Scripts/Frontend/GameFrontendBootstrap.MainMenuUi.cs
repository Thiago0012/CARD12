using System.Collections;
using ArcaneArena.Multiplayer;
using ArcaneDuel.Game;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    public sealed partial class GameFrontendBootstrap
    {
        private const string MainMenuAssetsPath =
            "Frontend/MainMenuUiAssets";
        private const string MainMenuHudOverlayShaderPath =
            "Frontend/MainMenuHudOverlay";
        private const string MainMenuHudOverlayMaterialPath =
            "Frontend/MainMenuHudOverlayMaterial";
        private const string ConnectionProbeUrl =
            "https://services.api.unity.com";

        private Coroutine _connectionMonitor;
        private Image[] _connectionBars;
        private Text _connectionStatus;
        private Text _relayRegionStatus;
        private MainMenuUiAssets _mainMenuAssets;
        private Material _mainMenuHudOverlayMaterial;
        private bool _ownsMainMenuHudOverlayMaterial;
        private MainMenuSceneView _mainMenuSceneView;

        public void ShowMainMenu()
        {
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

            _font = Resources.GetBuiltinResource<Font>(
                "LegacyRuntime.ttf");
            _canvas = _mainMenuSceneView.SceneCanvas;
            _canvasRect = _canvas != null
                ? _canvas.GetComponent<RectTransform>()
                : null;
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
            OpenBotDuelSelectionFromMainMenu();
        }

        public void MainMenuDecks()
        {
            FrontendClickAudio.Play();
            OpenDeckEditorScene();
        }

        public void MainMenuShop()
        {
            FrontendClickAudio.Play();
            ShowDeckShop();
        }

        public void MainMenuMultiplayer()
        {
            FrontendClickAudio.Play();
            ShowMultiplayerRoom();
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

            CreateTemplateButton(
                "DUELAR",
                _mainMenuAssets.duelButton,
                new Vector2(0.0718f, 0.4692f),
                new Vector2(0.2976f, 0.5654f),
                OpenBotDuelSelectionFromMainMenu);
            CreateTemplateButton(
                "MULTIPLAYER",
                _mainMenuAssets.multiplayerButton,
                new Vector2(0.0730f, 0.3624f),
                new Vector2(0.2988f, 0.4586f),
                ShowMultiplayerRoom);
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
                ShowDeckShop);
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

        private void OpenBotDuelSelectionFromMainMenu()
        {
            if (CanStartWithSelectedDeck())
            {
                ShowBotDeckSelection();
                return;
            }

            ShowDeckGallery();
        }

        private void ShowMultiplayerRoom()
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
                ShowRankedModeNotice);
            CreateMultiplayerLobbyButton(
                "CASUAL",
                _mainMenuAssets.casualModeButton,
                new Vector2(0.0078f, 0.4606f),
                new Vector2(0.2189f, 0.6298f),
                () => OpenMultiplayerPanel(false));
            CreateMultiplayerLobbyButton(
                "TORNEIOS",
                _mainMenuAssets.tournamentModeButton,
                new Vector2(0.0078f, 0.2766f),
                new Vector2(0.2183f, 0.4457f),
                ShowTournamentHub);

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
                ShowMainMenu);

            _duelRoomStatus = CreateText(
                _screenRoot,
                string.Empty,
                20,
                FontStyle.Bold,
                Muted,
                new Vector2(0.035f, 0.018f),
                new Vector2(0.965f, 0.104f),
                TextAnchor.MiddleCenter);
        }

        private void ShowLegacyMultiplayerRoom()
        {
            BuildSharedBackground("MULTIPLAYER");
            BuildHeader("MULTIPLAYER", ShowMainMenu);

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

        private void CreateMultiplayerLobbyButton(
            string label,
            Texture texture,
            Vector2 min,
            Vector2 max,
            System.Action action)
        {
            RawImage artwork = CreateFullCanvasArtwork(
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
            button.targetGraphic = artwork;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.88f, 0.58f, 1f);
            colors.pressedColor = new Color(0.62f, 0.82f, 1f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
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

        private void OpenMultiplayerPanel(bool focusJoinCode)
        {
            ArcaneArenaMultiplayerController.ShowPanel(focusJoinCode);
            if (_duelRoomStatus == null)
                return;

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

        private void CreateInvisibleButton(
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
