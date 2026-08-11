using UnityEngine;
using ArcaneDuel.Game.Competitive;

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

        public static void ShowPanel(
            bool focusJoinCode = false,
            CompetitivePolicy policy = CompetitivePolicy.Unranked)
        {
            DuelOnlineSession.EnsureInstance().ShowPanel(
                focusJoinCode,
                policy);
        }

        public static void StartRankedMatchmaking()
        {
            DuelOnlineSession.EnsureInstance().StartRankedMatchmaking();
        }

        public void AttachOnlineArena(CardArenaBootstrap arena)
        {
            DuelOnlineSession.EnsureInstance().AttachOnlineArena(arena);
        }
    }
}
