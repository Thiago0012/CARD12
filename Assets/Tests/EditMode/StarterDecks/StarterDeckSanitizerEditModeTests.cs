using System.Linq;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class StarterDeckSanitizerEditModeTests
    {
        [Test]
        public void S01_ForbiddenCardsAreRemovedDeterministically()
        {
            BanlistDefinition definition = Definition(
                Entry("10000001", 0));
            var raw = Raw(41);
            raw.mainDeck.Insert(10, "10000001");

            StarterDeckSanitizationResult result =
                StarterDeckSanitizer.Sanitize(
                    raw,
                    new BanlistService(definition));

            Assert.That(result.MainDeck.Count, Is.EqualTo(41));
            Assert.That(result.MainDeck, Does.Not.Contain("10000001"));
            Assert.That(result.Audit.Single().removedPasscode,
                Is.EqualTo("10000001"));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void S02_LimitsAreCombinedAcrossSections()
        {
            BanlistDefinition definition = Definition(
                Entry("10000002", 1));
            var raw = Raw(40);
            raw.mainDeck.Add("10000002");
            raw.sideDeck.Add("10000002");

            StarterDeckSanitizationResult result =
                StarterDeckSanitizer.Sanitize(
                    raw,
                    new BanlistService(definition));

            Assert.That(result.MainDeck, Does.Contain("10000002"));
            Assert.That(result.SideDeck, Is.Empty);
            Assert.That(result.Audit.Count, Is.EqualTo(1));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void S03_MainBelowFortyRemainsBlockedWithoutApprovedOverride()
        {
            BanlistDefinition definition = Definition(
                Entry("10000003", 0));
            var raw = Raw(39);
            raw.mainDeck.Add("10000003");

            StarterDeckSanitizationResult result =
                StarterDeckSanitizer.Sanitize(
                    raw,
                    new BanlistService(definition));

            Assert.That(result.MainDeck.Count, Is.EqualTo(39));
            Assert.That(result.IsLegal, Is.False);
            Assert.That(result.LegalitySummary, Does.Contain("40"));
            Assert.That(result.Audit.Any(entry => !entry.approved), Is.True);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void S05_ApprovedReplacementRestoresLegalMainDeck()
        {
            BanlistDefinition definition = Definition(
                Entry("10000005", 0));
            var raw = Raw(39);
            raw.mainDeck.Add("10000005");
            var approved = new[]
            {
                new ReplacementAuditEntry
                {
                    removedPasscode = "10000005",
                    replacementPasscode = "29999999",
                    section = "Main",
                    reason = "Override aprovado no catalogo.",
                    approved = true
                }
            };

            StarterDeckSanitizationResult result =
                StarterDeckSanitizer.Sanitize(
                    raw,
                    new BanlistService(definition),
                    approved);

            Assert.That(result.MainDeck.Count, Is.EqualTo(40));
            Assert.That(result.MainDeck, Does.Contain("29999999"));
            Assert.That(result.IsLegal, Is.True);
            Assert.That(result.Audit.Any(entry => !entry.approved), Is.False);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void S04_SanitizationIsIdempotent()
        {
            BanlistDefinition definition = Definition(
                Entry("10000004", 2));
            var raw = Raw(40);
            raw.extraDeck.AddRange(new[]
            {
                "10000004", "10000004", "10000004"
            });
            var service = new BanlistService(definition);

            StarterDeckSanitizationResult first =
                StarterDeckSanitizer.Sanitize(raw, service);
            var alreadyClean = new RawStarterDeckDefinition();
            alreadyClean.mainDeck.AddRange(first.MainDeck);
            alreadyClean.extraDeck.AddRange(first.ExtraDeck);
            alreadyClean.sideDeck.AddRange(first.SideDeck);
            StarterDeckSanitizationResult second =
                StarterDeckSanitizer.Sanitize(alreadyClean, service);

            Assert.That(second.MainDeck, Is.EqualTo(first.MainDeck));
            Assert.That(second.ExtraDeck, Is.EqualTo(first.ExtraDeck));
            Assert.That(second.Audit, Is.Empty);
            Object.DestroyImmediate(definition);
        }

        private static RawStarterDeckDefinition Raw(int mainCount)
        {
            var raw = new RawStarterDeckDefinition();
            raw.mainDeck.AddRange(Enumerable.Range(20000000, mainCount)
                .Select(value => value.ToString("00000000")));
            return raw;
        }

        private static BanlistDefinition Definition(
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
    }
}
