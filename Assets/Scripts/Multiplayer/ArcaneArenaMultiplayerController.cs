using UnityEngine;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Presentation-only compatibility boundary for the migrated menu.
    /// The legacy online authority is deliberately excluded.
    /// </summary>
    public sealed class ArcaneArenaMultiplayerController : MonoBehaviour
    {
        public static ArcaneArenaMultiplayerController Instance { get; private set; }

        public bool IsOnlineDuelActive => false;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void ShowPanel(bool focusJoinCode = false)
        {
            Debug.LogWarning(
                focusJoinCode
                    ? "Entrada por código aguarda a camada multiplayer do core novo."
                    : "Sala privada aguarda a camada multiplayer do core novo.");
        }

        public void AttachOnlineArena(CardArenaBootstrap arena)
        {
            // Transport and online authority will be implemented on the new core.
        }
    }
}
