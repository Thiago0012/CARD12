using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ArcaneArena.Frontend
{
    /// <summary>
    /// Referencias serializadas do Main Menu. A arte e o layout vivem na
    /// cena; este componente apenas liga os botoes ao fluxo do frontend.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuSceneView : MonoBehaviour
    {
        [Header("Raizes editaveis")]
        [SerializeField] private Canvas sceneCanvas;
        [SerializeField] private RectTransform mainMenuRoot;
        [SerializeField] private RectTransform dynamicRoot;

        [Header("Botoes")]
        [SerializeField] private Button duelButton;
        [SerializeField] private Button decksButton;
        [SerializeField] private Button shopButton;
        [SerializeField] private Button multiplayerButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button profileButton;

        private GameFrontendBootstrap _boundController;
        private UnityAction _duelAction;
        private UnityAction _decksAction;
        private UnityAction _shopAction;
        private UnityAction _multiplayerAction;
        private UnityAction _settingsAction;
        private UnityAction _profileAction;

        public Canvas SceneCanvas => sceneCanvas;
        public RectTransform DynamicRoot => dynamicRoot;
        public bool IsConfigured =>
            sceneCanvas != null &&
            mainMenuRoot != null &&
            dynamicRoot != null &&
            duelButton != null &&
            decksButton != null &&
            shopButton != null &&
            multiplayerButton != null &&
            settingsButton != null;

        public void Configure(
            Canvas canvas,
            RectTransform authoredRoot,
            RectTransform runtimeRoot,
            Button duel,
            Button decks,
            Button shop,
            Button multiplayer,
            Button settings,
            Button profile)
        {
            sceneCanvas = canvas;
            mainMenuRoot = authoredRoot;
            dynamicRoot = runtimeRoot;
            duelButton = duel;
            decksButton = decks;
            shopButton = shop;
            multiplayerButton = multiplayer;
            settingsButton = settings;
            profileButton = profile;
        }

        public void Bind(GameFrontendBootstrap controller)
        {
            if (controller == null || _boundController == controller)
                return;
            Unbind();
            _boundController = controller;
            BindButton(duelButton, ref _duelAction, controller.MainMenuDuel);
            BindButton(decksButton, ref _decksAction, controller.MainMenuDecks);
            BindButton(shopButton, ref _shopAction, controller.MainMenuShop);
            BindButton(
                multiplayerButton,
                ref _multiplayerAction,
                controller.MainMenuMultiplayer);
            BindButton(
                settingsButton,
                ref _settingsAction,
                controller.MainMenuSettings);
            BindButton(
                profileButton,
                ref _profileAction,
                controller.MainMenuProfile);
            controller.DecorateMainMenuProfileButton(profileButton);
        }

        public void SetMainMenuVisible(bool visible)
        {
            if (mainMenuRoot != null)
                mainMenuRoot.gameObject.SetActive(visible);
            if (dynamicRoot != null)
                dynamicRoot.gameObject.SetActive(!visible);
        }

        private static void BindButton(
            Button button,
            ref UnityAction storedAction,
            UnityAction action)
        {
            if (button == null)
                return;
            storedAction = action;
            button.onClick.AddListener(storedAction);
        }

        private void Unbind()
        {
            RemoveButton(duelButton, ref _duelAction);
            RemoveButton(decksButton, ref _decksAction);
            RemoveButton(shopButton, ref _shopAction);
            RemoveButton(multiplayerButton, ref _multiplayerAction);
            RemoveButton(settingsButton, ref _settingsAction);
            RemoveButton(profileButton, ref _profileAction);
            _boundController = null;
        }

        private static void RemoveButton(
            Button button,
            ref UnityAction storedAction)
        {
            if (button != null && storedAction != null)
                button.onClick.RemoveListener(storedAction);
            storedAction = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
