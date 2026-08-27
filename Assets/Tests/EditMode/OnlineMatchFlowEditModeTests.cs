using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class OnlineMatchFlowEditModeTests
    {
        [Test]
        public void HostWaitsForBothScenesAndBothSnapshotsBeforeBegin()
        {
            Type type = TypeByName(
                "ArcaneArena.Multiplayer.OnlineMatchReadinessBarrier");
            object barrier = Activator.CreateInstance(type);
            Invoke(barrier, "Begin", "match-a", 7u);

            Assert.That(Invoke(barrier, "RegisterSceneReady", "match-a", 7u, (byte)0), Is.True);
            Assert.That(Property<bool>(barrier, "CanIssueBegin"), Is.False);
            Assert.That(Invoke(barrier, "RegisterSceneReady", "match-a", 7u, (byte)1), Is.True);
            Assert.That(Property<bool>(barrier, "BothScenesReady"), Is.True);
            Assert.That(Property<bool>(barrier, "CanIssueBegin"), Is.False);

            Assert.That(Invoke(barrier, "SetInitialStateVersion", "match-a", 7u, 12UL), Is.True);
            Assert.That(Invoke(barrier, "RegisterSnapshotApplied", "match-a", 7u, (byte)0, 12UL), Is.True);
            Assert.That(Property<bool>(barrier, "CanIssueBegin"), Is.False);
            Assert.That(Invoke(barrier, "RegisterSnapshotApplied", "match-a", 7u, (byte)1, 12UL), Is.True);
            Assert.That(Property<bool>(barrier, "CanIssueBegin"), Is.True);
            Assert.That(Invoke(barrier, "TryIssueBegin"), Is.True);
            Assert.That(Invoke(barrier, "TryIssueBegin"), Is.False,
                "BeginDuel must be issued exactly once per match/epoch.");
        }

        [Test]
        public void ReadyGateRejectsStaleEpochAndWrongStateVersion()
        {
            Type type = TypeByName(
                "ArcaneArena.Multiplayer.OnlineMatchReadinessBarrier");
            object barrier = Activator.CreateInstance(type);
            Invoke(barrier, "Begin", "current", 3u);

            Assert.That(Invoke(barrier, "RegisterSceneReady", "old", 3u, (byte)0), Is.False);
            Assert.That(Invoke(barrier, "RegisterSceneReady", "current", 2u, (byte)0), Is.False);
            Assert.That(Invoke(barrier, "RegisterSceneReady", "current", 3u, (byte)2), Is.False);
            Assert.That(Invoke(barrier, "SetInitialStateVersion", "current", 3u, 5UL), Is.True);
            Assert.That(Invoke(barrier, "RegisterSnapshotApplied", "current", 3u, (byte)0, 4UL), Is.False);
            Assert.That(Property<int>(barrier, "SceneReadyCount"), Is.Zero);
            Assert.That(Property<int>(barrier, "SnapshotAppliedCount"), Is.Zero);
        }

        [Test]
        public void DuplicateReadyMessagesRemainIdempotent()
        {
            Type type = TypeByName(
                "ArcaneArena.Multiplayer.OnlineMatchReadinessBarrier");
            object barrier = Activator.CreateInstance(type);
            Invoke(barrier, "Begin", "same", 1u);

            Invoke(barrier, "RegisterSceneReady", "same", 1u, (byte)0);
            Invoke(barrier, "RegisterSceneReady", "same", 1u, (byte)0);
            Invoke(barrier, "SetInitialStateVersion", "same", 1u, 9UL);
            Invoke(barrier, "RegisterSnapshotApplied", "same", 1u, (byte)0, 9UL);
            Invoke(barrier, "RegisterSnapshotApplied", "same", 1u, (byte)0, 9UL);

            Assert.That(Property<int>(barrier, "SceneReadyCount"), Is.EqualTo(1));
            Assert.That(Property<int>(barrier, "SnapshotAppliedCount"), Is.EqualTo(1));
        }

        [Test]
        public void PlayerChoiceNeverConsumesTheSceneLoadingTimeout()
        {
            Type stateType = TypeByName(
                "ArcaneArena.Multiplayer.OnlineMatchFlowState");
            Type policyType = TypeByName(
                "ArcaneArena.Multiplayer.OnlineMatchFlowPolicy");
            object choosing = Enum.Parse(stateType, "ChoosingFirstPlayer");
            object preparing = Enum.Parse(stateType, "PreparingTransition");
            MethodInfo usesTimeout = policyType.GetMethod(
                "UsesTransitionTimeout",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(usesTimeout, Is.Not.Null);
            Assert.That(
                usesTimeout.Invoke(null, new[] { choosing }),
                Is.False,
                "Waiting for a human RPS choice must not cancel the match.");
            Assert.That(
                usesTimeout.Invoke(null, new[] { preparing }),
                Is.True,
                "The real scene transition must still be protected by a timeout.");
        }

        [Test]
        public void LeavingAutomaticQueueDoesNotReopenTheGenericLobby()
        {
            Type policyType = TypeByName(
                "ArcaneArena.Multiplayer.OnlineMatchFlowPolicy");
            MethodInfo shouldReopen = policyType.GetMethod(
                "ShouldReopenLobbyAfterLeave",
                BindingFlags.Public | BindingFlags.Static);

            Assert.That(shouldReopen, Is.Not.Null);
            Assert.That(
                shouldReopen.Invoke(null, new object[] { true, true }),
                Is.False);
            Assert.That(
                shouldReopen.Invoke(null, new object[] { true, false }),
                Is.True);
        }

        [TestCase((byte)0, 0, 1, "ENGINE_WIN", "Victory")]
        [TestCase((byte)1, 0, 1, "ENGINE_WIN", "Defeat")]
        [TestCase((byte)1, -1, -1, "DRAW", "Draw")]
        [TestCase((byte)0, -1, -1, "NO_CONTEST", "NoContest")]
        [TestCase((byte)2, 0, 1, "ENGINE_WIN", "Invalid")]
        public void ResultMapsOnlyFromAuthoritativeSeats(
            byte localSeat,
            int winnerSeat,
            int loserSeat,
            string endReason,
            string expected)
        {
            Type mapper = TypeByName(
                "ArcaneArena.Multiplayer.OnlineDuelResultMapper");
            object result = mapper.GetMethod(
                    "Map",
                    BindingFlags.Public | BindingFlags.Static)
                ?.Invoke(null, new object[]
                {
                    localSeat, winnerSeat, loserSeat, endReason
                });
            Assert.That(result?.ToString(), Is.EqualTo(expected));
        }

        private static object Invoke(object target, string method, params object[] args)
        {
            MethodInfo value = target.GetType().GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(value, Is.Not.Null, method);
            return value.Invoke(target, args);
        }

        private static T Property<T>(object target, string name)
        {
            PropertyInfo property = target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(target);
        }

        private static Type TypeByName(string fullName)
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, fullName);
            return type;
        }
    }
}
