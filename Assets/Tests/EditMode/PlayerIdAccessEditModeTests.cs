using System;
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
            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                fallback,
                PlayerIdCapability.Online,
                allowWhenUnverified: false), Is.False,
                "Online não pode ser liberado quando o catálogo ainda não " +
                "confirmou a conta.");
            Assert.That(PlayerIdAccessPolicy.AllowsStandardCapability(
                fallback,
                PlayerIdCapability.Ranked,
                allowWhenUnverified: false), Is.False);
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

        [Test]
        public void LoginBootstrapCannotOverwriteLoadedPublicProfile()
        {
            Assert.That(
                PlayerIdAccessPolicy.PublicProfileUploadSchemaVersion(false),
                Is.Zero,
                "Antes do save autenticado, o servidor deve preservar o ícone existente.");
            Assert.That(
                PlayerIdAccessPolicy.PublicProfileUploadSchemaVersion(true),
                Is.EqualTo(PlayerIdAccessPolicy.PublicProfileSchemaVersion));
        }

        [Test]
        public void PublicProfileRevisionIsSafeMonotonicUnixMilliseconds()
        {
            long firstTicks = DateTime.UnixEpoch.AddSeconds(1).Ticks;
            long secondTicks = DateTime.UnixEpoch.AddSeconds(2).Ticks;

            long first = PlayerIdAccessPolicy
                .PublicProfileRevisionUtcMilliseconds(firstTicks);
            long second = PlayerIdAccessPolicy
                .PublicProfileRevisionUtcMilliseconds(secondTicks);

            Assert.That(first, Is.EqualTo(1000));
            Assert.That(second, Is.GreaterThan(first));
            Assert.That(second, Is.LessThan(9007199254740991L));
        }
    }
}
