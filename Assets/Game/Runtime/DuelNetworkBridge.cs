using System;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.DuelEngine.Protocol;
using ArcaneDuel.DuelEngine.State;

namespace ArcaneDuel.Game
{
    /// <summary>
    /// Narrow assembly boundary between the Core-facing game assembly and the
    /// authored arena/network assembly. It prevents the Core layer from
    /// depending on UI or transport packages.
    /// </summary>
    public interface IDuelNetworkState
    {
        string Status { get; }
        bool HasDuelClock { get; }
        float LocalDuelTimeRemaining { get; }
        float OpponentDuelTimeRemaining { get; }
        byte ActiveDuelClockPlayer { get; }

        void ApplyTo(
            DuelPresentationState state,
            CardDatabase database,
            out DuelPrompt prompt);

        bool TryGetCombatStats(
            byte controller,
            byte location,
            uint sequence,
            out int attack,
            out int defense);
    }

    public static class DuelOnlineBridge
    {
        public static Action<DuelChoice> SubmitReplicaChoice;
        public static Action<byte[], ulong> SubmitReplicaResponse;

        /// <summary>
        /// Set before the multiplayer session opens DuelArena. The authored
        /// arena uses it to avoid briefly starting an unrelated local duel
        /// before the host/client authority is attached.
        /// </summary>
        public static bool OnlineArenaTransitionPending { get; private set; }

        public static void BeginOnlineArenaTransition()
        {
            OnlineArenaTransitionPending = true;
        }

        public static void CompleteOnlineArenaTransition()
        {
            OnlineArenaTransitionPending = false;
        }
    }
}
