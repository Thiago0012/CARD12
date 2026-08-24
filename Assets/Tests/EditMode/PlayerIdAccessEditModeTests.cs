using System.Collections.Generic;
using ArcaneDuel.Game.Accounts;
using NUnit.Framework;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class PlayerIdAccessEditModeTests
    {
        [Test]
        public void UnverifiedAccountKeepsStandardGameButNeverGetsExclusiveFeature()
        {
            PlayerIdAccessSnapshot fallback =
                PlayerIdAccessPolicy.CreateUnverifiedFallback(
                    "3f8a91c2example-player");

            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                fallback,
                PlayerIdCapability.Game), Is.True);
            Assert.That(PlayerIdAccessPolicy.HasGrantedFeature(
                fallback,
                PlayerIdFeature.ExclusiveAccountContent), Is.False);
            Assert.That(fallback.publicId,
                Does.Match("^[0-9]{12}$"));
            Assert.That(PlayerIdAccessPolicy.FormatPublicId(
                    "3f8a91c2example-player"),
                Is.EqualTo(fallback.publicId),
                "A mesma conta deve conservar sempre o mesmo ID numérico.");
        }

        [Test]
        public void VerifiedRecordAppliesRestrictionsDirectlyToItsId()
        {
            var record = new PlayerIdAccessSnapshot
            {
                playerId = "player-limited-01",
                serverVerified = true,
                blockedCapabilities = new List<string>
                {
                    PlayerIdCapability.Ranked,
                    PlayerIdCapability.Economy
                }
            };

            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                record,
                PlayerIdCapability.Game), Is.True);
            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                record,
                PlayerIdCapability.Online), Is.True);
            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                record,
                PlayerIdCapability.Ranked), Is.False);
            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                record,
                PlayerIdCapability.Economy), Is.False);
        }

        [Test]
        public void ExclusiveFeatureRequiresVerifiedGrantForThatExactAccount()
        {
            var record = new PlayerIdAccessSnapshot
            {
                playerId = "owner-account-id",
                serverVerified = true,
                grantedFeatures = new List<string>
                {
                    PlayerIdFeature.ExclusiveAccountContent
                }
            };

            Assert.That(PlayerIdAccessPolicy.HasGrantedFeature(
                record,
                PlayerIdFeature.ExclusiveAccountContent), Is.True);

            record.serverVerified = false;
            Assert.That(PlayerIdAccessPolicy.HasGrantedFeature(
                record,
                PlayerIdFeature.ExclusiveAccountContent), Is.False);
        }

        [Test]
        public void EntireGameCanBeBlockedForOneCatalogedId()
        {
            var record = new PlayerIdAccessSnapshot
            {
                playerId = "blocked-player-id",
                blockGameAccess = true,
                serverVerified = true,
                message = "Acesso temporariamente indisponível."
            };

            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                record,
                PlayerIdCapability.Game), Is.False);
            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                record,
                PlayerIdCapability.Online), Is.False);
        }

        [Test]
        public void CatalogRejectsNonNumericPublicIdAndRegeneratesIt()
        {
            var record = new PlayerIdAccessSnapshot
            {
                playerId = "canonical-unity-player-id",
                publicId = "#AB12-CD34"
            };

            record.Normalize();

            Assert.That(record.publicId, Does.Match("^[0-9]{12}$"));
            Assert.That(PlayerIdAccessPolicy.IsValidPublicId(record.publicId),
                Is.True);
        }
    }
}
