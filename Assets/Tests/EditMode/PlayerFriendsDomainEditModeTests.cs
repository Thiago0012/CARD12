using ArcaneDuel.Game.Social;
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
                online = true
            };

            FriendProfileView profile = response.ToProfile();

            Assert.That(profile.publicId, Is.EqualTo("483920175641"));
            Assert.That(profile.displayName, Is.EqualTo("KimDelas"));
            Assert.That(profile.presence, Is.EqualTo(
                FriendPresenceState.Online));
        }
    }
}
