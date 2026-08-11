using System;
using System.IO;
using System.Linq;
using ArcaneDuel.Game;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class DynamicBotSystemEditModeTests
    {
        [Test]
        public void Catalog_HasThirtyThreeStableUniqueProfiles()
        {
            Assert.That(DynamicBotCatalog.All.Count, Is.EqualTo(33));
            Assert.That(DynamicBotCatalog.All.Select(profile => profile.botId)
                .Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(33));
            Assert.That(DynamicBotCatalog.All.All(profile =>
                !string.IsNullOrWhiteSpace(profile.botId) &&
                !string.IsNullOrWhiteSpace(profile.displayName) &&
                profile.initialRankPoints >= 0 &&
                profile.initialRankPoints <= 200 &&
                profile.minimumDeckPower <= profile.maximumDeckPower), Is.True);
        }

        [TestCase(BotSkillLevel.Beginner, .22f, 1.35f, .18f, 2)]
        [TestCase(BotSkillLevel.Intermediate, .12f, .95f, .10f, 3)]
        [TestCase(BotSkillLevel.Advanced, .06f, .65f, .05f, 4)]
        [TestCase(BotSkillLevel.Expert, .025f, .40f, .02f, 5)]
        [TestCase(BotSkillLevel.Master, .005f, .18f, 0f, int.MaxValue)]
        public void DifficultySettings_MatchSpecification(
            BotSkillLevel skill,
            float epsilon,
            float temperature,
            float maximumSuboptimalRate,
            int topK)
        {
            BotDifficultySettings settings = DynamicBotCatalog.Settings(skill);
            Assert.That(settings.Epsilon, Is.EqualTo(epsilon).Within(.0001f));
            Assert.That(settings.Temperature,
                Is.EqualTo(temperature).Within(.0001f));
            Assert.That(settings.MaximumSuboptimalRate,
                Is.EqualTo(maximumSuboptimalRate).Within(.0001f));
            Assert.That(settings.TopK, Is.EqualTo(topK));
        }

        [Test]
        public void StateRepository_CreatesAndReusesStableBotRecord()
        {
            string folder = Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "Temp",
                "ArcaneBotTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string save = Path.Combine(folder, "bots.json");
            try
            {
                BotProfile profile = DynamicBotCatalog.Find("BOT_024");
                var firstRepository = new BotStateRepository(save);
                BotPersistentRecord first = firstRepository.GetOrCreate(profile);
                var secondRepository = new BotStateRepository(save);
                BotPersistentRecord second = secondRepository.GetOrCreate(profile);

                Assert.That(first.botId, Is.EqualTo("BOT_024"));
                Assert.That(first.rankedPoints,
                    Is.EqualTo(profile.initialRankPoints));
                Assert.That(second.botId, Is.EqualTo(first.botId));
                Assert.That(second.rankedPoints, Is.EqualTo(first.rankedPoints));
                Assert.That(File.Exists(save), Is.True);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void RankedMatchmaker_IsDeterministicAndUsesNearbyPersistentPe()
        {
            string folder = CreateTemporaryFolder();
            string save = Path.Combine(folder, "bots.json");
            try
            {
                BotProfile first = new BotStateRepository(save)
                    .SelectRankedOpponent(
                        105, 424242, Array.Empty<string>());
                BotProfile second = new BotStateRepository(save)
                    .SelectRankedOpponent(
                        105, 424242, Array.Empty<string>());

                Assert.That(second.botId, Is.EqualTo(first.botId));
                Assert.That(Math.Abs(first.initialRankPoints - 105),
                    Is.LessThanOrEqualTo(15));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        [Test]
        public void RankedMatchmaker_AvoidsRecentOpponentWhenAlternativeExists()
        {
            string folder = CreateTemporaryFolder();
            string save = Path.Combine(folder, "bots.json");
            try
            {
                var repository = new BotStateRepository(save);
                BotProfile first = repository.SelectRankedOpponent(
                    105, 7, Array.Empty<string>());
                BotProfile alternative = repository.SelectRankedOpponent(
                    105, 7, new[] { first.botId });

                Assert.That(alternative.botId,
                    Is.Not.EqualTo(first.botId));
                Assert.That(Math.Abs(alternative.initialRankPoints - 105),
                    Is.LessThanOrEqualTo(15));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static string CreateTemporaryFolder()
        {
            string folder = Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "Temp",
                "ArcaneBotTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }
    }
}
