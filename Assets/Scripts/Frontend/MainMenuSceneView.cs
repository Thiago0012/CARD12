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
        [SerializeField] private Button friendsButton;
        [SerializeField] private Button missionsButton;

        private GameFrontendBootstrap _boundController;
        private UnityAction _duelAction;
        private UnityAction _decksAction;
        private UnityAction _shopAction;
        private UnityAction _multiplayerAction;
        private UnityAction _settingsAction;
        private UnityAction _profileAction;
        private UnityAction _friendsAction;
        private UnityAction _missionsAction;
        private RectTransform _artworkViewport;
        private Image _artworkImage;
        private MainMenuArtworkFloat _artworkFloat;

        public Canvas SceneCanvas => sceneCanvas;
        public RectTransform DynamicRoot => dynamicRoot;
        public bool IsConfigured =>
            sceneCanvas != null &&
            mainMenuRoot != null &&
            dynamicRoot != null &&
            duelButton != null &&
            decksButton != null &&
            shopButton != null &&
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
            Button profile,
            Button friends = null,
            Button missions = null)
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
            friendsButton = friends;
            missionsButton = missions;
        }

        public void Bind(GameFrontendBootstrap controller)
        {
            if (controller == null || _boundController == controller)
                return;
            Unbind();
            _boundController = controller;
            EnsureMissionsButton();
            EnsureFriendsButton();
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
            BindButton(
                friendsButton,
                ref _friendsAction,
                controller.MainMenuFriends);
            BindButton(
                missionsButton,
                ref _missionsAction,
                controller.MainMenuMissions);
            controller.DecorateMainMenuProfileButton(profileButton);
            controller.DecorateMainMenuFriendsButton(friendsButton);
        }

        public void SetMainMenuVisible(bool visible)
        {
            if (mainMenuRoot != null)
                mainMenuRoot.gameObject.SetActive(visible);
            if (dynamicRoot != null)
                dynamicRoot.gameObject.SetActive(!visible);
        }

        public void SetEquippedArtwork(Sprite sprite, string artworkId)
        {
            EnsureArtworkViewport();
            if (_artworkViewport == null || _artworkImage == null)
                return;

            bool hasArtwork = sprite != null;
            _artworkViewport.gameObject.SetActive(hasArtwork);
            _artworkImage.sprite = sprite;
            _artworkImage.enabled = hasArtwork;
            if (hasArtwork)
                _artworkFloat?.Configure(artworkId);
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
            RemoveButton(friendsButton, ref _friendsAction);
            RemoveButton(missionsButton, ref _missionsAction);
            _boundController = null;
        }

        private void EnsureFriendsButton()
        {
            if (friendsButton != null || mainMenuRoot == null)
                return;

            var item = new GameObject(
                "Ação AMIGOS (SINO)",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            item.transform.SetParent(mainMenuRoot, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            // Área exata do sino desenhado na arte-base da barra superior.
            // Centro medido na arte exibida pela cena: x 0,909 / y 0,9565.
            // A área cobre o hexágono inteiro de forma simétrica.
            rect.anchorMin = new Vector2(0.891f, 0.918f);
            rect.anchorMax = new Vector2(0.927f, 0.995f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image hitArea = item.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            friendsButton = item.GetComponent<Button>();
            friendsButton.targetGraphic = hitArea;
            friendsButton.transition = Selectable.Transition.None;
            Navigation navigation = friendsButton.navigation;
            navigation.mode = Navigation.Mode.None;
            friendsButton.navigation = navigation;
        }

        private void EnsureMissionsButton()
        {
            if (missionsButton != null || mainMenuRoot == null)
                return;
            var item = new GameObject(
                "Ação MISSÕES (CORREIO)",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            item.transform.SetParent(mainMenuRoot, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            // Área do envelope desenhado na barra superior da arte-base.
            rect.anchorMin = new Vector2(0.842f, 0.918f);
            rect.anchorMax = new Vector2(0.878f, 0.995f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            Image hitArea = item.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0.001f);
            missionsButton = item.GetComponent<Button>();
            missionsButton.targetGraphic = hitArea;
            missionsButton.transition = Selectable.Transition.None;
            Navigation navigation = missionsButton.navigation;
            navigation.mode = Navigation.Mode.None;
            missionsButton.navigation = navigation;
        }

        private void EnsureArtworkViewport()
        {
            if (_artworkViewport != null || mainMenuRoot == null)
                return;

            var viewportObject = new GameObject(
                "Artwork Equipada - Recorte da Moldura",
                typeof(RectTransform),
                typeof(RectMask2D));
            viewportObject.transform.SetParent(mainMenuRoot, false);
            _artworkViewport = viewportObject.GetComponent<RectTransform>();
            // Interior medido da moldura direita da arte oficial. O pequeno
            // recuo conserva os filetes ciano/dourado sempre visíveis.
            _artworkViewport.anchorMin = new Vector2(0.365f, 0.165f);
            _artworkViewport.anchorMax = new Vector2(0.955f, 0.845f);
            _artworkViewport.offsetMin = Vector2.zero;
            _artworkViewport.offsetMax = Vector2.zero;
            _artworkViewport.pivot = new Vector2(0.5f, 0.5f);
            _artworkViewport.SetAsLastSibling();

            var imageObject = new GameObject(
                "Arte Flutuante",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(MainMenuArtworkFloat));
            imageObject.transform.SetParent(_artworkViewport, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.025f, 0.025f);
            imageRect.anchorMax = new Vector2(0.975f, 0.975f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.pivot = new Vector2(0.5f, 0.5f);

            _artworkImage = imageObject.GetComponent<Image>();
            _artworkImage.preserveAspect = true;
            _artworkImage.raycastTarget = false;
            _artworkFloat = imageObject.GetComponent<MainMenuArtworkFloat>();
            CanvasGroup canvasGroup = imageObject.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
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
