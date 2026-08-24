using ArcaneDuel.Game.Social;
using ArcaneDuel.Game.Competitive;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class PlayerFriendsDomainEditModeTests
    {
        [TestCase("483920175641", true)]
        [TestCase("KimDelas", false)]
        [TestCase("Maga do Eclipse", false)]
        [TestCase("Duelista#1234", false)]
        public void SearchAcceptsNumericIdOrPlayerName(
            string query,
            bool expectedNumeric)
        {
            bool valid = PlayerFriendSearchPolicy.TryNormalize(
                query,
                out string normalized,
                out bool numeric,
                out string rejection);

            Assert.That(valid, Is.True, rejection);
            Assert.That(normalized, Is.Not.Empty);
            Assert.That(numeric, Is.EqualTo(expectedNumeric));
        }

        [TestCase("")]
        [TestCase("12")]
        [TestCase("Jo")]
        [TestCase("Nome@Invalido")]
        public void SearchRejectsAmbiguousOrInvalidQuery(string query)
        {
            Assert.That(PlayerFriendSearchPolicy.TryNormalize(
                query,
                out _,
                out _,
                out string rejection), Is.False);
            Assert.That(rejection, Is.Not.Empty);
        }

        [Test]
        public void SearchResponseCreatesIndependentProfileView()
        {
            var response = new FriendSearchResponse
            {
                found = true,
                playerId = "canonical-player-id",
                publicId = "483920175641",
                displayName = "KimDelas",
                equippedIconId = "icon-violet-eclipse-sorceress",
                publicProfileSchemaVersion = 1,
                rankTier = RankTier.Stone,
                rankedPoints = 48,
                duelsPlayed = 12,
                wins = 8,
                losses = 3,
                draws = 1,
                online = true
            };

            FriendProfileView profile = response.ToProfile();

            Assert.That(profile.publicId, Is.EqualTo("483920175641"));
            Assert.That(profile.displayName, Is.EqualTo("KimDelas"));
            Assert.That(profile.equippedIconId,
                Is.EqualTo("icon-violet-eclipse-sorceress"));
            Assert.That(profile.rankTier, Is.EqualTo(RankTier.Stone));
            Assert.That(profile.rankedPoints, Is.EqualTo(48));
            Assert.That(profile.duelsPlayed, Is.EqualTo(12));
            Assert.That(profile.wins, Is.EqualTo(8));
            Assert.That(profile.presence, Is.EqualTo(
                FriendPresenceState.Online));
        }

        [Test]
        public void ProfileCopyPreservesPublicVisualAndCompetitiveSummary()
        {
            var original = new FriendProfileView
            {
                playerId = "player-01",
                publicId = "483920175641",
                displayName = "KimDelas",
                equippedIconId = "icon-violet-eclipse-sorceress",
                publicProfileSchemaVersion = 1,
                rankTier = RankTier.Gold,
                rankedPoints = 132,
                duelsPlayed = 35,
                wins = 20,
                losses = 12,
                draws = 3,
                profileUpdatedUtcUnixSeconds = 123456
            };

            FriendProfileView copy = original.Copy();

            Assert.That(copy, Is.Not.SameAs(original));
            Assert.That(copy.equippedIconId, Is.EqualTo(
                original.equippedIconId));
            Assert.That(copy.rankTier, Is.EqualTo(RankTier.Gold));
            Assert.That(copy.rankedPoints, Is.EqualTo(132));
            Assert.That(copy.duelsPlayed, Is.EqualTo(35));
            Assert.That(copy.profileUpdatedUtcUnixSeconds, Is.EqualTo(123456));
        }
    }
}
