using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ArcaneDuel.Game.Competitive;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class RankPointServiceEditModeTests
    {
        [TestCase(0, RankTier.Wood)]
        [TestCase(24, RankTier.Wood)]
        [TestCase(25, RankTier.Stone)]
        [TestCase(49, RankTier.Stone)]
        [TestCase(50, RankTier.Iron)]
        [TestCase(74, RankTier.Iron)]
        [TestCase(75, RankTier.Silver)]
        [TestCase(99, RankTier.Silver)]
        [TestCase(100, RankTier.Gold)]
        [TestCase(124, RankTier.Gold)]
        [TestCase(125, RankTier.Platinum)]
        [TestCase(149, RankTier.Platinum)]
        [TestCase(150, RankTier.Diamond)]
        [TestCase(174, RankTier.Diamond)]
        [TestCase(175, RankTier.GrandMaster)]
        [TestCase(200, RankTier.GrandMaster)]
        public void ResolveTier_UsesNormativeThresholds(
            int points,
            RankTier expected)
        {
            Assert.That(RankRules.ResolveTier(points), Is.EqualTo(expected));
        }

        [TestCase(RankTier.Wood, 7, 0)]
        [TestCase(RankTier.Stone, 6, -1)]
        [TestCase(RankTier.Iron, 5, -2)]
        [TestCase(RankTier.Silver, 5, -3)]
        [TestCase(RankTier.Gold, 4, -4)]
        [TestCase(RankTier.Platinum, 3, -4)]
        [TestCase(RankTier.Diamond, 3, -5)]
        [TestCase(RankTier.GrandMaster, 2, -6)]
        public void SameTier_UsesNormativeBaseDeltas(
            RankTier tier,
            int expectedWin,
            int expectedLoss)
        {
            int points = RankRules.Definition(tier).Minimum + 10;
            RankChangeReceipt win = Create(points, points, RankedOutcome.Win);
            RankChangeReceipt loss = Create(points, points, RankedOutcome.Loss);
            Assert.That(win.delta, Is.EqualTo(expectedWin));
            Assert.That(loss.delta, Is.EqualTo(expectedLoss));
        }

        [Test]
        public void RankDifference_AdjustsWinAndLossExactlyOnce()
        {
            Assert.That(Create(100, 150, RankedOutcome.Win).delta, Is.EqualTo(5));
            Assert.That(Create(100, 50, RankedOutcome.Win).delta, Is.EqualTo(3));
            Assert.That(Create(100, 150, RankedOutcome.Loss).delta, Is.EqualTo(-3));
            Assert.That(Create(100, 50, RankedOutcome.Loss).delta, Is.EqualTo(-5));
        }

        [Test]
        public void Promotion_GrantsShield_AndNextNormalLossProtectsFloor()
        {
            RankChangeReceipt promotion = Create(124, 124, RankedOutcome.Win);
            Assert.That(promotion.newTier, Is.EqualTo(RankTier.Platinum));
            Assert.That(promotion.shieldGranted, Is.True);
            Assert.That(promotion.shieldActiveAfter, Is.True);

            RankChangeReceipt protectedLoss = Create(
                125,
                125,
                RankedOutcome.Loss,
                shield: true,
                shieldTier: RankTier.Platinum);
            Assert.That(protectedLoss.newPoints, Is.EqualTo(125));
            Assert.That(protectedLoss.shieldConsumed, Is.True);
            Assert.That(protectedLoss.shieldPreventedDemotion, Is.True);
            Assert.That(protectedLoss.demoted, Is.False);
        }

        [Test]
        public void ConfirmedAbandonment_AddsPenalty_AndIgnoresShield()
        {
            RankChangeReceipt receipt = Create(
                125,
                125,
                RankedOutcome.ConfirmedAbandonment,
                shield: true,
                shieldTier: RankTier.Platinum);
            Assert.That(receipt.delta, Is.EqualTo(-5));
            Assert.That(receipt.newTier, Is.EqualTo(RankTier.Gold));
            Assert.That(receipt.shieldConsumed, Is.True);
            Assert.That(receipt.shieldPreventedDemotion, Is.False);
            Assert.That(receipt.abandonmentPenaltyApplied, Is.True);
        }

        [Test]
        public void PointsAreClamped_AndUnrankedDoesNotChangeStateVersion()
        {
            RankChangeReceipt maximum = Create(200, 200, RankedOutcome.Win);
            Assert.That(maximum.newPoints, Is.EqualTo(200));
            Assert.That(maximum.delta, Is.Zero);

            RankedMatchSnapshot match = Match(60, 60, CompetitivePolicy.Unranked);
            Assert.That(RankPointService.TryCreateReceipt(
                match, 0, RankedOutcome.Win, out RankChangeReceipt casual,
                out _), Is.True);
            Assert.That(casual.delta, Is.Zero);
            Assert.That(casual.stateVersionAfter,
                Is.EqualTo(casual.stateVersionBefore));
            Assert.That(casual.status, Is.EqualTo(RankReceiptStatus.NotRanked));
        }

        [Test]
        public void GrandMasterProgress_UsesTheFull175To200Span()
        {
            Assert.That(RankRules.TierProgress01(175), Is.EqualTo(0f));
            Assert.That(RankRules.TierProgress01(199), Is.EqualTo(24f / 25f));
            Assert.That(RankRules.TierProgress01(200), Is.EqualTo(1f));
        }

        [Test]
        public void RepositoryCommit_IsAtomicAndIdempotent()
        {
            Type repositoryType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(
                    "ArcaneArena.Frontend.DeckRepository"))
                .FirstOrDefault(type => type != null);
            Assert.That(repositoryType, Is.Not.Null,
                "DeckRepository não foi carregado na Assembly-CSharp.");

            string projectRoot = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, ".."));
            string folder = Path.Combine(
                projectRoot,
                "Temp",
                "ArcaneRankTests",
                Guid.NewGuid().ToString("N"));
            StringAssert.StartsWith(
                Path.GetFullPath(projectRoot) + Path.DirectorySeparatorChar,
                Path.GetFullPath(folder),
                "O teste só pode criar dados temporários dentro do projeto no disco D.");
            Directory.CreateDirectory(folder);
            string save = Path.Combine(folder, "decks.json");
            try
            {
                object repository = Activator.CreateInstance(
                    repositoryType,
                    new object[] { save });
                MethodInfo load = repositoryType.GetMethod("Load");
                load.Invoke(repository, new object[] { null, false });
                RankPlayerSnapshot local = (RankPlayerSnapshot)repositoryType
                    .GetMethod("CaptureRankSnapshot")
                    .Invoke(repository, null);
                var match = new RankedMatchSnapshot
                {
                    matchId = "idempotency-match",
                    policy = CompetitivePolicy.Ranked,
                    source = CompetitiveMatchSource.PrivateRoom,
                    rulesVersion = RankRules.RulesVersion,
                    rulesHash = RankRules.RulesHash,
                    sealedAtUtcTicks = DateTime.UtcNow.Ticks,
                    seat0 = local,
                    seat1 = Snapshot("opponent", 0, 1)
                };
                Assert.That(RankPointService.TryCreateReceipt(
                    match, 0, RankedOutcome.Win,
                    out RankChangeReceipt proposed, out _), Is.True);

                MethodInfo commit = repositoryType.GetMethod(
                    "TryCommitRankReceipt");
                object[] first = { proposed, null, null };
                Assert.That((bool)commit.Invoke(repository, first), Is.True);
                RankChangeReceipt applied = (RankChangeReceipt)first[1];
                Assert.That(applied.status, Is.EqualTo(RankReceiptStatus.Applied));

                object[] second = { proposed, null, null };
                Assert.That((bool)commit.Invoke(repository, second), Is.True);
                RankChangeReceipt duplicate = (RankChangeReceipt)second[1];
                Assert.That(duplicate.status,
                    Is.EqualTo(RankReceiptStatus.AlreadyProcessed));

                RankPlayerSnapshot after = (RankPlayerSnapshot)repositoryType
                    .GetMethod("CaptureRankSnapshot")
                    .Invoke(repository, null);
                Assert.That(after.rankedPoints, Is.EqualTo(applied.newPoints));
                Assert.That(after.stateVersion,
                    Is.EqualTo(applied.stateVersionAfter));
            }
            finally
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
        }

        private static RankChangeReceipt Create(
            int localPoints,
            int opponentPoints,
            RankedOutcome outcome,
            bool shield = false,
            RankTier shieldTier = RankTier.Wood)
        {
            RankedMatchSnapshot match = Match(
                localPoints,
                opponentPoints,
                CompetitivePolicy.Ranked,
                shield,
                shieldTier);
            Assert.That(RankPointService.TryCreateReceipt(
                match, 0, outcome, out RankChangeReceipt receipt,
                out string rejection), Is.True, rejection);
            return receipt;
        }

        private static RankedMatchSnapshot Match(
            int localPoints,
            int opponentPoints,
            CompetitivePolicy policy,
            bool shield = false,
            RankTier shieldTier = RankTier.Wood)
        {
            RankPlayerSnapshot local = Snapshot("local", localPoints, 3);
            local.promotionShieldActive = shield;
            local.promotionShieldTier = shieldTier;
            return new RankedMatchSnapshot
            {
                matchId = Guid.NewGuid().ToString("N"),
                policy = policy,
                source = CompetitiveMatchSource.PrivateRoom,
                rulesVersion = RankRules.RulesVersion,
                rulesHash = RankRules.RulesHash,
                sealedAtUtcTicks = DateTime.UtcNow.Ticks,
                seat0 = local,
                seat1 = Snapshot("opponent", opponentPoints, 9)
            };
        }

        private static RankPlayerSnapshot Snapshot(
            string id,
            int points,
            int version)
        {
            return new RankPlayerSnapshot
            {
                stablePlayerId = id,
                rankedPoints = points,
                tier = RankRules.ResolveTier(points),
                stateVersion = version,
                promotionShieldActive = false,
                promotionShieldTier = RankTier.Wood,
                rulesVersion = RankRules.RulesVersion,
                rulesHash = RankRules.RulesHash
            };
        }
    }
}
