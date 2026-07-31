using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class ToonDeckContentEditModeTests
    {
        [Serializable]
        private sealed class LocalizationFile
        {
            public LocalizationCard[] cards;
        }

        [Serializable]
        private sealed class LocalizationCard
        {
            public uint code;
            public string name;
            public string description;
        }

        [Test]
        public void ToonDeckHasTheExactMainAndExtraDeckSizes()
        {
            Assert.That(CuratedDeckLists.ToonTestMain, Has.Length.EqualTo(40));
            Assert.That(CuratedDeckLists.ToonTestExtra, Has.Length.EqualTo(15));
            Assert.That(
                CuratedDeckLists.ToonTestMain
                    .Concat(CuratedDeckLists.ToonTestExtra)
                    .Distinct()
                    .Count(),
                Is.EqualTo(33));
        }

        [Test]
        public void EveryToonCardHasPortugueseDataArtAndOfficialScript()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            uint[] codes = CuratedDeckLists.ToonTestMain
                .Concat(CuratedDeckLists.ToonTestExtra)
                .Distinct()
                .ToArray();

            foreach (uint code in codes)
            {
                CardRecord card = database.Get(code);
                Assert.That(card.Name, Is.Not.Empty, code.ToString());
                Assert.That(card.Description, Is.Not.Empty, card.Name);
                Assert.That(visuals.TryGet(code, out CardVisualData visual),
                    Is.True, card.Name);
                Assert.That(File.Exists(visuals.ArtPath(code)), Is.True, card.Name);
                Assert.That(visual.scriptStatus, Is.EqualTo("true"), card.Name);
                Assert.That(
                    File.Exists(Path.Combine(
                        Application.streamingAssetsPath,
                        "Ygo",
                        "Scripts",
                        "official",
                        visual.scriptFile)),
                    Is.True,
                    card.Name);
            }
        }

        [Test]
        public void PortugueseLocalizationCoversEveryAuthoredCoreCard()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string corePath = Path.Combine(
                projectRoot,
                "Documentation",
                "CoreCardCatalog.csv");
            string localizationPath = Path.Combine(
                projectRoot,
                "Documentation",
                "CardTextPtBr.json");
            uint[] authoredCodes = File.ReadAllLines(corePath)
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => line.Split(','))
                .Where(parts => parts.Length >= 3 &&
                    parts[2] != "runtime_dependency")
                .Select(parts => uint.Parse(parts[0]))
                .ToArray();
            LocalizationFile localization = JsonUtility.FromJson<LocalizationFile>(
                File.ReadAllText(localizationPath));
            var localized = new Dictionary<uint, LocalizationCard>();
            foreach (LocalizationCard card in localization.cards)
                localized[card.code] = card;

            Assert.That(authoredCodes, Has.Length.EqualTo(319));
            Assert.That(localized.Keys, Is.EquivalentTo(authoredCodes));
            CardDatabase database = CardDatabase.LoadDefault();
            foreach (uint code in authoredCodes)
            {
                LocalizationCard text = localized[code];
                Assert.That(text.name, Is.Not.Empty, code.ToString());
                Assert.That(text.description, Is.Not.Empty, text.name);
                CardRecord compiled = database.Get(code);
                Assert.That(compiled.Name, Is.EqualTo(text.name), code.ToString());
                Assert.That(compiled.Description, Is.EqualTo(text.description), text.name);
            }
        }
    }
}
