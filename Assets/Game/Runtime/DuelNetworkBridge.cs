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
    }
}
