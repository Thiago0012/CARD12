using UnityEngine;

namespace ArcaneArena.Multiplayer
{
    /// <summary>
    /// Compatibility facade kept for the authored menu. The real Relay
    /// session lives in <see cref="DuelOnlineSession"/> and persists while
    /// the player moves from the frontend into the arena scene.
    /// </summary>
    public sealed class ArcaneArenaMultiplayerController : MonoBehaviour
    {
        public static ArcaneArenaMultiplayerController Instance { get; private set; }

        public bool IsOnlineDuelActive =>
            DuelOnlineSession.Instance != null &&
            DuelOnlineSession.Instance.IsOnlineDuelActive;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void ShowPanel(bool focusJoinCode = false)
        {
            DuelOnlineSession.EnsureInstance().ShowPanel(focusJoinCode);
        }

        public void AttachOnlineArena(CardArenaBootstrap arena)
        {
            DuelOnlineSession.EnsureInstance().AttachOnlineArena(arena);
        }
    }
}
