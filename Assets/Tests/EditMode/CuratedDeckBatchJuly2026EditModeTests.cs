using System;
using System.Collections;
using System.IO;
using System.Linq;
using ArcaneDuel.DuelEngine.Data;
using ArcaneDuel.Game;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArcaneDuel.Tests.EditMode
{
    public sealed class CuratedDeckBatchJuly2026EditModeTests
    {
        [Test]
        public void EveryCapturedDeckPreservesItsMainAndExtraSizes()
        {
            uint[][] mainDecks =
            {
                CuratedDeckLists.AzaminaIllusionsMain,
                CuratedDeckLists.PlantLinkMain,
                CuratedDeckLists.NoobsGaiaMain,
                CuratedDeckLists.SummonBansMain,
                CuratedDeckLists.StarWarriorLevel5XyzMain,
                CuratedDeckLists.AssaultModeGoodStuffMain,
                CuratedDeckLists.Dragones2Main,
                CuratedDeckLists.FemaleReptileMain,
                CuratedDeckLists.ReturnToSenderMain
            };
            uint[][] extraDecks =
            {
                CuratedDeckLists.AzaminaIllusionsExtra,
                CuratedDeckLists.PlantLinkExtra,
                CuratedDeckLists.NoobsGaiaExtra,
                CuratedDeckLists.SummonBansExtra,
                CuratedDeckLists.StarWarriorLevel5XyzExtra,
                CuratedDeckLists.AssaultModeGoodStuffExtra,
                CuratedDeckLists.Dragones2Extra,
                CuratedDeckLists.FemaleReptileExtra,
                CuratedDeckLists.ReturnToSenderExtra
            };
            int[] expectedMain = { 40, 40, 40, 40, 40, 40, 40, 42, 45 };
            int[] expectedExtra = { 15, 15, 10, 1, 15, 15, 1, 8, 0 };
            int[] expectedUnique = { 33, 36, 25, 21, 33, 33, 29, 31, 25 };

            for (int index = 0; index < mainDecks.Length; index++)
            {
                Assert.That(mainDecks[index], Has.Length.EqualTo(expectedMain[index]));
                Assert.That(extraDecks[index], Has.Length.EqualTo(expectedExtra[index]));
                Assert.That(
                    mainDecks[index].Concat(extraDecks[index]).Distinct().Count(),
                    Is.EqualTo(expectedUnique[index]));
            }
            Assert.That(CuratedDeckLists.SummonBansSide, Has.Length.EqualTo(9));
            Assert.That(
                mainDecks.SelectMany(cards => cards)
                    .Concat(extraDecks.SelectMany(cards => cards))
                    .Distinct()
                    .Count(),
                Is.EqualTo(252));
        }

        [Test]
        public void EveryBatchCardHasPortugueseDataArtAndResolvedRules()
        {
            CardDatabase database = CardDatabase.LoadDefault();
            CardVisualCatalog visuals = CardVisualCatalog.LoadDefault();
            uint[] codes = new[]
                {
                    CuratedDeckLists.AzaminaIllusionsMain,
                    CuratedDeckLists.AzaminaIllusionsExtra,
                    CuratedDeckLists.PlantLinkMain,
                    CuratedDeckLists.PlantLinkExtra,
                    CuratedDeckLists.NoobsGaiaMain,
                    CuratedDeckLists.NoobsGaiaExtra,
                    CuratedDeckLists.SummonBansMain,
                    CuratedDeckLists.SummonBansExtra,
                    CuratedDeckLists.StarWarriorLevel5XyzMain,
                    CuratedDeckLists.StarWarriorLevel5XyzExtra,
                    CuratedDeckLists.AssaultModeGoodStuffMain,
                    CuratedDeckLists.AssaultModeGoodStuffExtra,
                    CuratedDeckLists.Dragones2Main,
                    CuratedDeckLists.Dragones2Extra,
                    CuratedDeckLists.FemaleReptileMain,
                    CuratedDeckLists.FemaleReptileExtra,
                    CuratedDeckLists.ReturnToSenderMain,
                    CuratedDeckLists.ReturnToSenderExtra
                }
                .SelectMany(cards => cards)
                .Distinct()
                .ToArray();

            foreach (uint code in codes)
            {
                CardRecord card = database.Get(code);
                Assert.That(card.Name, Is.Not.Empty, code.ToString());
                Assert.That(card.Description, Is.Not.Empty, card.Name);
                Assert.That(
                    card.Description,
                    Does.Not.Contain("[ Pendulum Effect ]"),
                    card.Name);
                Assert.That(
                    card.Description,
                    Does.Not.Contain("[ Monster Effect ]"),
                    card.Name);
                Assert.That(
                    card.Description,
                    Does.Not.Contain("You can "),
                    card.Name);
                Assert.That(
                    card.Description,
                    Does.Not.Contain("Special Summon"),
                    card.Name);
                Assert.That(
                    visuals.TryGet(code, out CardVisualData visual),
                    Is.True,
                    card.Name);
                Assert.That(File.Exists(visuals.ArtPath(code)), Is.True, card.Name);
                if (visual.scriptStatus == "not_required_no_effect")
                    continue;
                Assert.That(
                    visual.scriptStatus == "true" || visual.scriptStatus == "via_alias",
                    Is.True,
                    card.Name);
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
        public void XyzPendulumMonsterRemainsInTheExtraDeck()
        {
            Type catalogType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly =>
                    assembly.GetType("ArcaneArena.Cards.CardCatalog"))
                .First(type => type != null);
            UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath(
                "Assets/Cards/CardCatalog.asset",
                catalogType);
            IEnumerable entries = (IEnumerable)catalogType
                .GetProperty("Entries")
                .GetValue(catalog);
            object machinex = entries.Cast<object>().Single(entry =>
                (string)entry.GetType()
                    .GetProperty("OfficialCardId")
                    .GetValue(entry) == "46593546");

            Assert.That(
                machinex.GetType()
                    .GetProperty("MonsterFrame")
                    .GetValue(machinex)
                    .ToString(),
                Is.EqualTo("Xyz"));
            Type repositoryType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly =>
                    assembly.GetType("ArcaneArena.Frontend.DeckRepository"))
                .First(type => type != null);
            bool belongs = (bool)repositoryType
                .GetMethod("BelongsToExtraDeck")
                .Invoke(null, new[] { machinex });
            Assert.That(belongs, Is.True);
        }
    }
}
