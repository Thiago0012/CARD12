using UnityEngine;

namespace ArcaneArena.Frontend
{
    [CreateAssetMenu(
        fileName = "MainMenuUiAssets",
        menuName = "Arcane Arena/Main Menu UI Assets")]
    public sealed class MainMenuUiAssets : ScriptableObject
    {
        [Header("Tela inicial")]
        public Texture2D hud;
        public Texture2D duelButton;
        public Texture2D decksButton;
        public Texture2D shopButton;
        public Texture2D multiplayerButton;
        public Texture2D settingsButton;

        [Header("Lobby multiplayer")]
        public Texture2D multiplayerLobby;
        public Texture2D rankedModeButton;
        public Texture2D casualModeButton;
        public Texture2D tournamentModeButton;

        [Header("Áudio")]
        public AudioClip interfaceClick;

        // A apresentacao visual continua disponivel mesmo se o efeito
        // sonoro opcional for renomeado ou ainda nao tiver sido importado.
        public bool IsReady =>
            hud != null &&
            duelButton != null &&
            decksButton != null &&
            shopButton != null &&
            multiplayerButton != null &&
            settingsButton != null;

        public bool HasConfirmationSound => interfaceClick != null;

        public bool HasMultiplayerLobby =>
            multiplayerLobby != null &&
            rankedModeButton != null &&
            casualModeButton != null &&
            tournamentModeButton != null;
    }
}
