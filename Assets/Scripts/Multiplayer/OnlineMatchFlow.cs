using System;
using System.Collections.Generic;

namespace ArcaneArena.Multiplayer
{
    [Serializable]
    public sealed class OnlineMatchFlowConfig
    {
        public float SceneLoadTimeoutSeconds = 45f;
        public float SnapshotApplyTimeoutSeconds = 15f;
        public float ReconnectGraceSeconds = 45f;
        public float MinimumBlackScreenSeconds = 0.35f;
        public float StartLeadSeconds = 0.75f;
    }

    public enum OnlineMatchFlowState
    {
        InSessionWaiting,
        PreparingTransition,
        LoadingDuel,
        WaitingSceneReady,
        Synchronizing,
        WaitingSnapshotAck,
        InDuel,
        DuelFinished,
        ResultScreen,
        Leaving,
        Menu,
        RecoverableError,
        FatalError
    }

    /// <summary>
    /// Host-side two-stage gate. Messages from an old match or transition
    /// epoch cannot mutate the current gate and duplicate messages are
    /// intentionally idempotent.
    /// </summary>
    public sealed class OnlineMatchReadinessBarrier
    {
        private readonly HashSet<byte> sceneReadySeats = new HashSet<byte>();
        private readonly HashSet<byte> snapshotAppliedSeats =
            new HashSet<byte>();

        public string MatchId { get; private set; } = string.Empty;
        public uint TransitionEpoch { get; private set; }
        public ulong InitialStateVersion { get; private set; }
        public bool BeginIssued { get; private set; }
        public int SceneReadyCount => sceneReadySeats.Count;
        public int SnapshotAppliedCount => snapshotAppliedSeats.Count;
        public bool BothScenesReady => sceneReadySeats.Count == 2;
        public bool BothSnapshotsApplied => snapshotAppliedSeats.Count == 2;
        public bool CanIssueBegin =>
            !BeginIssued && BothScenesReady && BothSnapshotsApplied &&
            InitialStateVersion > 0;

        public void Begin(string matchId, uint transitionEpoch)
        {
            if (string.IsNullOrWhiteSpace(matchId))
                throw new ArgumentException("MatchId is required.", nameof(matchId));
            if (transitionEpoch == 0)
                throw new ArgumentOutOfRangeException(nameof(transitionEpoch));

            MatchId = matchId;
            TransitionEpoch = transitionEpoch;
            InitialStateVersion = 0;
            BeginIssued = false;
            sceneReadySeats.Clear();
            snapshotAppliedSeats.Clear();
        }

        public bool RegisterSceneReady(
            string matchId,
            uint transitionEpoch,
            byte seat)
        {
            if (!Matches(matchId, transitionEpoch) || seat > 1)
                return false;
            sceneReadySeats.Add(seat);
            return true;
        }

        public bool SetInitialStateVersion(
            string matchId,
            uint transitionEpoch,
            ulong stateVersion)
        {
            if (!Matches(matchId, transitionEpoch) || stateVersion == 0)
                return false;
            if (InitialStateVersion != 0 && InitialStateVersion != stateVersion)
                return false;
            InitialStateVersion = stateVersion;
            return true;
        }

        public bool RegisterSnapshotApplied(
            string matchId,
            uint transitionEpoch,
            byte seat,
            ulong stateVersion)
        {
            if (!Matches(matchId, transitionEpoch) || seat > 1 ||
                InitialStateVersion == 0 ||
                stateVersion != InitialStateVersion)
            {
                return false;
            }
            snapshotAppliedSeats.Add(seat);
            return true;
        }

        public bool TryIssueBegin()
        {
            if (!CanIssueBegin)
                return false;
            BeginIssued = true;
            return true;
        }

        public void Reset()
        {
            MatchId = string.Empty;
            TransitionEpoch = 0;
            InitialStateVersion = 0;
            BeginIssued = false;
            sceneReadySeats.Clear();
            snapshotAppliedSeats.Clear();
        }

        private bool Matches(string matchId, uint transitionEpoch)
        {
            return TransitionEpoch != 0 && transitionEpoch == TransitionEpoch &&
                   string.Equals(MatchId, matchId, StringComparison.Ordinal);
        }
    }

    public enum OnlineDuelResultKind
    {
        Victory,
        Defeat,
        Draw,
        NoContest,
        Invalid
    }

    public static class OnlineDuelResultMapper
    {
        public static OnlineDuelResultKind Map(
            byte localSeat,
            int winnerSeat,
            int loserSeat,
            string endReason)
        {
            if (localSeat > 1)
                return OnlineDuelResultKind.Invalid;
            if (winnerSeat == localSeat)
                return OnlineDuelResultKind.Victory;
            if (loserSeat == localSeat)
                return OnlineDuelResultKind.Defeat;

            string reason = endReason ?? string.Empty;
            if (reason.IndexOf("DRAW", StringComparison.OrdinalIgnoreCase) >= 0)
                return OnlineDuelResultKind.Draw;
            if (reason.IndexOf("NO_CONTEST", StringComparison.OrdinalIgnoreCase) >= 0 ||
                reason.IndexOf("HOST_DISCONNECTED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return OnlineDuelResultKind.NoContest;
            }
            return OnlineDuelResultKind.Invalid;
        }
    }
}
