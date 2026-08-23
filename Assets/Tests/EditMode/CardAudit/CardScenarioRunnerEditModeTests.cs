using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode.CardAudit
{
    [Serializable]
    internal sealed class FirstBatchDossierDocument
    {
        public FirstBatchDossierCard[] cards;
    }

    [Serializable]
    internal sealed class FirstBatchDossierCard
    {
        public string officialCardId;
        public string name;
    }

    [Category("CardAudit.Phase3")]
    public sealed class CardScenarioRunnerEditModeTests
    {
        [TestCaseSource(nameof(FirstBatchCards))]
        public void FirstBatchCardAdvancesBothSeatsThroughAuthoritativeCore(
            uint cardCode,
            string cardName)
        {
            ulong seed = 0xCA4D5CE000000000UL + cardCode;
            var runner = new CardScenarioRunner();
            CardScenarioRunResult result = runner.Run(
                cardCode,
                seed,
                targetTurns: 3);

            Assert.That(result.CardCode, Is.EqualTo(cardCode));
            Assert.That(result.CardName, Is.Not.Null.And.Not.Empty,
                $"The published card {cardCode:00000000} ({cardName}) has no localized name.");
            Assert.That(result.Completed, Is.True, result.Diagnostic);
            Assert.That(result.PlayerOneDecisions, Is.GreaterThan(0),
                result.Diagnostic);
            Assert.That(result.PlayerTwoDecisions, Is.GreaterThan(0),
                result.Diagnostic);
            TestContext.Progress.WriteLine(result.Diagnostic);
        }

        [TestCase(89631139u)] // Normal monster.
        [TestCase(38517737u)] // Effect monster.
        [TestCase(59811955u)] // Spell.
        [TestCase(11443677u)] // Extra Deck.
        public void RepresentativeScenarioFingerprintIsDeterministic(
            uint cardCode)
        {
            const ulong seed = 0xD37E4D1A5C3E0001UL;
            var runner = new CardScenarioRunner();
            CardScenarioRunResult first = runner.Run(
                cardCode,
                seed,
                targetTurns: 3);
            CardScenarioRunResult second = runner.Run(
                cardCode,
                seed,
                targetTurns: 3);

            Assert.That(first.Completed, Is.True, first.Diagnostic);
            Assert.That(second.Completed, Is.True, second.Diagnostic);
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint),
                $"Non-deterministic Core trace for {cardCode:00000000}.\n" +
                first.Diagnostic + "\n" + second.Diagnostic);
            TestContext.Progress.WriteLine(first.Diagnostic);
        }

        private static IEnumerable<TestCaseData> FirstBatchCards()
        {
            FirstBatchDossierDocument document = LoadDossiers();
            foreach (FirstBatchDossierCard card in document.cards)
            {
                uint code = uint.Parse(
                    card.officialCardId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture);
                yield return new TestCaseData(code, card.name)
                    .SetName($"CardScenario_{code:00000000}_{SafeName(card.name)}");
            }
        }

        private static FirstBatchDossierDocument LoadDossiers()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                ".."));
            string path = Path.Combine(
                projectRoot,
                "Documentation",
                "CardAudit",
                "FirstBatchDossiers.json");
            Assert.That(File.Exists(path), Is.True,
                "Generate FirstBatchDossiers.json before running Phase 3.");
            FirstBatchDossierDocument document =
                JsonUtility.FromJson<FirstBatchDossierDocument>(
                    File.ReadAllText(path));
            Assert.That(document?.cards, Is.Not.Null);
            Assert.That(document.cards.Length, Is.InRange(25, 50));
            return document;
        }

        private static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unnamed";
            return new string(value
                .Where(character => char.IsLetterOrDigit(character))
                .Take(36)
                .ToArray());
        }
    }
}
