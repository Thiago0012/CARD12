using System.Collections.Generic;
using System.IO;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class DeckSystemEditModeTests
    {
        private CardDatabase database;
        private CardVisualCatalog visuals;

        [SetUp]
        public void SetUp()
        {
            database = CardDatabase.LoadDefault();
            visuals = CardVisualCatalog.LoadDefault();
        }

        [Test]
        public void StarterDeckIsValidAndUsesOfficialCodes()
        {
            DeckFile deck = DeckRepository.CreateStarterDeck();
            DeckValidationResult result =
                DeckRules.Validate(deck, database, visuals);
            Assert.That(result.IsValid, Is.True, result.Summary);
            Assert.That(deck.mainDeck, Has.Count.EqualTo(40));
            Assert.That(deck.extraDeck, Has.Count.EqualTo(3));
        }

        [Test]
        public void FourthCopyIsRejectedAcrossMainAndExtraSections()
        {
            DeckFile deck = DeckRepository.CreateStarterDeck();
            deck.mainDeck.Add(89631139);
            DeckValidationResult result =
                DeckRules.Validate(deck, database, visuals);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Summary, Does.Contain("excede o limite"));
        }

        [Test]
        public void ExtraDeckMonsterInMainDeckIsRejected()
        {
            DeckFile deck = DeckRepository.CreateStarterDeck();
            deck.mainDeck[0] = 11901678;
            DeckValidationResult result =
                DeckRules.Validate(deck, database, visuals);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Summary, Does.Contain("seção incorreta"));
        }

        [Test]
        public void VersionedDeckRoundTripsAsReadableJson()
        {
            string path = Path.Combine(
                Application.temporaryCachePath,
                "arcane-deck-roundtrip.json");
            try
            {
                DeckFile source = DeckRepository.CreateStarterDeck();
                DeckRepository.Save(path, source, database, visuals);
                string json = File.ReadAllText(path);
                Assert.That(json, Does.Contain("\"schemaVersion\": 1"));
                DeckFile loaded = DeckRepository.Load(path);
                Assert.That(loaded.name, Is.EqualTo(source.name));
                Assert.That(loaded.mainDeck, Is.EqualTo(source.mainDeck));
                Assert.That(loaded.extraDeck, Is.EqualTo(source.extraDeck));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
