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
                publicProfileRevisionUtcMilliseconds = 1787616401000,
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
            Assert.That(profile.publicProfileRevisionUtcMilliseconds,
                Is.EqualTo(1787616401000));
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
                profileUpdatedUtcUnixSeconds = 123456,
                publicProfileRevisionUtcMilliseconds = 1787616401000
            };

            FriendProfileView copy = original.Copy();

            Assert.That(copy, Is.Not.SameAs(original));
            Assert.That(copy.equippedIconId, Is.EqualTo(
                original.equippedIconId));
            Assert.That(copy.rankTier, Is.EqualTo(RankTier.Gold));
            Assert.That(copy.rankedPoints, Is.EqualTo(132));
            Assert.That(copy.duelsPlayed, Is.EqualTo(35));
            Assert.That(copy.profileUpdatedUtcUnixSeconds, Is.EqualTo(123456));
            Assert.That(copy.publicProfileRevisionUtcMilliseconds,
                Is.EqualTo(1787616401000));
        }

        [TestCase("casual", FriendDuelMode.Casual, "casual")]
        [TestCase("ranked", FriendDuelMode.Ranked, "ranked")]
        [TestCase("RANKED", FriendDuelMode.Ranked, "ranked")]
        public void DuelChallengeModeRoundTripsThroughServerValue(
            string serverValue,
            FriendDuelMode expected,
            string serialized)
        {
            FriendDuelMode mode =
                FriendDuelChallengePolicy.ParseMode(serverValue);

            Assert.That(mode, Is.EqualTo(expected));
            Assert.That(
                FriendDuelChallengePolicy.SerializeMode(mode),
                Is.EqualTo(serialized));
        }

        [TestCase("pending", true)]
        [TestCase("accepted", true)]
        [TestCase("ready", true)]
        [TestCase("joined", false)]
        [TestCase("declined", false)]
        [TestCase("cancelled", false)]
        [TestCase("expired", false)]
        public void DuelChallengeOnlyKeepsNegotiationStatesActive(
            string serverStatus,
            bool expectedActive)
        {
            FriendDuelChallengeStatus status =
                FriendDuelChallengePolicy.ParseStatus(serverStatus);

            Assert.That(
                FriendDuelChallengePolicy.IsActive(status),
                Is.EqualTo(expectedActive));
        }

        [Test]
        public void DuelChallengeAcceptRequiresRecipientPendingAndUnexpired()
        {
            var challenge = new FriendDuelChallengeView
            {
                recipientPlayerId = "recipient-player",
                status = "pending",
                expiresUtcUnixSeconds = 500
            };

            Assert.That(
                FriendDuelChallengePolicy.CanAccept(
                    challenge,
                    "recipient-player",
                    499),
                Is.True);
            Assert.That(
                FriendDuelChallengePolicy.CanAccept(
                    challenge,
                    "different-player",
                    499),
                Is.False);
            Assert.That(
                FriendDuelChallengePolicy.CanAccept(
                    challenge,
                    "recipient-player",
                    500),
                Is.False);
            challenge.status = "accepted";
            Assert.That(
                FriendDuelChallengePolicy.CanAccept(
                    challenge,
                    "recipient-player",
                    499),
                Is.False);
        }

        [Test]
        public void DuelChallengeCopyKeepsPrivateSessionNegotiationData()
        {
            var challenge = new FriendDuelChallengeView
            {
                challengeId = "0123456789abcdef0123456789abcdef",
                senderPlayerId = "sender",
                recipientPlayerId = "recipient",
                duelMode = "ranked",
                status = "ready",
                roomCode = "ABC123",
                expiresUtcUnixSeconds = 900
            };

            FriendDuelChallengeView copy = challenge.Copy();

            Assert.That(copy, Is.Not.SameAs(challenge));
            Assert.That(copy.Mode, Is.EqualTo(FriendDuelMode.Ranked));
            Assert.That(copy.Status,
                Is.EqualTo(FriendDuelChallengeStatus.Ready));
            Assert.That(copy.roomCode, Is.EqualTo("ABC123"));
            Assert.That(copy.IsActive, Is.True);
        }
    }
}
