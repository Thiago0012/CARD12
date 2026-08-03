using System.Collections.Generic;
using System.Linq;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class BanlistEditModeTests
    {
        [Test]
        public void B01_AbsentCard_AllowsThreeCopies()
        {
            BanlistDefinition definition = CreateDefinition();
            var service = new BanlistService(definition);
            List<string> main = Filler(37);
            main.AddRange(Enumerable.Repeat("99999999", 3));

            Assert.That(
                DeckLegalityValidator.Validate(main, null, null, service).IsLegal,
                Is.True);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void B02_ForbiddenCard_IsRejected()
        {
            BanlistDefinition definition = CreateDefinition(
                Entry("00440556", 0));
            var service = new BanlistService(definition);
            List<string> main = Filler(39);
            main.Add("00440556");

            DeckLegalityResult result =
                DeckLegalityValidator.Validate(main, null, null, service);

            Assert.That(result.IsLegal, Is.False);
            Assert.That(result.Summary, Does.Contain("máximo 0"));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void B03_LimitedCard_IsCombinedAcrossAllSections()
        {
            BanlistDefinition definition = CreateDefinition(
                Entry("24224830", 1));
            var service = new BanlistService(definition);
            List<string> main = Filler(39);
            main.Add("24224830");

            DeckLegalityResult result = DeckLegalityValidator.Validate(
                main,
                null,
                new[] { "24224830" },
                service);

            Assert.That(result.IsLegal, Is.False);
            Assert.That(result.Summary, Does.Contain("usa 2 cópia(s)"));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void B04_SemiLimitedCard_RejectsThirdCopy()
        {
            BanlistDefinition definition = CreateDefinition(
                Entry("94145021", 2));
            var service = new BanlistService(definition);
            List<string> main = Filler(39);
            main.Add("94145021");

            DeckLegalityResult result = DeckLegalityValidator.Validate(
                main,
                new[] { "94145021" },
                new[] { "94145021" },
                service);

            Assert.That(result.IsLegal, Is.False);
            Assert.That(result.Summary, Does.Contain("máximo 2"));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void B05_SideDeck_LimitIsFifteen()
        {
            BanlistDefinition definition = CreateDefinition();
            var service = new BanlistService(definition);

            DeckLegalityResult result = DeckLegalityValidator.Validate(
                Filler(40),
                null,
                Filler(16, 70000000),
                service);

            Assert.That(result.IsLegal, Is.False);
            Assert.That(result.Summary, Does.Contain("Side Deck"));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void B06_LeadingZeroPasscode_RemainsCanonical()
        {
            BanlistDefinition definition = CreateDefinition(
                Entry("00440556", 0));
            var service = new BanlistService(definition);

            Assert.That(BanlistService.NormalizePasscode("440556"),
                Is.EqualTo("00440556"));
            Assert.That(service.MaximumCopies("440556"), Is.Zero);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void B07_ActiveAsset_HasNormativeCountsAndHash()
        {
            BanlistDefinition definition = Resources.Load<BanlistDefinition>(
                "Banlist/tcg_eu_2026_05_18");

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.Id, Is.EqualTo("tcg_eu_2026_05_18"));
            Assert.That(definition.EffectiveDate, Is.EqualTo("2026-05-18"));
            Assert.That(definition.Entries.Count, Is.EqualTo(226));
            Assert.That(definition.Entries.Count(entry => entry.maxCopies == 0),
                Is.EqualTo(119));
            Assert.That(definition.Entries.Count(entry => entry.maxCopies == 1),
                Is.EqualTo(97));
            Assert.That(definition.Entries.Count(entry => entry.maxCopies == 2),
                Is.EqualTo(10));
            Assert.That(definition.SourceSha256, Is.EqualTo(
                "946f0c25ca1676397353e93c291d25577daf3bdded6160f9efb26fe715a40260"));
            Assert.That(definition.ForbiddenBadge, Is.Not.Null);
            Assert.That(definition.LimitedBadge, Is.Not.Null);
            Assert.That(definition.SemiLimitedBadge, Is.Not.Null);
        }

        [Test]
        public void ManifestHash_IsStableAcrossSectionReordering()
        {
            string first = DeckManifestHasher.ComputeSha256(
                BanlistService.ActiveBanlistId,
                new[] { "00000002", "1" },
                new[] { "4", "3" },
                null);
            string second = DeckManifestHasher.ComputeSha256(
                BanlistService.ActiveBanlistId,
                new[] { "00000001", "2" },
                new[] { "3", "4" },
                null);

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Length, Is.EqualTo(64));
        }

        private static BanlistDefinition CreateDefinition(
            params BanlistEntry[] entries)
        {
            var seed = new BanlistSeedFile
            {
                id = BanlistService.ActiveBanlistId,
                effectiveDate = "2026-05-18",
                entries = entries.ToList()
            };
            BanlistDefinition definition =
                ScriptableObject.CreateInstance<BanlistDefinition>();
            definition.Initialize(seed, null, null, null);
            return definition;
        }

        private static BanlistEntry Entry(string passcode, int maximum)
        {
            return new BanlistEntry
            {
                officialName = passcode,
                passcode = passcode,
                maxCopies = maximum
            };
        }

        private static List<string> Filler(int count, int start = 10000000)
        {
            return Enumerable.Range(start, count)
                .Select(value => value.ToString("00000000"))
                .ToList();
        }
    }
}
